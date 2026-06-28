using System;
using System.IO;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AutoReconnectLogSecurityTests
{
    [Fact]
    public void SanitizeField_RemovesLineAndControlCharacterInjection()
    {
        string sanitized = AutoReconnectLog.SanitizeField("victim\r\nFAKE  success\t\u0001");

        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain('\t', sanitized);
        Assert.DoesNotContain('\u0001', sanitized);
        Assert.Contains("victim", sanitized);
    }

    [Fact]
    public void Write_WhenLogExceedsLimit_RotatesBeforeAppending()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "auto-reconnect.log");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.SetLength(AutoReconnectLog.MaxLogBytes + 1L);

            var log = new AutoReconnectLog(path);
            log.Write("safe");

            Assert.True(new FileInfo(path).Length < AutoReconnectLog.MaxLogBytes);
            Assert.True(File.Exists(path + ".1"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
