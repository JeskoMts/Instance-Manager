using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InstanceManager.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationStop
{
    Never,
    AfterMinutes,
    AtTime
}

public sealed class Automation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New automation";

    public List<Guid> AccountIds { get; set; } = new();

    public JoinMode Mode { get; set; } = JoinMode.PublicByLink;

    public string Target { get; set; } = string.Empty;

    public string? TargetName { get; set; }

    public bool ScheduleEnabled { get; set; }

    public bool StartOnSchedule { get; set; } = true;

    public string StartTime { get; set; } = "18:00";

    public List<DayOfWeek> Days { get; set; } = new();

    public AutomationStop Stop { get; set; } = AutomationStop.Never;

    public int StopAfterMinutes { get; set; } = 60;

    public string StopTime { get; set; } = "23:00";

    public DateTime? LastStartLocal { get; set; }

    public DateTime? LastStopLocal { get; set; }
}

public sealed class AutomationData
{
    public List<Automation> Automations { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LaunchTemplate>? Templates { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LaunchSchedule>? Schedules { get; set; }
}

public sealed class LaunchTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Template";
    public List<Guid> AccountIds { get; set; } = new();
    public JoinMode Mode { get; set; } = JoinMode.PublicByLink;
    public string Target { get; set; } = string.Empty;
}

public enum ScheduleAction
{
    Start,
    Close
}

public enum ScheduleTarget
{
    Template,
    Account
}

public sealed class LaunchSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Schedule";
    public bool Enabled { get; set; } = true;
    public ScheduleAction Action { get; set; }
    public ScheduleTarget Target { get; set; } = ScheduleTarget.Template;
    public Guid? TemplateId { get; set; }
    public Guid? AccountId { get; set; }
    public string Place { get; set; } = string.Empty;
    public string Time { get; set; } = "09:00";
    public List<DayOfWeek> Days { get; set; } = new();
    public int CloseAfterMinutes { get; set; }
    public DateTime? LastRunLocal { get; set; }
}
