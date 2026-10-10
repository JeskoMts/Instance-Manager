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

    [Fact]
    public void Save_AfterLoadWithUnchangedContent_LeavesFileUntouched()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "data.json");
            var value = new Sample { Name = "Jürgen 🎮", Items = new() { 1, 2, 3 } };
            new JsonFileStore(path).Save(value);
            var written = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, written);

            var store = new JsonFileStore(path);
            Sample loaded = store.Load(() => new Sample());
            store.Save(loaded);

            Assert.Equal(written, File.GetLastWriteTimeUtc(path));
            loaded.Items.Add(4);
            store.Save(loaded);
            Assert.NotEqual(written, File.GetLastWriteTimeUtc(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Load_ReadsFilesWithUtf8ByteOrderMarkAndRoundTripsText()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "data.json");
            File.WriteAllText(path, "{\"Name\":\"Jürgen 🎮\",\"Items\":[5]}", new System.Text.UTF8Encoding(true));

            Sample loaded = new JsonFileStore(path).Load(() => new Sample());

            Assert.Equal("Jürgen 🎮", loaded.Name);
            Assert.Equal(new[] { 5 }, loaded.Items);
            new JsonFileStore(path).Save(loaded);
            Assert.Equal("Jürgen 🎮", new JsonFileStore(path).Load(() => new Sample()).Name);
            Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Load_WhenFileIsNotJson_ReturnsFallback()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "data.json");
            File.WriteAllText(path, "not json");

            Assert.Equal("fallback", new JsonFileStore(path).Load(() => new Sample { Name = "fallback" }).Name);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class Sample
    {
        public string Name { get; set; } = string.Empty;
        public System.Collections.Generic.List<int> Items { get; set; } = new();
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
