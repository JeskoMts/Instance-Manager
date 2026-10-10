using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using InstanceManager.Models;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AutomationRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"im-automations-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void LegacyTemplatesAndSchedules_BecomeAutomations()
    {
        Guid alpha = Guid.NewGuid(), beta = Guid.NewGuid();
        var farm = new LaunchTemplate { Name = "Farm", AccountIds = new List<Guid> { alpha, beta }, Mode = JoinMode.PublicByLink, Target = "606849621" };
        var legacy = new AutomationData
        {
            Templates = new List<LaunchTemplate> { farm },
            Schedules = new List<LaunchSchedule>
            {
                new() { Name = "Evening", Target = ScheduleTarget.Template, TemplateId = farm.Id, Time = "18:00",
                        Days = new List<DayOfWeek> { DayOfWeek.Friday }, CloseAfterMinutes = 90 },
                new() { Name = "Night", Action = ScheduleAction.Close, Target = ScheduleTarget.Template, TemplateId = farm.Id, Time = "23:30" },
                new() { Name = "Solo", Target = ScheduleTarget.Account, AccountId = alpha, Time = "07:00", Enabled = false,
                        Place = "https://www.roblox.com/games/start?placeId=1&gameInstanceId=0f0b0a0e-1111-2222-3333-444455556666" }
            }
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(legacy));

        var repository = new AutomationRepository(_path);

        Assert.Equal(new[] { "Farm", "Night", "Solo" }, repository.Automations.Select(a => a.Name));

        Automation evening = repository.Automations[0];
        Assert.Equal(farm.Id, evening.Id);
        Assert.Equal(new[] { alpha, beta }, evening.AccountIds);
        Assert.True(evening.ScheduleEnabled);
        Assert.Equal("18:00", evening.StartTime);
        Assert.Equal(new[] { DayOfWeek.Friday }, evening.Days);
        Assert.Equal(AutomationStop.AfterMinutes, evening.Stop);
        Assert.Equal(90, evening.StopAfterMinutes);

        Automation night = repository.Automations[1];
        Assert.False(night.StartOnSchedule);
        Assert.Equal(AutomationStop.AtTime, night.Stop);
        Assert.Equal("23:30", night.StopTime);
        Assert.Equal(new[] { alpha, beta }, night.AccountIds);

        Automation solo = repository.Automations[2];
        Assert.Equal(JoinMode.PrivateByJobId, solo.Mode);
        Assert.Equal(new[] { alpha }, solo.AccountIds);
        Assert.False(solo.ScheduleEnabled);

        string saved = File.ReadAllText(_path);
        Assert.DoesNotContain("\"Templates\"", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Schedules\"", saved, StringComparison.Ordinal);
        Assert.Equal(3, new AutomationRepository(_path).Automations.Count);
    }

    [Fact]
    public void InsertAndRemove_KeepTheOrderAndPersist()
    {
        var repository = new AutomationRepository(_path);
        var first = new Automation { Name = "First" };
        var second = new Automation { Name = "Second" };

        repository.Insert(0, second);
        repository.Insert(0, first);
        repository.Remove(second);
        repository.Insert(5, second);

        Assert.Equal(new[] { "First", "Second" }, new AutomationRepository(_path).Automations.Select(a => a.Name));
    }

    [Fact]
    public void Load_RepairsMissingAndOutOfRangeValues()
    {
        File.WriteAllText(_path, "{\"Automations\":[{\"Name\":\"  \",\"AccountIds\":null,\"Days\":[1,1],\"StopAfterMinutes\":0}]}");

        Automation automation = Assert.Single(new AutomationRepository(_path).Automations);

        Assert.Equal("Automation", automation.Name);
        Assert.Empty(automation.AccountIds);
        Assert.Equal(new[] { DayOfWeek.Monday }, automation.Days);
        Assert.Equal(1, automation.StopAfterMinutes);
    }
}
