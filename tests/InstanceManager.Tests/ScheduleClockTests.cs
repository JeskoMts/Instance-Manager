using System;
using System.Collections.Generic;
using InstanceManager.Models;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ScheduleClockTests
{
    private static readonly DateTime Monday = new(2026, 9, 28);

    [Theory]
    [InlineData("9:05", 9, 5)]
    [InlineData("09:05", 9, 5)]
    [InlineData(" 23:59 ", 23, 59)]
    [InlineData("0:00", 0, 0)]
    public void TryParseTime_AcceptsHoursAndMinutes(string text, int hours, int minutes)
    {
        Assert.True(ScheduleClock.TryParseTime(text, out TimeSpan time));
        Assert.Equal(new TimeSpan(hours, minutes, 0), time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24:00")]
    [InlineData("9")]
    [InlineData("nine")]
    [InlineData("12:60")]
    public void TryParseTime_RejectsInvalidTimes(string? text)
    {
        Assert.False(ScheduleClock.TryParseTime(text, out _));
    }

    [Fact]
    public void DueStart_FiresWithinTheGraceWindowOnly()
    {
        Assert.Equal(DayOfWeek.Monday, Monday.DayOfWeek);
        Automation automation = Scheduled("09:00");

        Assert.Null(ScheduleClock.DueStart(automation, Monday.AddHours(9).AddMinutes(-1)));
        Assert.Equal(Monday.AddHours(9), ScheduleClock.DueStart(automation, Monday.AddHours(9)));
        Assert.Equal(Monday.AddHours(9), ScheduleClock.DueStart(automation, Monday.AddHours(9).AddMinutes(10)));
        Assert.Null(ScheduleClock.DueStart(automation, Monday.AddHours(9).AddMinutes(11)));
    }

    [Fact]
    public void DueStart_DoesNotRepeatAfterItRan()
    {
        Automation automation = Scheduled("09:00");
        automation.LastStartLocal = Monday.AddHours(9).AddSeconds(5);

        Assert.Null(ScheduleClock.DueStart(automation, Monday.AddHours(9).AddMinutes(2)));
        Assert.Equal(Monday.AddDays(1).AddHours(9), ScheduleClock.DueStart(automation, Monday.AddDays(1).AddHours(9).AddMinutes(1)));
    }

    [Fact]
    public void DueStart_HonorsTheScheduleSwitchesAndChosenDays()
    {
        DateTime nine = Monday.AddHours(9).AddMinutes(1);

        Automation off = Scheduled("09:00");
        off.ScheduleEnabled = false;
        Automation noStart = Scheduled("09:00");
        noStart.StartOnSchedule = false;

        Assert.Null(ScheduleClock.DueStart(off, nine));
        Assert.Null(ScheduleClock.DueStart(noStart, nine));
        Assert.Null(ScheduleClock.DueStart(Scheduled("09:00", DayOfWeek.Tuesday), nine));
        Assert.NotNull(ScheduleClock.DueStart(Scheduled("09:00", DayOfWeek.Monday), nine));
    }

    [Fact]
    public void DueStart_CatchesARunJustBeforeMidnight()
    {
        Automation automation = Scheduled("23:58", DayOfWeek.Monday);

        Assert.Equal(Monday.AddHours(23).AddMinutes(58), ScheduleClock.DueStart(automation, Monday.AddDays(1).AddMinutes(3)));
    }

    [Fact]
    public void DueStop_AtTime_BelongsToTheNextDayWhenItIsBeforeTheStart()
    {
        Automation automation = Scheduled("22:00", DayOfWeek.Monday);
        automation.Stop = AutomationStop.AtTime;
        automation.StopTime = "02:00";

        Assert.True(ScheduleClock.StopsNextDay(automation));
        Assert.Null(ScheduleClock.DueStop(automation, Monday.AddHours(2)));
        Assert.Equal(Monday.AddDays(1).AddHours(2), ScheduleClock.DueStop(automation, Monday.AddDays(1).AddHours(2).AddMinutes(1)));
        Assert.Equal(Monday.AddDays(1).AddHours(2), ScheduleClock.NextStop(automation, Monday.AddHours(12)));
    }

    [Fact]
    public void DueStop_AfterMinutes_CountsFromTheLastStart()
    {
        Automation automation = Scheduled("09:00");
        automation.Stop = AutomationStop.AfterMinutes;
        automation.StopAfterMinutes = 45;

        Assert.Null(ScheduleClock.DueStop(automation, Monday.AddHours(10)));

        automation.LastStartLocal = Monday.AddHours(9);
        Assert.Null(ScheduleClock.DueStop(automation, Monday.AddHours(9).AddMinutes(44)));
        Assert.Equal(Monday.AddHours(9).AddMinutes(45), ScheduleClock.DueStop(automation, Monday.AddHours(9).AddMinutes(46)));
        Assert.Equal(Monday.AddHours(9).AddMinutes(45), ScheduleClock.NextStop(automation, Monday.AddHours(9).AddMinutes(10)));

        automation.LastStopLocal = Monday.AddHours(9).AddMinutes(46);
        Assert.Null(ScheduleClock.DueStop(automation, Monday.AddHours(9).AddMinutes(47)));
        Assert.Equal(Monday.AddDays(1).AddHours(9).AddMinutes(45), ScheduleClock.NextStop(automation, Monday.AddHours(10)));
    }

    [Fact]
    public void NextStart_SkipsToTheNextChosenDay()
    {
        DateTime ten = Monday.AddHours(10);

        Assert.Equal(Monday.AddDays(1).AddHours(9), ScheduleClock.NextStart(Scheduled("09:00"), ten));
        Assert.Equal(Monday.AddDays(2).AddHours(9), ScheduleClock.NextStart(Scheduled("09:00", DayOfWeek.Wednesday), ten));
        Assert.Equal(Monday.AddDays(7).AddHours(9), ScheduleClock.NextStart(Scheduled("09:00", DayOfWeek.Monday), ten));
        Assert.Null(ScheduleClock.NextStart(Scheduled("later"), ten));
    }

    [Fact]
    public void Describe_SaysTodayTomorrowOrTheWeekday()
    {
        DateTime now = Monday.AddHours(8);

        Assert.Equal("today at 09:30", ScheduleClock.Describe(Monday.AddHours(9.5), now));
        Assert.Equal("tomorrow at 07:00", ScheduleClock.Describe(Monday.AddDays(1).AddHours(7), now));
        Assert.Equal("Thu at 18:15", ScheduleClock.Describe(Monday.AddDays(3).AddHours(18.25), now));
    }

    private static Automation Scheduled(string start, params DayOfWeek[] days) => new()
    {
        ScheduleEnabled = true,
        StartTime = start,
        Days = new List<DayOfWeek>(days)
    };
}
