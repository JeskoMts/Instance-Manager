using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using InstanceManager.Views.Settings;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AntiAfkKeyCaptureTests : IDisposable
{
    private readonly InstanceTracker _tracker = new();
    private readonly RecordingSettings _settings = new();
    private readonly AntiAfkService _antiAfk;
    private readonly List<(string Message, bool Error)> _messages = new();

    public AntiAfkKeyCaptureTests()
    {
        _antiAfk = new AntiAfkService(_tracker, _settings);
    }

    public void Dispose()
    {
        _antiAfk.Dispose();
        _tracker.Dispose();
    }

    [Fact]
    public void PressingAKeyWhileCapturing_MakesItTheAntiAfkKeyAndSavesIt()
    {
        OperationsViewModel vm = Create();

        vm.BeginAntiAfkKeyCaptureCommand.Execute(null);
        Assert.True(vm.IsCapturingAntiAfkKey);
        vm.CaptureAntiAfkKey(Key.F13);

        Assert.False(vm.IsCapturingAntiAfkKey);
        Assert.Equal("F13", vm.AntiAfkKey);
        Assert.Equal("F13", _settings.Settings.AntiAfkKey);
        Assert.Equal(1, _settings.SaveCount);
        Assert.Empty(_messages);
    }

    [Fact]
    public void Escape_CancelsTheCaptureAndKeepsTheKey()
    {
        OperationsViewModel vm = Create();

        vm.BeginAntiAfkKeyCaptureCommand.Execute(null);
        vm.CaptureAntiAfkKey(Key.Escape);

        Assert.False(vm.IsCapturingAntiAfkKey);
        Assert.Equal("Space", vm.AntiAfkKey);
        Assert.Equal(0, _settings.SaveCount);
        Assert.Empty(_messages);
    }

    [Theory]
    [InlineData(Key.LWin)]
    [InlineData(Key.VolumeMute)]
    public void AKeyThatActsOutsideRoblox_IsRefusedWithAnErrorMessage(Key key)
    {
        OperationsViewModel vm = Create();

        vm.BeginAntiAfkKeyCaptureCommand.Execute(null);
        vm.CaptureAntiAfkKey(key);

        Assert.False(vm.IsCapturingAntiAfkKey);
        Assert.Equal("Space", vm.AntiAfkKey);
        Assert.Equal(0, _settings.SaveCount);
        Assert.True(Assert.Single(_messages).Error);
    }

    [Fact]
    public void KeyPresses_OutsideACapture_ChangeNothing()
    {
        OperationsViewModel vm = Create();

        vm.CaptureAntiAfkKey(Key.F13);

        Assert.Equal("Space", vm.AntiAfkKey);
        Assert.Equal(0, _settings.SaveCount);
    }

    [Fact]
    public void TheKeyLabel_FollowsTheCaptureAndTheNewKey()
    {
        OperationsViewModel vm = Create();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.BeginAntiAfkKeyCaptureCommand.Execute(null);
        Assert.Contains(nameof(OperationsViewModel.AntiAfkKeyLabel), changed);

        changed.Clear();
        vm.CaptureAntiAfkKey(Key.LeftShift);
        Assert.Contains(nameof(OperationsViewModel.AntiAfkKeyLabel), changed);
        Assert.Equal("Left Shift", vm.AntiAfkKeyLabel);
    }

    private OperationsViewModel Create()
    {
        var vm = new OperationsViewModel(_settings, _antiAfk);
        vm.MessagePosted += (message, error) => _messages.Add((message, error));
        return vm;
    }
}

[Collection("WPF application")]
public sealed class AntiAfkKeyButtonTests
{
    [Fact]
    public void ClickingTheButtonThenPressingAKey_ChangesTheKeyAndTheLabel() =>
        OnPage((page, operations, settings) =>
        {
            Button button = page.AntiAfkKeyButton;
            Assert.True(button.IsEnabled);
            Assert.Equal("Space", button.Content);

            button.Command.Execute(null);
            Assert.Equal("Press a key…", button.Content);

            KeyEventArgs pressed = Press(button, Key.F13);

            Assert.True(pressed.Handled);
            Assert.Equal("F13", settings.Settings.AntiAfkKey);
            Assert.Equal("F13", button.Content);
        });

    [Fact]
    public void KeyPresses_WhenTheButtonIsNotCapturing_AreLeftToTheButton() =>
        OnPage((page, operations, settings) =>
        {
            KeyEventArgs pressed = Press(page.AntiAfkKeyButton, Key.Space);

            Assert.False(pressed.Handled);
            Assert.Equal("Space", settings.Settings.AntiAfkKey);
            Assert.Equal(0, settings.SaveCount);
        });

    [Fact]
    public void LosingKeyboardFocus_EndsTheCapture() =>
        OnPage((page, operations, settings) =>
        {
            Button button = page.AntiAfkKeyButton;
            button.Command.Execute(null);
            Assert.True(operations.IsCapturingAntiAfkKey);

            button.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, null)
            {
                RoutedEvent = Keyboard.LostKeyboardFocusEvent
            });

            Assert.False(operations.IsCapturingAntiAfkKey);
            Assert.Equal("Space", button.Content);
        });

    private static void OnPage(Action<ReconnectSettings, OperationsViewModel, RecordingSettings> test)
    {
        Exception? failure = ThemedDialogResourceTests.RunOnStaThread(() =>
        {
            ThemedDialogResourceTests.EnsureAppResources();
            ThemedDialogResourceTests.EnsureAppConverters();

            var settings = new RecordingSettings();
            settings.Settings.AntiAfkEnabled = true;
            using var tracker = new InstanceTracker();
            using var antiAfk = new AntiAfkService(tracker, settings);
            var operations = new OperationsViewModel(settings, antiAfk);
            var page = new ReconnectSettings { DataContext = new PageContext(operations) };

            using var host = new HwndSource(new HwndSourceParameters("AntiAfkKeyButtonTests") { Width = 700, Height = 800 })
            {
                RootVisual = page
            };
            page.UpdateLayout();
            test(page, operations, settings);
        });

        Assert.Null(failure);
    }

    private static KeyEventArgs Press(UIElement target, Key key)
    {
        var pressed = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        target.RaiseEvent(pressed);
        return pressed;
    }

    private sealed record PageContext(OperationsViewModel Operations);
}

internal sealed class RecordingSettings : ISettingsService
{
    public AppSettings Settings { get; } = new();
    public int SaveCount { get; private set; }
    public void Save() => SaveCount++;
}
