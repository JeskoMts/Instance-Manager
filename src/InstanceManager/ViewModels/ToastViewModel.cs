using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InstanceManager.ViewModels;

public sealed partial class ToastViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required NotificationKind Kind { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }

    public int LifetimeMs { get; init; }

    public Action? UndoAction { get; init; }
    public bool CanUndo => UndoAction != null;

    [ObservableProperty] private bool isClosing;

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;
    public string Time => CreatedAt.ToString("HH:mm");
}
