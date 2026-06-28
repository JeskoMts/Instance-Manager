using System.Text.Json.Serialization;

namespace InstanceManager.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationId
{
    GroupEmpty,
    AccountUpdated,
    AccountAdded,
    AccountRemoved,
    GroupUpdated,
    GroupCreated,
    GroupDeleted,
    NothingToLaunch,
    InvalidLaunchTarget,
    RobloxNotFound,
    MultiInstanceUnavailable,
    LaunchComplete,
    GameSelected,
    FavoriteApplied,
    FavoriteNotSaved,
    FavoriteSaved,
    FavoriteUpdated,
    FavoriteDeleted,
    AccountRenamed,
    AccountGroupsUpdated,
    GroupRenamed,
    ThemeApplied,
    ThemeCreated,
    ThemeUpdated,
    ThemeDeleted,
    ThemeExported,
    ThemeImported,
    ThemeImportFailed
}
