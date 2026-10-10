using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using InstanceManager.Behaviors;
using InstanceManager.Controls;
using InstanceManager.ViewModels;

namespace InstanceManager;

public partial class MainWindow : Window
{
    private const string DiscordInviteUrl = "https://discord.gg/8XyKcZdSGe";

    private bool _quitting;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += (_, _) =>
        {
            UpdateMaxIcon();
            if (WindowState == WindowState.Minimized)
                TrimWorkingSet();
        };
    }

    public event Action? HiddenToTray;

    public Func<bool>? CloseToTray { get; set; }

    private ShellViewModel? Vm => DataContext as ShellViewModel;

    public void ShowFromTray()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void Quit()
    {
        _quitting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_quitting && CloseToTray?.Invoke() == true)
        {
            e.Cancel = true;
            Hide();
            TrimWorkingSet();
            HiddenToTray?.Invoke();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as DependencyObject;
        if (Keyboard.FocusedElement is TextBoxBase editing
            && !TreeWalk.IsDescendantOrSelf(originalSource, editing))
        {
            ReleaseTextFocus();
        }

        AccountsView.HandleWindowMouseDown(e);
        base.OnPreviewMouseDown(e);
    }

    private void ReleaseTextFocus()
    {
        FocusManager.SetFocusedElement(this, null);
        Keyboard.ClearFocus();
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

    private void DiscordButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = DiscordInviteUrl, UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void NotificationScrim_Click(object sender, MouseButtonEventArgs e)
    {
        if (Vm is { } vm)
            vm.Notifications.IsCenterOpen = false;
        e.Handled = true;
    }

    private void UpdateMaxIcon() =>
        MaxIcon.Kind = WindowState == WindowState.Maximized ? IconKind.Restore : IconKind.Maximize;
}
