using System;

namespace InstanceManager.Models;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Alias { get; set; }

    public Guid? GroupId { get; set; }

    public System.Collections.Generic.List<Guid> GroupIds { get; set; } = new();

    public string? Notes { get; set; }

    public string EncryptedCookie { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int SortOrder { get; set; }

    public long BrowserTrackerId { get; set; } = InstanceManager.Services.RobloxLauncher.GenerateBrowserTrackerId();

    public string? PreferredVersionGuid { get; set; }

    public bool AutoReconnectEnabled { get; set; } = true;

    public string DisplayLabel =>
        !string.IsNullOrWhiteSpace(Alias) ? Alias!
        : !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName
        : Username;

    public bool NormalizeGroupMemberships()
    {
        GroupIds ??= new System.Collections.Generic.List<Guid>();
        var seen = new System.Collections.Generic.HashSet<Guid>();
        var normalized = new System.Collections.Generic.List<Guid>();
        foreach (Guid id in GroupIds)
        {
            if (id != Guid.Empty && seen.Add(id))
                normalized.Add(id);
        }

        if (GroupId is Guid legacyId && legacyId != Guid.Empty && seen.Add(legacyId))
            normalized.Add(legacyId);

        bool changed = GroupId != null || GroupIds.Count != normalized.Count;
        if (!changed)
        {
            for (int i = 0; i < GroupIds.Count; i++)
            {
                if (GroupIds[i] != normalized[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        GroupIds = normalized;
        GroupId = null;
        return changed;
    }

    public bool BelongsTo(Guid groupId) => GroupIds.Contains(groupId);
}
