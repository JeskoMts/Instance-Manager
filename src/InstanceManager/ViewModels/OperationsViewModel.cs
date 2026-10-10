using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;

namespace InstanceManager.ViewModels;

public partial class OperationsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly AntiAfkService _antiAfk;
    private bool _loading = true;
    public event Action? AvatarPreferenceChanged;
    public event Action<string, bool>? MessagePosted;

    public OperationsViewModel(ISettingsService settings, AntiAfkService antiAfk)
    {
        _settings = settings; _antiAfk = antiAfk;
        AppSettings s = settings.Settings;
        antiAfkEnabled = s.AntiAfkEnabled; antiAfkIntervalMinutes = s.AntiAfkIntervalMinutes;
        antiAfkKey = s.AntiAfkKey; antiAfkReturnFocus = s.AntiAfkReturnFocus;
        liveAvatars = s.LiveAvatars;
        _loading = false;
    }

    [ObservableProperty] private bool antiAfkEnabled;
    [ObservableProperty] private int antiAfkIntervalMinutes;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AntiAfkKeyLabel))]
    private string antiAfkKey = AntiAfkKeys.Default;
    [ObservableProperty] private bool antiAfkReturnFocus;
    [ObservableProperty] private bool liveAvatars;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AntiAfkKeyLabel))]
    private bool isCapturingAntiAfkKey;

    public string AntiAfkKeyLabel => IsCapturingAntiAfkKey ? "Press a key…" : AntiAfkKeys.Format(AntiAfkKey);

    private void Post(string message, bool error = false) => MessagePosted?.Invoke(message, error);

    private void Save(Action<AppSettings> change)
    {
        if (_loading) return;
        change(_settings.Settings); _settings.Save();
    }
    partial void OnAntiAfkEnabledChanged(bool value) { Save(s => s.AntiAfkEnabled = value); _antiAfk.Restart(); }
    partial void OnAntiAfkIntervalMinutesChanged(int value)
    {
        int normalized = Math.Clamp(value, 1, 60);
        if (value != normalized) { AntiAfkIntervalMinutes = normalized; return; }
        Save(s => s.AntiAfkIntervalMinutes = value); _antiAfk.Restart();
    }
    partial void OnAntiAfkKeyChanged(string value) => Save(s => s.AntiAfkKey = value);
    partial void OnAntiAfkReturnFocusChanged(bool value) => Save(s => s.AntiAfkReturnFocus = value);
    partial void OnLiveAvatarsChanged(bool value) { Save(s => s.LiveAvatars = value); AvatarPreferenceChanged?.Invoke(); }

    public void Start() => _antiAfk.Restart();
    public void Stop() => _antiAfk.Stop();

    [RelayCommand] private void BeginAntiAfkKeyCapture() => IsCapturingAntiAfkKey = true;

    public void CancelAntiAfkKeyCapture() => IsCapturingAntiAfkKey = false;

    public void CaptureAntiAfkKey(Key key)
    {
        if (!IsCapturingAntiAfkKey) return;
        IsCapturingAntiAfkKey = false;
        if (key == Key.Escape) return;
        if (!AntiAfkKeys.IsAllowed(key))
        {
            Post("That key can't be used, because it would act outside the Roblox window. Pick another one.", error: true);
            return;
        }
        AntiAfkKey = key.ToString();
    }

    [RelayCommand] private async Task SendAntiAfkNow()
    {
        int sent = await _antiAfk.SendNowAsync();
        Post(sent switch
        {
            0 => "No running Roblox window could be reached.",
            1 => "Sent a key press to 1 Roblox window.",
            _ => $"Sent a key press to {sent} Roblox windows."
        }, error: sent == 0);
    }
}
