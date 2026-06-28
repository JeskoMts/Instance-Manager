using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using InstanceManager.Behaviors;
using InstanceManager.Models;
using InstanceManager.ViewModels;

namespace InstanceManager;

public partial class MainWindow : Window
{
    private static readonly string GlyphMaximize = ((char)0xE922).ToString();
    private static readonly string GlyphRestore = ((char)0xE923).ToString();

    private Point _dragStartPoint;
    private Point _grabOffset;
    private AccountRowViewModel? _dragRow;
    private DragAdorner? _dragAdorner;
    private Point _favoriteDragStartPoint;
    private Point _favoriteGrabOffset;
    private FavoriteGame? _dragFavorite;
    private bool _favoriteDragActive;
    private DragAdorner? _favoriteDragAdorner;
    private long _favoritesClosedAtTicks;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += (_, _) =>
        {
            UpdateMaxGlyph();
            if (WindowState == WindowState.Minimized)
                TrimWorkingSet();
        };

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.SelectedSection)
                && viewModel.SelectedSection == AppSection.Accounts)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    SearchBox.Focus();
                }));
            }
        };
    }

    private ShellViewModel? Vm => DataContext as ShellViewModel;


    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape && Vm is { SelectedSection: AppSection.Settings } vm)
        {
            vm.SelectedSection = AppSection.Accounts;
            e.Handled = true;
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        DependencyObject? originalSource = e.OriginalSource as DependencyObject;

        if (Keyboard.FocusedElement is TextBox focusedSearch
            && ReferenceEquals(focusedSearch.Style, TryFindResource("SearchTextBox"))
            && !IsDescendantOrSelf(originalSource, focusedSearch))
        {
            Keyboard.ClearFocus();
        }

        if (Vm is { } vm && !IsInsideInlineActionMenu(originalSource))
            vm.AccountList.CloseOpenMenu();

        Point posInFavorites = e.GetPosition(FavoritesCombo);
        bool pressOnFavoritesHeader =
            posInFavorites.X >= 0 && posInFavorites.Y >= 0
            && posInFavorites.X <= FavoritesCombo.ActualWidth
            && posInFavorites.Y <= FavoritesCombo.ActualHeight;
        if (pressOnFavoritesHeader)
        {
            if (FavoritesCombo.IsDropDownOpen)
                FavoritesCombo.IsDropDownOpen = false;
            else if (Environment.TickCount64 - _favoritesClosedAtTicks > 200)
                FavoritesCombo.IsDropDownOpen = true;
            e.Handled = true;
        }
        else if (FavoritesCombo.IsDropDownOpen && !IsInsideFavoritesPopup(originalSource))
        {
            FavoritesCombo.IsDropDownOpen = false;
        }

        base.OnPreviewMouseDown(e);
    }


    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

    private static void TrimWorkingSet()
    {
        try
        {
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
        }
        catch
        {
        }
    }

    private void MinButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void NotificationScrim_Click(object sender, MouseButtonEventArgs e)
    {
        if (Vm is { } vm)
            vm.Notifications.IsCenterOpen = false;
        e.Handled = true;
    }

    private void UpdateMaxGlyph() =>
        MaxButton.Content = WindowState == WindowState.Maximized ? GlyphRestore : GlyphMaximize;


    private bool _suppressRowClick;

    private void AccountRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (_suppressRowClick)
        {
            _suppressRowClick = false;
            return;
        }
        if (IsInteractiveOriginalSource(e.OriginalSource as DependencyObject))
            return;
        if (sender is FrameworkElement { DataContext: AccountRowViewModel row })
            row.ToggleMenu();
    }

    private void AccountMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AccountRowViewModel row })
            row.ToggleMenu();
        e.Handled = true;
    }

    private void GroupMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GroupViewModel g })
            g.ToggleMenu();
        e.Handled = true;
    }


    private void AccountRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressRowClick = false;

        if (IsInteractiveOriginalSource(e.OriginalSource as DependencyObject))
        {
            _dragRow = null;
            return;
        }

        if (sender is FrameworkElement { DataContext: AccountRowViewModel row } fe)
        {
            _dragRow = row;
            _dragStartPoint = e.GetPosition(null);
            _grabOffset = e.GetPosition(fe);
        }
    }

    private void AccountRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragRow is not { } row || e.LeftButton != MouseButtonState.Pressed || sender is not Border source)
            return;

        Point pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragRow = null;
        _suppressRowClick = true;
        var data = new DataObject(typeof(AccountRowViewModel), row);

        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(AccountsList);
        if (layer != null)
        {
            _dragAdorner = new DragAdorner(AccountsList, CreateDragGhost(source));
            _dragAdorner.SetPosition(SubtractGrab(e.GetPosition(AccountsList)));
            layer.Add(_dragAdorner);
        }

        source.Opacity = 0.4;
        try
        {
            DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
        }
        finally
        {
            source.Opacity = 1.0;
            if (_dragAdorner != null)
            {
                layer?.Remove(_dragAdorner);
                _dragAdorner = null;
            }
        }
    }

    private Point SubtractGrab(Point cursor) => new(cursor.X - _grabOffset.X, cursor.Y - _grabOffset.Y);

    private void UpdateDragAdorner(DragEventArgs e) =>
        _dragAdorner?.SetPosition(SubtractGrab(e.GetPosition(AccountsList)));

    private void AccountsList_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragAdorner(e);
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private static Image CreateDragGhost(FrameworkElement source)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(source);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(source);
        bitmap.Freeze();
        return new Image
        {
            Source = bitmap,
            Width = source.ActualWidth,
            Height = source.ActualHeight,
            Opacity = 0.8,
            IsHitTestVisible = false
        };
    }

    private void GroupHeader_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragAdorner(e);
        bool valid = TryGetDropTarget(sender, e, out _);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        SetHeaderHighlight(sender as Border, valid);
        e.Handled = true;
    }

    private void GroupHeader_DragLeave(object sender, DragEventArgs e)
    {
        SetHeaderHighlight(sender as Border, false);
        e.Handled = true;
    }

    private void GroupHeader_Drop(object sender, DragEventArgs e)
    {
        SetHeaderHighlight(sender as Border, false);
        if (TryGetDropTarget(sender, e, out (AccountRowViewModel Row, GroupViewModel Group) target) && Vm is { } vm)
        {
            vm.AccountList.DropAccountOnGroup(target.Row, target.Group);
            AnimateItemMove(AccountsList, target.Row);
        }
        e.Handled = true;
    }

    private void AccountRow_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragAdorner(e);
        bool valid = TryGetReorderTarget(sender, e, out _);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        SetRowDropIndicator(sender as Border, valid);
        e.Handled = true;
    }

    private void AccountRow_DragLeave(object sender, DragEventArgs e)
    {
        SetRowDropIndicator(sender as Border, false);
        e.Handled = true;
    }

    private void AccountRow_Drop(object sender, DragEventArgs e)
    {
        SetRowDropIndicator(sender as Border, false);
        if (TryGetReorderTarget(sender, e, out (AccountRowViewModel Dragged, AccountRowViewModel Target) t) && Vm is { } vm)
        {
            vm.AccountList.ReorderAccount(t.Dragged, t.Target);
            AnimateItemMove(AccountsList, t.Dragged);
        }
        e.Handled = true;
    }

    private static bool TryGetReorderTarget(object sender, DragEventArgs e, out (AccountRowViewModel Dragged, AccountRowViewModel Target) target)
    {
        target = default;
        if (sender is not FrameworkElement { DataContext: AccountRowViewModel targetRow })
            return false;
        if (e.Data.GetData(typeof(AccountRowViewModel)) is not AccountRowViewModel dragged)
            return false;
        if (ReferenceEquals(dragged, targetRow))
            return false;
        target = (dragged, targetRow);
        return true;
    }

    private void SetRowDropIndicator(Border? border, bool on)
    {
        if (border == null) return;
        border.BorderBrush = (Brush)FindResource(on ? "Brush.Accent" : "Brush.Border");
    }

    private void AnimateItemMove(ItemsControl list, object item)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is not UIElement container)
                return;

            var slide = new TranslateTransform();
            container.RenderTransform = slide;

            var emphasized = (IEasingFunction)FindResource("EaseOutEmphasized");
            var standard = (IEasingFunction)FindResource("EaseOutStandard");

            slide.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = emphasized, FillBehavior = FillBehavior.Stop });
            container.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = standard, FillBehavior = FillBehavior.Stop });
        }));
    }

    private static bool TryGetDropTarget(object sender, DragEventArgs e, out (AccountRowViewModel Row, GroupViewModel Group) target)
    {
        target = default;
        if (sender is not FrameworkElement { DataContext: GroupViewModel group } || !group.HasGroup)
            return false;
        if (e.Data.GetData(typeof(AccountRowViewModel)) is not AccountRowViewModel row)
            return false;
        target = (row, group);
        return true;
    }

    private void SetHeaderHighlight(Border? border, bool on)
    {
        if (border == null) return;
        border.BorderBrush = (Brush)FindResource(on ? "Brush.Accent" : "Brush.Border");
    }

    private Point _themeDragStart;
    private Point _themeGrabOffset;
    private ThemeDefinition? _dragTheme;
    private FrameworkElement? _themeDragSource;
    private DragAdorner? _themeDragAdorner;

    private void ThemeCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThemeDefinition theme } fe)
        {
            _dragTheme = theme;
            _themeDragSource = fe;
            _themeDragStart = e.GetPosition(null);
            _themeGrabOffset = e.GetPosition(fe);
            e.Handled = true;
        }
    }

    private void ThemeCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_themeDragSource is { DataContext: ThemeDefinition theme }
            && ReferenceEquals(_dragTheme, theme)
            && Vm is { } vm)
        {
            vm.Theme.Activate(theme);
        }
        _dragTheme = null;
        _themeDragSource = null;
    }

    private void ThemeCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTheme is not { } theme || _themeDragSource is not { } source || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _themeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _themeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragTheme = null;
        _themeDragSource = null;

        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(ThemesList);
        if (layer != null)
        {
            _themeDragAdorner = new DragAdorner(ThemesList, CreateDragGhost(source));
            _themeDragAdorner.SetPosition(SubtractThemeGrab(e.GetPosition(ThemesList)));
            layer.Add(_themeDragAdorner);
        }

        source.Opacity = 0.4;
        try
        {
            DragDrop.DoDragDrop(source, new DataObject(typeof(ThemeDefinition), theme), DragDropEffects.Move);
        }
        finally
        {
            source.Opacity = 1.0;
            if (_themeDragAdorner != null)
            {
                layer?.Remove(_themeDragAdorner);
                _themeDragAdorner = null;
            }
        }
    }

    private Point SubtractThemeGrab(Point cursor) => new(cursor.X - _themeGrabOffset.X, cursor.Y - _themeGrabOffset.Y);

    private void ThemesList_DragOver(object sender, DragEventArgs e)
    {
        _themeDragAdorner?.SetPosition(SubtractThemeGrab(e.GetPosition(ThemesList)));
        TryGetDraggedTheme(e.Data, out ThemeDefinition? draggedTheme);
        e.Effects = ThemeDragDecision.DropEffectFor(draggedTheme);
        e.Handled = true;
    }

    private void ThemesList_Drop(object sender, DragEventArgs e) => e.Handled = true;

    private void ThemeCard_DragOver(object sender, DragEventArgs e)
    {
        _themeDragAdorner?.SetPosition(SubtractThemeGrab(e.GetPosition(ThemesList)));
        TryGetDraggedTheme(e.Data, out ThemeDefinition? draggedTheme);
        bool valid = TryGetThemeReorderTarget(sender, e, out _);
        e.Effects = ThemeDragDecision.DropEffectFor(draggedTheme);
        SetThemeDropIndicator(sender as DependencyObject, valid);
        e.Handled = true;
    }

    private void ThemeCard_DragLeave(object sender, DragEventArgs e)
    {
        SetThemeDropIndicator(sender as DependencyObject, false);
        e.Handled = true;
    }

    private void ThemeCard_Drop(object sender, DragEventArgs e)
    {
        SetThemeDropIndicator(sender as DependencyObject, false);
        if (TryGetThemeReorderTarget(sender, e, out (ThemeDefinition Dragged, ThemeDefinition Target) t) && Vm is { } vm)
        {
            IReadOnlyList<ThemeDefinition> desired = ThemeSwap.SwapPair(vm.Theme.Themes, t.Dragged, t.Target);
            Dictionary<ThemeDefinition, Point> positionsBeforeSwap = CaptureThemePositions(includeRenderTransform: true);
            vm.Theme.ApplyOrder(desired);
            AnimateThemeReorder(positionsBeforeSwap);
        }
        e.Handled = true;
    }

    private static void SetThemeDropIndicator(DependencyObject? item, bool on)
    {
        if (item != null)
            ThemeDrag.SetIsDropTarget(item, on);
    }

    private static bool TryGetThemeReorderTarget(object sender, DragEventArgs e, out (ThemeDefinition Dragged, ThemeDefinition Target) target)
    {
        target = default;
        if (sender is not FrameworkElement { DataContext: ThemeDefinition targetTheme })
            return false;
        if (!TryGetDraggedTheme(e.Data, out ThemeDefinition? dragged))
            return false;
        if (!ThemeDragDecision.CanReorder(dragged, targetTheme))
            return false;
        target = (dragged!, targetTheme);
        return true;
    }

    private static bool TryGetDraggedTheme(IDataObject data, out ThemeDefinition? draggedTheme)
    {
        draggedTheme = data.GetData(typeof(ThemeDefinition)) as ThemeDefinition;
        return draggedTheme is not null;
    }

    private Dictionary<ThemeDefinition, Point> CaptureThemePositions(bool includeRenderTransform)
    {
        var positions = new Dictionary<ThemeDefinition, Point>();
        foreach (ThemeDefinition theme in ThemesList.Items)
        {
            if (ThemesList.ItemContainerGenerator.ContainerFromItem(theme) is not UIElement container)
                continue;

            Vector layoutOffset = VisualTreeHelper.GetOffset(container);
            Point position = new(layoutOffset.X, layoutOffset.Y);
            if (includeRenderTransform && container.RenderTransform is TranslateTransform transform)
                position.Offset(transform.X, transform.Y);
            positions[theme] = position;
        }

        return positions;
    }

    private void AnimateThemeReorder(IReadOnlyDictionary<ThemeDefinition, Point> positionsBeforeMove)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Dictionary<ThemeDefinition, Point> positionsAfterMove = CaptureThemePositions(includeRenderTransform: false);
            IReadOnlyDictionary<ThemeDefinition, Vector> offsets =
                ThemeReorderAnimation.CalculateOffsets(positionsBeforeMove, positionsAfterMove);
            var easing = (IEasingFunction)FindResource("EaseOutEmphasized");

            foreach ((ThemeDefinition theme, Vector offset) in offsets)
            {
                if (ThemesList.ItemContainerGenerator.ContainerFromItem(theme) is not UIElement container)
                    continue;

                var slide = new TranslateTransform();
                container.RenderTransform = slide;
                slide.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(offset.X, 0, TimeSpan.FromMilliseconds(260))
                    {
                        EasingFunction = easing,
                        FillBehavior = FillBehavior.Stop
                    });
                slide.BeginAnimation(
                    TranslateTransform.YProperty,
                    new DoubleAnimation(offset.Y, 0, TimeSpan.FromMilliseconds(260))
                    {
                        EasingFunction = easing,
                        FillBehavior = FillBehavior.Stop
                    });
            }
        }));
    }

    private static bool IsInteractiveOriginalSource(DependencyObject? source)
    {
        DependencyObject? node = source;
        while (node != null)
        {
            if (node is ButtonBase or ComboBox or TextBoxBase)
                return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private bool IsInsideFavoritesPopup(DependencyObject? source)
    {
        if (FavoritesCombo.Template?.FindName("Popup", FavoritesCombo) is not Popup popup
            || popup.Child is not DependencyObject content)
            return false;
        return IsDescendantOrSelf(source, content);
    }

    private static bool IsDescendantOrSelf(DependencyObject? source, DependencyObject ancestor)
    {
        DependencyObject? node = source;
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;

            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return false;
    }

    private static bool IsInsideInlineActionMenu(DependencyObject? source)
    {
        DependencyObject? node = source;
        while (node != null)
        {
            if (node is FrameworkElement { Tag: "InlineActionMenu" or "InlineMenuToggle" })
                return true;

            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return false;
    }


    private void FavoriteRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveOriginalSource(e.OriginalSource as DependencyObject))
        {
            _dragFavorite = null;
            return;
        }

        if (sender is FrameworkElement { DataContext: FavoriteGame favorite } fe)
        {
            _dragFavorite = favorite;
            _favoriteDragStartPoint = e.GetPosition(null);
            _favoriteGrabOffset = e.GetPosition(fe);
        }
    }

    private void FavoriteRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragFavorite is not { } favorite || e.LeftButton != MouseButtonState.Pressed || sender is not Border source)
            return;

        Point position = e.GetPosition(null);
        if (Math.Abs(position.X - _favoriteDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _favoriteDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragFavorite = null;
        _favoriteDragActive = true;

        UIElement? ghostHost = FavoriteDragHost();
        AdornerLayer? layer = ghostHost != null ? AdornerLayer.GetAdornerLayer(ghostHost) : null;
        if (ghostHost != null && layer != null)
        {
            _favoriteDragAdorner = new DragAdorner(ghostHost, CreateDragGhost(source));
            _favoriteDragAdorner.SetPosition(SubtractFavoriteGrab(e.GetPosition(ghostHost)));
            layer.Add(_favoriteDragAdorner);
        }

        source.Opacity = 0.4;
        try
        {
            DragDrop.DoDragDrop(source, new DataObject(typeof(FavoriteGame), favorite), DragDropEffects.Move);
        }
        finally
        {
            source.Opacity = 1.0;
            _favoriteDragActive = false;
            if (_favoriteDragAdorner != null)
            {
                layer?.Remove(_favoriteDragAdorner);
                _favoriteDragAdorner = null;
            }
            FavoritesCombo.IsDropDownOpen = true;
        }
    }

    private UIElement? FavoriteDragHost() =>
        FavoritesCombo.Template?.FindName("PART_FavoriteDragHost", FavoritesCombo) as UIElement;

    private Point SubtractFavoriteGrab(Point cursor) =>
        new(cursor.X - _favoriteGrabOffset.X, cursor.Y - _favoriteGrabOffset.Y);

    private void UpdateFavoriteDragAdorner(DragEventArgs e)
    {
        if (_favoriteDragAdorner != null && FavoriteDragHost() is { } host)
            _favoriteDragAdorner.SetPosition(SubtractFavoriteGrab(e.GetPosition(host)));
    }

    private void FavoritesCombo_DropDownClosed(object sender, EventArgs e)
    {
        if (_favoriteDragActive)
        {
            FavoritesCombo.IsDropDownOpen = true;
            return;
        }
        _favoritesClosedAtTicks = Environment.TickCount64;
    }

    private void FavoriteRow_DragOver(object sender, DragEventArgs e)
    {
        UpdateFavoriteDragAdorner(e);
        bool valid = TryGetFavoriteReorderTarget(sender, e, out _);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        SetFavoriteRowDropIndicator(sender as Border, valid);
        e.Handled = true;
    }

    private void FavoriteRow_DragLeave(object sender, DragEventArgs e)
    {
        SetFavoriteRowDropIndicator(sender as Border, false);
        e.Handled = true;
    }

    private void FavoriteRow_Drop(object sender, DragEventArgs e)
    {
        SetFavoriteRowDropIndicator(sender as Border, false);
        if (TryGetFavoriteReorderTarget(sender, e, out (FavoriteGame Dragged, FavoriteGame Target) target) && Vm is { } vm)
            vm.LaunchPanel.ReorderFavorite(target.Dragged, target.Target);
        e.Handled = true;
    }

    private static bool TryGetFavoriteReorderTarget(
        object sender,
        DragEventArgs e,
        out (FavoriteGame Dragged, FavoriteGame Target) target)
    {
        target = default;
        if (sender is not FrameworkElement { DataContext: FavoriteGame targetFavorite })
            return false;
        if (e.Data.GetData(typeof(FavoriteGame)) is not FavoriteGame draggedFavorite)
            return false;
        if (ReferenceEquals(draggedFavorite, targetFavorite) || draggedFavorite.IsPrimary != targetFavorite.IsPrimary)
            return false;

        target = (draggedFavorite, targetFavorite);
        return true;
    }

    private void SetFavoriteRowDropIndicator(Border? border, bool on)
    {
        if (border == null) return;
        border.BorderBrush = (Brush)FindResource(on ? "Brush.Accent" : "Brush.Border");
    }

    private void FavoriteRowButton_Click(object sender, RoutedEventArgs e) => e.Handled = true;

    private void FavoritesCombo_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is not ComboBox combo)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (combo.Template?.FindName("PART_FavoriteSearch", combo) is TextBox search)
            {
                search.Focus();
                Keyboard.Focus(search);
            }
        }));
    }

    private void ToastLife_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border { RenderTransform: ScaleTransform scale } life)
            return;

        int ms = (life.DataContext as ToastViewModel)?.LifetimeMs ?? 0;
        if (ms <= 0)
            return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms)) { FillBehavior = FillBehavior.HoldEnd });
    }

    private void ToastContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement host)
            host.Clip = new RectangleGeometry(new Rect(e.NewSize), 15, 15);
    }
}
