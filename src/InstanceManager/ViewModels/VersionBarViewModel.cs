using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class VersionBarViewModel : ObservableObject
{
    private readonly VersionService _versions;
    private readonly ISettingsService _settings;

    public VersionBarViewModel(VersionService versions, ISettingsService settings)
    {
        _versions = versions;
        _settings = settings;
    }

    public ObservableCollection<RobloxVersion> Versions { get; } = new();

    [ObservableProperty] private RobloxVersion? selectedVersion;

    partial void OnSelectedVersionChanged(RobloxVersion? value)
    {
        if (value != null)
        {
            _settings.Settings.SelectedVersionGuid = value.VersionGuid;
            _settings.Save();
        }
    }

    public Task InitializeAsync()
    {
        RefreshVersions();
        return Task.CompletedTask;
    }

    [RelayCommand]
    public void RefreshVersions()
    {
        var found = _versions.Enumerate(_settings.Settings.VersionsPathOverride);
        Versions.Clear();
        foreach (var v in found)
            Versions.Add(v);

        RobloxVersion? newest = Versions.FirstOrDefault();
        RobloxVersion? saved = Versions.FirstOrDefault(v => v.VersionGuid == _settings.Settings.SelectedVersionGuid);

        SelectedVersion = saved != null && newest != null && saved.BuildNumber >= newest.BuildNumber
            ? saved
            : newest;
    }
}
