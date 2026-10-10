using System;
using System.Collections.Generic;
using System.Globalization;
using InstanceManager.Models;

namespace InstanceManager.Services;

public static class ScheduleClock
{
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return TimeSpan.TryParseExact(text.Trim(), new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out time)
               && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
    }

    public static bool RunsOn(List<DayOfWeek> days, DayOfWeek day) =>
        days.Count == 0 || days.Contains(day);

    public static bool StopsNextDay(Automation automation) =>
        automation.StartOnSchedule
        && TryParseTime(automation.StartTime, out TimeSpan start)
        && TryParseTime(automation.StopTime, out TimeSpan stop)
        && stop <= start;

    public static DateTime? DueStart(Automation automation, DateTime now)
    {
        if (!automation.ScheduleEnabled || !automation.StartOnSchedule || !TryParseTime(automation.StartTime, out TimeSpan time))
            return null;
        return DueOccurrence(now, time, automation.Days, dayShift: 0, automation.LastStartLocal);
    }

    public static DateTime? DueStop(Automation automation, DateTime now)
    {
        if (!automation.ScheduleEnabled)
            return null;

        switch (automation.Stop)
        {
            case AutomationStop.AtTime when TryParseTime(automation.StopTime, out TimeSpan time):
                return DueOccurrence(now, time, automation.Days, StopsNextDay(automation) ? 1 : 0, automation.LastStopLocal);

            case AutomationStop.AfterMinutes when automation.LastStartLocal is DateTime started:
                DateTime due = started.AddMinutes(automation.StopAfterMinutes);
                if (now < due || now - due > Grace)
                    return null;
                if (automation.LastStopLocal is DateTime last && last >= due)
                    return null;
                return due;

            default:
                return null;
        }
    }

    public static DateTime? NextStart(Automation automation, DateTime now) =>
        automation.ScheduleEnabled && automation.StartOnSchedule && TryParseTime(automation.StartTime, out TimeSpan time)
            ? Next(now, time, automation.Days, dayShift: 0)
            : null;

    public static DateTime? NextStop(Automation automation, DateTime now)
    {
        if (!automation.ScheduleEnabled)
            return null;

        switch (automation.Stop)
        {
            case AutomationStop.AtTime when TryParseTime(automation.StopTime, out TimeSpan time):
                return Next(now, time, automation.Days, StopsNextDay(automation) ? 1 : 0);

            case AutomationStop.AfterMinutes:
                if (automation.LastStartLocal is DateTime started)
                {
                    DateTime pending = started.AddMinutes(automation.StopAfterMinutes);
                    bool alreadyStopped = automation.LastStopLocal is DateTime last && last >= pending;
                    if (pending > now && !alreadyStopped)
                        return pending;
                }
                return NextStart(automation, now)?.AddMinutes(automation.StopAfterMinutes);

            default:
                return null;
        }
    }

    public static string Describe(DateTime next, DateTime now)
    {
        string clock = next.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (next.Date == now.Date)
            return "today at " + clock;
        if (next.Date == now.Date.AddDays(1))
            return "tomorrow at " + clock;
        return next.ToString("ddd", CultureInfo.InvariantCulture) + " at " + clock;
    }

    private static DateTime? DueOccurrence(DateTime now, TimeSpan time, List<DayOfWeek> days, int dayShift, DateTime? lastRun)
    {
        for (int daysBack = 0; daysBack <= 1; daysBack++)
        {
            DateTime occurrence = now.Date.AddDays(-daysBack) + time;
            if (occurrence > now || !RunsOn(days, occurrence.AddDays(-dayShift).DayOfWeek))
                continue;
            if (now - occurrence > Grace)
                return null;
            if (lastRun is DateTime last && last >= occurrence)
                return null;
            return occurrence;
        }
        return null;
    }

    private static DateTime? Next(DateTime now, TimeSpan time, List<DayOfWeek> days, int dayShift)
    {
        for (int day = 0; day <= 8; day++)
        {
            DateTime candidate = now.Date.AddDays(day) + time;
            if (candidate <= now || !RunsOn(days, candidate.AddDays(-dayShift).DayOfWeek))
                continue;
            return candidate;
        }
        return null;
    }
}
