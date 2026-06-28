using System.Collections.Generic;

namespace InstanceManager.Models;

public sealed class AppSettings
{
    public const int LaunchDelayStepMs = 500;
    public const int MaxLaunchDelayMs = 30_000;
    public const int MinToastDurationMs = 500;
    public const int MaxToastDurationMs = 5000;
    public const int MinAutoReconnectAttempts = 1;
    public const int MaxAutoReconnectAttempts = 30;

    public const int UnlimitedAutoReconnectAttempts = MaxAutoReconnectAttempts + 1;
    public const string DefaultThemeId = "dark";

    public string? SelectedVersionGuid { get; set; }

    public System.Guid? PrimaryFavoriteId { get; set; }

    public JoinMode LastJoinMode { get; set; } = JoinMode.PublicByLink;

    public bool MultiInstanceEnabled { get; set; } = true;

    public int LaunchDelayMs { get; set; } = 1500;

    public string? VersionsPathOverride { get; set; }

    public string? LastTargetInput { get; set; }

    public string? LastJobIdInput { get; set; }

    public bool SwitchToAccountsOnGameSelect { get; set; }

    public System.Guid? LastSelectedFavoriteId { get; set; }

    public double WindowWidth { get; set; } = 980;
    public double WindowHeight { get; set; } = 660;

    public string ThemeId { get; set; } = DefaultThemeId;

    public List<string> ThemeOrder { get; set; } = new();

    public bool ConfirmBypassMaster { get; set; }
    public bool ConfirmBypassRemoveAccount { get; set; }
    public bool ConfirmBypassDeleteGroup { get; set; }
    public bool ConfirmBypassDeleteFavorite { get; set; }
    public bool ConfirmBypassStopAllInstances { get; set; }
    public bool ConfirmBypassClearAccountGroups { get; set; }
    public bool ConfirmBypassDeleteTheme { get; set; }

    public bool NotifyMuteMaster { get; set; }
    public List<NotificationId> MutedNotifications { get; set; } = new();
    public int ToastDurationMs { get; set; } = 4500;

    public bool AutoReconnectMaster { get; set; } = true;

    public bool AutoReconnectOnKickError { get; set; } = true;

    public bool AutoReconnectOnCrash { get; set; } = true;

    public int AutoReconnectMaxAttempts { get; set; } = 3;

    public bool? AutoRejoinMaster { get; set; }
    public bool? AutoRejoinOnKickError { get; set; }
    public bool? AutoRejoinOnCrash { get; set; }
    public int? AutoRejoinMaxAttempts { get; set; }
    public bool? AutoRejoinOnError { get; set; }
    public bool? AutoRejoinOnKick { get; set; }

    public bool IsConfirmBypassed(ConfirmAction action) => ConfirmBypassMaster || action switch
    {
        ConfirmAction.RemoveAccount => ConfirmBypassRemoveAccount,
        ConfirmAction.DeleteGroup => ConfirmBypassDeleteGroup,
        ConfirmAction.DeleteFavorite => ConfirmBypassDeleteFavorite,
        ConfirmAction.StopAllInstances => ConfirmBypassStopAllInstances,
        ConfirmAction.ClearAccountGroups => ConfirmBypassClearAccountGroups,
        ConfirmAction.DeleteTheme => ConfirmBypassDeleteTheme,
        _ => false
    };

    public bool IsNotificationMuted(NotificationId id) =>
        NotifyMuteMaster || MutedNotifications.Contains(id);

    public bool IsAutoReconnectEnabledFor(AutoReconnectTrigger trigger) => AutoReconnectMaster && trigger switch
    {
        AutoReconnectTrigger.Error => AutoReconnectOnKickError,
        AutoReconnectTrigger.Kick => AutoReconnectOnKickError,
        AutoReconnectTrigger.Crash => AutoReconnectOnCrash,
        _ => false
    };

    public static int NormalizeAutoReconnectAttempts(int value) =>
        System.Math.Clamp(value, MinAutoReconnectAttempts, UnlimitedAutoReconnectAttempts);

    public static bool IsUnlimitedAttempts(int value) => value >= UnlimitedAutoReconnectAttempts;

    public static int NormalizeLaunchDelay(int value)
    {
        int clamped = System.Math.Clamp(value, 0, MaxLaunchDelayMs);
        return (int)(System.Math.Round(
            clamped / (double)LaunchDelayStepMs,
            System.MidpointRounding.AwayFromZero) * LaunchDelayStepMs);
    }

    public bool Normalize()
    {
        bool changed = false;

        if (AutoRejoinMaster.HasValue)
        {
            AutoReconnectMaster = AutoRejoinMaster.Value;
            AutoRejoinMaster = null;
            changed = true;
        }

        if (AutoRejoinOnCrash.HasValue)
        {
            AutoReconnectOnCrash = AutoRejoinOnCrash.Value;
            AutoRejoinOnCrash = null;
            changed = true;
        }

        if (AutoRejoinMaxAttempts.HasValue)
        {
            AutoReconnectMaxAttempts = AutoRejoinMaxAttempts.Value;
            AutoRejoinMaxAttempts = null;
            changed = true;
        }

        if (AutoRejoinOnError.HasValue || AutoRejoinOnKick.HasValue)
        {
            AutoReconnectOnKickError = (AutoRejoinOnError ?? true) || (AutoRejoinOnKick ?? true);
            AutoRejoinOnError = null;
            AutoRejoinOnKick = null;
            AutoRejoinOnKickError = null;
            changed = true;
        }
        else if (AutoRejoinOnKickError.HasValue)
        {
            AutoReconnectOnKickError = AutoRejoinOnKickError.Value;
            AutoRejoinOnKickError = null;
            changed = true;
        }

        int normalizedDelay = NormalizeLaunchDelay(LaunchDelayMs);
        if (normalizedDelay != LaunchDelayMs)
        {
            LaunchDelayMs = normalizedDelay;
            changed = true;
        }

        int normalizedToast = System.Math.Clamp(ToastDurationMs, MinToastDurationMs, MaxToastDurationMs);
        if (normalizedToast != ToastDurationMs)
        {
            ToastDurationMs = normalizedToast;
            changed = true;
        }

        int normalizedAttempts = NormalizeAutoReconnectAttempts(AutoReconnectMaxAttempts);
        if (normalizedAttempts != AutoReconnectMaxAttempts)
        {
            AutoReconnectMaxAttempts = normalizedAttempts;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(ThemeId))
        {
            ThemeId = DefaultThemeId;
            changed = true;
        }

        if (ThemeOrder == null)
        {
            ThemeOrder = new();
            changed = true;
        }

        MutedNotifications ??= new();

        return changed;
    }
}
