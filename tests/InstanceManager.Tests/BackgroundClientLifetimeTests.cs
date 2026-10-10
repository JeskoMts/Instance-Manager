using System.Diagnostics;
using System.IO;
using System.Reflection;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class BackgroundClientLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HarmlessNetworkDiagnostics_DoNotCloseHealthyClient(bool homeScreen)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, ".test-output", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var tracker = new InstanceTracker();
        using Process client = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 60 > nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        // Keep an independent handle: the tracker disposes its Process when it exits.
        using Process observer = Process.GetProcessById(client.Id);
        RobloxLogWatcher? watcher = null;
        using var reconnect = new AutoReconnectService(
            tracker, new ReconnectSettings(), new AutoReconnectLog(Path.Combine(directory, "reconnect.log")),
            started => watcher = new RobloxLogWatcher(directory, started));

        try
        {
            var account = new Account { UserId = 83, Username = "background-client" };
            tracker.Track(account.Id, client);
            reconnect.RegisterLaunch(account, homeScreen ? ServerTarget.Home() : ServerTarget.Public(111),
                new RobloxVersion { FolderPath = directory, PlayerExePath = "RobloxPlayerBeta.exe", VersionGuid = "test", FileVersion = "test" }, client);

            string log = Path.Combine(directory, $"0.726.0.7261140_{client.StartTime.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}_Player_background_last.log");
            File.WriteAllText(log,
                "[DFLog::HttpTraceError] CURLOPT_ERRORBUFFER: Failed to connect to 10.110.101.222 port 5052 after 21003 ms: Error\n" +
                "[DFLog::WebSocketTraceError] ws: 0 error: Connection closed\n" +
                "[DFLog::SignalRCoreError] ID: 1 Disconnected - connection lost\n");
            if (watcher != null)
                typeof(RobloxLogWatcher).GetMethod("Poll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(watcher, null);

            Assert.False(observer.WaitForExit(1600), "An unrelated network diagnostic closed a healthy Roblox client.");
            Assert.True(tracker.IsRunning(account.Id));
        }
        finally
        {
            reconnect.Dispose();
            if (!observer.HasExited)
                observer.Kill(entireProcessTree: true);
            observer.WaitForExit();
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ReconnectSettings : ISettingsService
    {
        public AppSettings Settings { get; } = new() { AutoReconnectMaster = true };
        public void Save() { }
    }
}
