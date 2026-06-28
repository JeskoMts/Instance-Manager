using InstanceManager.Models;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InstanceManager.ViewModels;

public partial class NotificationMuteOption : ObservableObject
{
    private readonly ISettingsService _service;

    public NotificationMuteOption(ISettingsService service, NotificationId id, string label)
    {
        _service = service;
        Id = id;
        Label = label;
        isMuted = service.Settings.MutedNotifications.Contains(id);
    }

    public NotificationId Id { get; }
    public string Label { get; }

    [ObservableProperty] private bool isMuted;

    partial void OnIsMutedChanged(bool value)
    {
        var muted = _service.Settings.MutedNotifications;
        if (value)
        {
            if (!muted.Contains(Id))
                muted.Add(Id);
        }
        else
        {
            muted.RemoveAll(x => x == Id);
        }
        _service.Save();
    }
}
