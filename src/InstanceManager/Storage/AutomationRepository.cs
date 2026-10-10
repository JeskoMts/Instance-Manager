using System;
using System.Collections.Generic;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Storage;

public interface IAutomationRepository
{
    IReadOnlyList<Automation> Automations { get; }

    void Insert(int index, Automation automation);

    void Remove(Automation automation);

    void Save();
}

public sealed class AutomationRepository : IAutomationRepository
{
    private readonly JsonFileStore _file;
    private readonly AutomationData _data;

    public AutomationRepository() : this(AppPaths.AutomationFile)
    {
        AppPaths.EnsureDataDirectory();
    }

    internal AutomationRepository(string path)
    {
        _file = new JsonFileStore(path);
        _data = _file.Load(() => new AutomationData());
        _data.Automations ??= new List<Automation>();
        _data.Automations.RemoveAll(a => a == null);

        bool migrated = MigrateLegacy(_data);
        foreach (Automation automation in _data.Automations)
            Normalize(automation);
        if (migrated)
            Save();
    }

    public IReadOnlyList<Automation> Automations => _data.Automations;

    public void Insert(int index, Automation automation)
    {
        _data.Automations.RemoveAll(a => a.Id == automation.Id);
        _data.Automations.Insert(Math.Clamp(index, 0, _data.Automations.Count), automation);
        Save();
    }

    public void Remove(Automation automation)
    {
        _data.Automations.RemoveAll(a => a.Id == automation.Id);
        Save();
    }

    public void Save() => _file.Save(_data);

    private static void Normalize(Automation automation)
    {
        automation.Name = string.IsNullOrWhiteSpace(automation.Name) ? "Automation" : automation.Name.Trim();
        automation.AccountIds = (automation.AccountIds ?? new List<Guid>()).Distinct().ToList();
        automation.Days = (automation.Days ?? new List<DayOfWeek>()).Distinct().ToList();
        automation.Target ??= string.Empty;
        automation.StartTime ??= "18:00";
        automation.StopTime ??= "23:00";
        automation.StopAfterMinutes = Math.Clamp(automation.StopAfterMinutes, 1, 24 * 60);
        if (!Enum.IsDefined(automation.Stop))
            automation.Stop = AutomationStop.Never;
    }

    internal static bool MigrateLegacy(AutomationData data)
    {
        if (data.Templates is not { Count: > 0 } && data.Schedules is not { Count: > 0 })
        {
            bool hadEmptyLists = data.Templates != null || data.Schedules != null;
            data.Templates = null;
            data.Schedules = null;
            return hadEmptyLists;
        }

        var byTemplate = new Dictionary<Guid, Automation>();
        foreach (LaunchTemplate template in data.Templates?.Where(t => t != null) ?? Enumerable.Empty<LaunchTemplate>())
        {
            var automation = new Automation
            {
                Id = template.Id,
                Name = template.Name,
                AccountIds = template.AccountIds?.ToList() ?? new List<Guid>(),
                Mode = template.Mode,
                Target = template.Target ?? string.Empty
            };
            byTemplate[template.Id] = automation;
            data.Automations.Add(automation);
        }

        var withSchedule = new HashSet<Automation>();
        foreach (LaunchSchedule schedule in data.Schedules?.Where(s => s != null) ?? Enumerable.Empty<LaunchSchedule>())
        {
            Automation target;
            if (schedule.Target == ScheduleTarget.Template && schedule.TemplateId is Guid templateId
                && byTemplate.TryGetValue(templateId, out Automation? owner))
            {
                target = withSchedule.Contains(owner) ? Copy(owner, schedule.Name) : owner;
            }
            else
            {
                string place = schedule.Place ?? string.Empty;
                target = new Automation
                {
                    Name = schedule.Name,
                    AccountIds = schedule.AccountId is Guid id ? new List<Guid> { id } : new List<Guid>(),
                    Mode = string.IsNullOrWhiteSpace(place) ? JoinMode.Home
                        : GameLinkParser.LooksLikeServerLink(place) ? JoinMode.PrivateByJobId : JoinMode.PublicByLink,
                    Target = place
                };
            }

            if (!data.Automations.Contains(target))
                data.Automations.Add(target);
            withSchedule.Add(target);

            target.ScheduleEnabled = schedule.Enabled;
            target.Days = schedule.Days?.ToList() ?? new List<DayOfWeek>();
            if (schedule.Action == ScheduleAction.Close)
            {
                target.StartOnSchedule = false;
                target.Stop = AutomationStop.AtTime;
                target.StopTime = schedule.Time;
                target.LastStopLocal = schedule.LastRunLocal;
            }
            else
            {
                target.StartOnSchedule = true;
                target.StartTime = schedule.Time;
                target.Stop = schedule.CloseAfterMinutes > 0 ? AutomationStop.AfterMinutes : AutomationStop.Never;
                if (schedule.CloseAfterMinutes > 0)
                    target.StopAfterMinutes = schedule.CloseAfterMinutes;
                target.LastStartLocal = schedule.LastRunLocal;
            }
        }

        data.Templates = null;
        data.Schedules = null;
        return true;
    }

    private static Automation Copy(Automation source, string name) => new()
    {
        Name = name,
        AccountIds = source.AccountIds.ToList(),
        Mode = source.Mode,
        Target = source.Target,
        TargetName = source.TargetName
    };
}
