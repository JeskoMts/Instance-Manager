using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using InstanceManager.Behaviors;
using InstanceManager.Controls;
using InstanceManager.Models;
using InstanceManager.ViewModels;

namespace InstanceManager.Views.Pages;

public partial class AccountsPage : UserControl
{
    private Point _dragStartPoint;
    private Point _grabOffset;
    private AccountRowViewModel? _dragRow;
    private DragAdorner? _dragAdorner;
    private bool _suppressRowClick;
    private Point _favoriteDragStartPoint;
    private Point _favoriteGrabOffset;
    private FavoriteGame? _dragFavorite;
    private bool _favoriteDragActive;
    private DragAdorner? _favoriteDragAdorner;
    private long _favoritesClosedAtTicks;

    public AccountsPage()
    {
        InitializeComponent();
    }

    private ShellViewModel? Vm => DataContext as ShellViewModel;

    public void HandleWindowMouseDown(MouseButtonEventArgs e)
    {
        if (!IsVisible)
            return;

        var originalSource = e.OriginalSource as DependencyObject;
        Point position = e.GetPosition(FavoritesCombo);
        bool onHeader = position.X >= 0 && position.Y >= 0
                        && position.X <= FavoritesCombo.ActualWidth
                        && position.Y <= FavoritesCombo.ActualHeight;
        if (onHeader)
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
    }

