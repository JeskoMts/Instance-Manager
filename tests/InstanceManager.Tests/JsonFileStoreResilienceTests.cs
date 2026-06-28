using System;
using System.IO;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class JsonFileStoreResilienceTests
{
    [Fact]
    public void Save_WhenTargetCannotBeWritten_DoesNotThrow()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "data.json");
            Directory.CreateDirectory(path);

            var store = new JsonFileStore(path);

            Exception? error = Record.Exception(() => store.Save(new { Value = 7 }));

            Assert.Null(error);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Save_AfterTransientFailure_StillWritesOnceTargetIsWritable()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "data.json");
            Directory.CreateDirectory(path);
            var store = new JsonFileStore(path);

            store.Save(new { Value = 7 });

            Directory.Delete(path);
            store.Save(new { Value = 7 });

            Assert.True(File.Exists(path));
            Assert.Contains("7", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Load_WhenFileExceedsSecurityLimit_ReturnsFallbackWithoutAllocatingPayload()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "data.json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(JsonFileStore.MaxFileBytes + 1L);
            }

            var store = new JsonFileStore(path);
            int value = store.Load(() => 42);

            Assert.Equal(42, value);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
