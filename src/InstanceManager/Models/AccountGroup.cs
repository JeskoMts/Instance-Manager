using System;

namespace InstanceManager.Models;

public sealed class AccountGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New group";

    public string ColorHex { get; set; } = "#4C8DFF";

    public int SortOrder { get; set; }

    public bool IsExpanded { get; set; } = true;
}