    private void AccountRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (_suppressRowClick)
        {
            _suppressRowClick = false;
            return;
        }
        if (TreeWalk.IsInteractive(e.OriginalSource as DependencyObject))
            return;
        if (sender is FrameworkElement { DataContext: AccountRowViewModel row })
            row.IsSelected = !row.IsSelected;
    }

    private void AccountRow_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AccountRowViewModel row } anchor)
        {
            OpenAccountMenu(anchor, row, PlacementMode.MousePoint);
            e.Handled = true;
        }
    }

    private void AccountMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AccountRowViewModel row } anchor)
            OpenAccountMenu(anchor, row, PlacementMode.Bottom);
        e.Handled = true;
    }

    private void GroupMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GroupViewModel group } anchor)
            OpenGroupMenu(anchor, group, PlacementMode.Bottom);
        e.Handled = true;
    }

    private void GroupHeader_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GroupViewModel { HasGroup: true } group } anchor)
        {
            OpenGroupMenu(anchor, group, PlacementMode.MousePoint);
            e.Handled = true;
        }
    }

    private void GroupHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (TreeWalk.IsInteractive(e.OriginalSource as DependencyObject))
            return;
        if (sender is FrameworkElement { DataContext: GroupViewModel group })
            group.IsExpanded = !group.IsExpanded;
    }

    private void OpenAccountMenu(FrameworkElement anchor, AccountRowViewModel row, PlacementMode placement)
    {
        if (Vm is not { } vm)
            return;

        ContextMenu menu = NewMenu(anchor, placement);
        MenuItem primary = row.IsRunning
            ? MenuEntry("Stop", IconKind.Stop, row.StopCommand, danger: true)
            : MenuEntry("Launch", IconKind.Play, row.LaunchCommand);
        primary.IsEnabled = row.IsRunning || !vm.IsBusy;
        menu.Items.Add(primary);

        MenuItem hop = MenuEntry("Join another server", IconKind.Shuffle);
        hop.IsEnabled = !vm.IsBusy;
        hop.Click += (_, _) => _ = vm.ServerHopAsync(new[] { row.Account });
        menu.Items.Add(hop);

        menu.Items.Add(MenuEntry("Rename…", IconKind.Pencil, row.RenameCommand));
        menu.Items.Add(new Separator());

        MenuItem versions = MenuEntry("Roblox version", IconKind.Layers);
        foreach (VersionChoiceViewModel choice in row.VersionChoices)
        {
            var item = new MenuItem
            {
                Header = choice.Label,
                IsCheckable = true,
                IsChecked = ReferenceEquals(choice, row.SelectedVersionChoice)
            };
            item.Click += (_, _) => row.SelectedVersionChoice = choice;
            versions.Items.Add(item);
        }
        menu.Items.Add(versions);

        if (vm.AccountList.GroupModels.Count > 0)
        {
            MenuItem groups = MenuEntry("Groups", IconKind.Folder);
            foreach (AccountGroup group in vm.AccountList.GroupModels)
            {
                var header = new StackPanel { Orientation = Orientation.Horizontal };
                header.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Fill = GroupBrush(group.ColorHex)
                });
                header.Children.Add(new TextBlock { Text = group.Name, VerticalAlignment = VerticalAlignment.Center });
                var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = row.Account.BelongsTo(group.Id) };
                item.Click += (_, _) => vm.AccountList.ToggleGroupMembership(row, group);
                groups.Items.Add(item);
            }
            if (row.IsGrouped)
            {
                groups.Items.Add(new Separator());
                groups.Items.Add(MenuEntry("Remove from all groups", IconKind.FolderMinus, row.ClearGroupsCommand));
            }
            menu.Items.Add(groups);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuEntry("Remove account", IconKind.Trash, row.RemoveCommand, danger: true));
        menu.IsOpen = true;
    }

    private void OpenGroupMenu(FrameworkElement anchor, GroupViewModel group, PlacementMode placement)
    {
        ContextMenu menu = NewMenu(anchor, placement);
        menu.Style = (Style)FindResource("GroupActionMenu");
        menu.DataContext = group;
        MenuItem launch = MenuEntry("Launch group", IconKind.Play, group.LaunchGroupCommand);
        launch.IsEnabled = Vm is not { IsBusy: true };
        menu.Items.Add(launch);
        menu.Items.Add(MenuEntry("Edit group…", IconKind.Pencil, group.RenameGroupCommand));
        menu.Items.Add(MenuEntry("Delete group", IconKind.Trash, group.DeleteGroupCommand, danger: true));
        menu.IsOpen = true;
    }

    private static ContextMenu NewMenu(FrameworkElement anchor, PlacementMode placement)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = placement };
        if (placement == PlacementMode.Bottom)
            menu.Opened += (_, _) => menu.HorizontalOffset = anchor.ActualWidth - menu.ActualWidth;
        return menu;
    }

    private MenuItem MenuEntry(string header, IconKind icon, ICommand? command = null, bool danger = false)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            Icon = new Icon { Kind = icon, Size = 15 }
        };
        if (danger)
            item.Foreground = (Brush)FindResource("Brush.Danger");
        return item;
    }

    private static Brush GroupBrush(string hex)
    {
        try { return (Brush)new BrushConverter().ConvertFromString(hex)!; }
        catch (FormatException) { return Brushes.Gray; }
    }

    private void AccountRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressRowClick = false;

        if (TreeWalk.IsInteractive(e.OriginalSource as DependencyObject))
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
            _dragAdorner = new DragAdorner(AccountsList, DragAdorner.CreateGhost(source));
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

    private void GroupHeader_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragAdorner(e);
        bool valid = TryGetDropTarget(sender, e, out _);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        SetHighlight(sender as Border, valid);
        e.Handled = true;
    }

    private void GroupHeader_DragLeave(object sender, DragEventArgs e)
    {
        SetHighlight(sender as Border, false);
        e.Handled = true;
    }

    private void GroupHeader_Drop(object sender, DragEventArgs e)
    {
        SetHighlight(sender as Border, false);
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
        SetHighlight(sender as Border, valid);
        e.Handled = true;
    }

    private void AccountRow_DragLeave(object sender, DragEventArgs e)
    {
        SetHighlight(sender as Border, false);
        e.Handled = true;
    }

    private void AccountRow_Drop(object sender, DragEventArgs e)
    {
        SetHighlight(sender as Border, false);
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

    private static bool TryGetDropTarget(object sender, DragEventArgs e, out (AccountRowViewModel Row, GroupViewModel Group) target)
    {
        target = default;
        if (sender is not FrameworkElement { DataContext: GroupViewModel group })
            return false;
        if (e.Data.GetData(typeof(AccountRowViewModel)) is not AccountRowViewModel row)
            return false;
        target = (row, group);
        return true;
    }

    private void SetHighlight(Border? border, bool on)
    {
        if (border != null)
            border.BorderBrush = on ? (Brush)FindResource("Brush.Accent") : Brushes.Transparent;
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

    private bool IsInsideFavoritesPopup(DependencyObject? source)
    {
        if (FavoritesCombo.Template?.FindName("Popup", FavoritesCombo) is not Popup popup
            || popup.Child is not DependencyObject content)
            return false;
        return TreeWalk.IsDescendantOrSelf(source, content);
    }

    private void FavoriteRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TreeWalk.IsInteractive(e.OriginalSource as DependencyObject))
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
            _favoriteDragAdorner = new DragAdorner(ghostHost, DragAdorner.CreateGhost(source));
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
        SetHighlight(sender as Border, valid);
        e.Handled = true;
    }

    private void FavoriteRow_DragLeave(object sender, DragEventArgs e)
    {
        SetHighlight(sender as Border, false);
        e.Handled = true;
    }

    private void FavoriteRow_Drop(object sender, DragEventArgs e)
    {
        SetHighlight(sender as Border, false);
        if (TryGetFavoriteReorderTarget(sender, e, out (FavoriteGame Dragged, FavoriteGame Target) target) && Vm is { } vm)
            vm.LaunchPanel.ReorderFavorite(target.Dragged, target.Target);
        e.Handled = true;
    }

    private static bool TryGetFavoriteReorderTarget(object sender, DragEventArgs e, out (FavoriteGame Dragged, FavoriteGame Target) target)
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
}
