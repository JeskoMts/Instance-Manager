using InstanceManager.Models;

namespace InstanceManager.Storage;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void ScheduleSave() => Save();
    void Flush() { }
}
