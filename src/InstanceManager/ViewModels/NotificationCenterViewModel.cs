using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class NotificationCenterViewModel : ObservableObject
{
    public static readonly TimeSpan ToastLifetime = TimeSpan.FromSeconds(4.5);

    private const int MaxToasts = 3;
    private const int MaxHistory = 50;

    private const int CloseAnimationMs = 100;

    private readonly ISettingsService? _settings;

    public NotificationCenterViewModel(ISettingsService? settings = null) => _settings = settings;

    public ObservableCollection<ToastViewModel> Items { get; } = new();

    public ObservableCollection<ToastViewModel> History { get; } = new();

    [ObservableProperty] private int unreadCount;
    [ObservableProperty] private bool isCenterOpen;

    public bool HasUnread => UnreadCount > 0;
    public bool HasHistory => History.Count > 0;
    public string UnreadBadge => UnreadCount > 9 ? "9+" : UnreadCount.ToString();

    public void Show(NotificationId id, NotificationKind kind, string title, string message, TimeSpan? lifetime = null, Action? undoAction = null)
    {
        Guid toastId = Guid.NewGuid();
        AddToHistory(kind, title, message, undoAction, toastId);

        if (_settings?.Settings.IsNotificationMuted(id) == true)
            return;

        if (Items.Any(item => item.Kind == kind && item.Title == title && item.Message == message))
            return;

        TimeSpan duration = lifetime ?? ResolveLifetime();
        var toast = new ToastViewModel
        {
            Id = toastId,
            Kind = kind,
            Title = title,
            Message = message,
            UndoAction = undoAction,
            LifetimeMs = (int)duration.TotalMilliseconds
        };
        Items.Insert(0, toast);
        while (Items.Count > MaxToasts)
            Items.RemoveAt(Items.Count - 1);

        if (duration > TimeSpan.Zero)
            _ = DismissLaterAsync(toast, duration);
    }

    private TimeSpan ResolveLifetime()
    {
        int ms = _settings?.Settings.ToastDurationMs ?? (int)ToastLifetime.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(ms);
    }

    private void AddToHistory(NotificationKind kind, string title, string message, Action? undoAction = null, Guid? sharedId = null)
    {
        if (History.FirstOrDefault() is { } latest && latest.Kind == kind && latest.Title == title && latest.Message == message)
            return;

        History.Insert(0, new ToastViewModel { Id = sharedId ?? Guid.NewGuid(), Kind = kind, Title = title, Message = message, UndoAction = undoAction });
        while (History.Count > MaxHistory)
            History.RemoveAt(History.Count - 1);

        if (!IsCenterOpen)
            UnreadCount++;
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void Dismiss(ToastViewModel? toast)
    {
        if (toast != null)
            _ = CloseAsync(toast);
    }

    [RelayCommand]
    private void ToggleCenter() => IsCenterOpen = !IsCenterOpen;

    [RelayCommand]
    private void RemoveFromHistory(ToastViewModel? toast)
    {
        if (toast != null && History.Remove(toast))
            OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void Undo(ToastViewModel? toast)
    {
        if (toast?.UndoAction != null)
        {
            toast.UndoAction.Invoke();

            var itemsToRemove = Items.Where(t => t.Id == toast.Id).ToList();
            foreach (var item in itemsToRemove)
                _ = CloseAsync(item);

            var historyToRemove = History.Where(t => t.Id == toast.Id).ToList();
            foreach (var item in historyToRemove)
                History.Remove(item);

            if (historyToRemove.Count > 0)
                OnPropertyChanged(nameof(HasHistory));
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
        IsCenterOpen = false;
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(UnreadBadge));
    }

    partial void OnIsCenterOpenChanged(bool value)
    {
        if (value)
            UnreadCount = 0;
    }

    private async Task DismissLaterAsync(ToastViewModel toast, TimeSpan duration)
    {
        await Task.Delay(duration);
        await CloseAsync(toast);
    }

    private async Task CloseAsync(ToastViewModel toast)
    {
        if (toast.IsClosing || !Items.Contains(toast))
            return;
        toast.IsClosing = true;
        await Task.Delay(CloseAnimationMs);
        Items.Remove(toast);
    }
}
