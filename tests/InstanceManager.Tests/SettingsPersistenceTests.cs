using System;
using System.IO;
using InstanceManager.Services;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class SettingsPersistenceTests
{
    [Fact]
    public void ScheduleSave_DoesNotWriteUntilFlushed()
    {
        string directory = Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        try
        {
            using var service = new SettingsService(path, TimeSpan.FromMinutes(1));
            service.Settings.LaunchDelayMs = 42_000;

            service.ScheduleSave();

            Assert.False(File.Exists(path));
            service.Flush();
            Assert.Contains("42000", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_IdenticalJson_DoesNotReplaceFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "value.json");
        try
        {
            var store = new JsonFileStore(path);
            store.Save(new { Value = 7 });
            DateTime sentinel = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, sentinel);

            store.Save(new { Value = 7 });

            Assert.Equal(sentinel, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
