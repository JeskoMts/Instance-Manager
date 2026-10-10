using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using InstanceManager.Services;
using InstanceManager.ViewModels;

namespace InstanceManager.Views.Pages;

public partial class AutomationPage : UserControl
{
    private AutomationViewModel? _observed;

    public AutomationPage()
    {
        InitializeComponent();
        Root.DataContextChanged += (_, _) => Observe(Root.DataContext as AutomationViewModel);
        Observe(Root.DataContext as AutomationViewModel);
    }

    private void Observe(AutomationViewModel? vm)
    {
        if (_observed != null)
            _observed.Created -= OnCreated;
        _observed = vm;
        if (_observed != null)
            _observed.Created += OnCreated;
    }

    private void OnCreated(AutomationItemViewModel item)
    {
        AutomationList.ScrollIntoView(item);
        EditorScroller.ScrollToVerticalOffset(0);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }));
    }

    private void TimeBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || e.Key is not (Key.Up or Key.Down))
            return;

        TimeSpan current = ScheduleClock.TryParseTime(box.Text, out TimeSpan parsed) ? parsed : TimeSpan.FromHours(18);
        int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 60 : 5;
        int minutes = (int)current.TotalMinutes + (e.Key == Key.Up ? step : -step);
        minutes = ((minutes % 1440) + 1440) % 1440;
        box.Text = $"{minutes / 60:00}:{minutes % 60:00}";
        box.CaretIndex = box.Text.Length;
        e.Handled = true;
    }
}
