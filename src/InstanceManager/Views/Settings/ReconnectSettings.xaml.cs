using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InstanceManager.ViewModels;

namespace InstanceManager.Views.Settings;

public partial class ReconnectSettings : UserControl
{
    public ReconnectSettings() => InitializeComponent();

    private void AntiAfkKeyButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: OperationsViewModel { IsCapturingAntiAfkKey: true } operations })
            return;

        Key key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };
        e.Handled = true;
        operations.CaptureAntiAfkKey(key);
    }

    private void AntiAfkKeyButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: OperationsViewModel operations })
            operations.CancelAntiAfkKeyCapture();
    }
}
