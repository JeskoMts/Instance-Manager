using System;
using System.IO;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class RobloxExecutableValidatorTests
{
    [Fact]
    public void TryValidate_AcceptsTrustedRobloxNamedFileInsideRoot()
    {
        using var fixture = new ExecutableFixture();
        var validator = new RobloxExecutableValidator(_ => true);

        bool valid = validator.TryValidate(fixture.Root, fixture.PlayerPath, out string error);

        Assert.True(valid, error);
    }

    [Fact]
    public void TryValidate_RejectsCandidateOutsideConfiguredRoot()
    {
        using var fixture = new ExecutableFixture();
        string outside = Path.Combine(fixture.Parent, "RobloxPlayerBeta.exe");
        File.WriteAllText(outside, "stub");
        var validator = new RobloxExecutableValidator(_ => true);

        bool valid = validator.TryValidate(fixture.Root, outside, out _);

        Assert.False(valid);
    }

    [Fact]
    public void TryValidate_RejectsUnexpectedFilename()
    {
        using var fixture = new ExecutableFixture();
        string renamed = Path.Combine(Path.GetDirectoryName(fixture.PlayerPath)!, "RobloxPlayerLauncher.exe");
        File.WriteAllText(renamed, "stub");
        var validator = new RobloxExecutableValidator(_ => true);

        bool valid = validator.TryValidate(fixture.Root, renamed, out _);

        Assert.False(valid);
    }

    [Fact]
    public void TryValidate_RejectsFileWhoseAuthenticodeTrustFails()
    {
        using var fixture = new ExecutableFixture();
        var validator = new RobloxExecutableValidator(_ => false);

        bool valid = validator.TryValidate(fixture.Root, fixture.PlayerPath, out _);

        Assert.False(valid);
    }

    private sealed class ExecutableFixture : IDisposable
    {
        public ExecutableFixture()
        {
            Parent = Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
            Root = Path.Combine(Parent, "Versions");
            string version = Path.Combine(Root, "version-test");
            Directory.CreateDirectory(version);
            PlayerPath = Path.Combine(version, "RobloxPlayerBeta.exe");
            File.WriteAllText(PlayerPath, "stub");
        }

        public string Parent { get; }
        public string Root { get; }
        public string PlayerPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Parent))
                Directory.Delete(Parent, recursive: true);
        }
    }
}
