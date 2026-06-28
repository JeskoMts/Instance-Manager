using System.IO;
using System.Linq;
using System.Net.Http;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public class VersionServiceTests
{
    [Theory]
    [InlineData("0.726.0.7261140", 7261140)]
    [InlineData("0.660.0.6600446", 6600446)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("1.2.3", 3)]
    public void ExtractBuildNumber_ParsesLastComponent(string version, long expected)
        => Assert.Equal(expected, VersionService.ExtractBuildNumber(version));

    [Theory]
    [InlineData("0, 726, 0, 7261140", "0.726.0.7261140")]
    [InlineData("0,726,0,7261140", "0.726.0.7261140")]
    [InlineData("0.726.0.7261140", "0.726.0.7261140")]
    [InlineData("  1.2.3  ", "1.2.3")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeVersion_ConvertsCommaFormToDotted(string? input, string expected)
        => Assert.Equal(expected, VersionService.NormalizeVersion(input));

    [Fact]
    public void Enumerate_FindsOnlyFoldersWithPlayerExe()
    {
        string root = Path.Combine(Path.GetTempPath(), "InstanceManagerTest_" + Path.GetRandomFileName());
        try
        {
            string withExe = Path.Combine(root, "version-aaa");
            string withoutExe = Path.Combine(root, "version-bbb");
            Directory.CreateDirectory(withExe);
            Directory.CreateDirectory(withoutExe);
            File.WriteAllText(Path.Combine(withExe, "RobloxPlayerBeta.exe"), "stub");
            File.WriteAllText(Path.Combine(withoutExe, "SomethingElse.txt"), "x");

            var service = new VersionService(new HttpClient(), new AllowAllExecutableValidator());
            var versions = service.Enumerate(root);

            Assert.Single(versions);
            Assert.Equal("version-aaa", versions[0].VersionGuid);
            Assert.EndsWith("RobloxPlayerBeta.exe", versions[0].PlayerExePath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_MissingRoot_ReturnsEmpty()
    {
        var service = new VersionService(new HttpClient());
        var versions = service.Enumerate(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName()));
        Assert.Empty(versions);
    }

    [Theory]
    [InlineData(@"\\attacker.example\share\Versions")]
    [InlineData(@"//attacker.example/share/Versions")]
    [InlineData(@"relative\Versions")]
    public void TryNormalizeFixedLocalDirectory_RejectsNetworkAndRelativePaths(string path)
    {
        Assert.False(LocalPathPolicy.TryNormalizeFixedLocalDirectory(path, out _));
    }

    [Fact]
    public void Enumerate_UnsafeNetworkRoot_IsRejectedBeforeExecutableValidation()
    {
        var validator = new CountingExecutableValidator();
        var service = new VersionService(new HttpClient(), validator);

        var versions = service.Enumerate(@"\\attacker.example\share\Versions");

        Assert.Empty(versions);
        Assert.Equal(0, validator.Calls);
    }

    private sealed class AllowAllExecutableValidator : IRobloxExecutableValidator
    {
        public bool TryValidate(string versionsRoot, string executablePath, out string error)
        {
            error = string.Empty;
            return true;
        }
    }

    private sealed class CountingExecutableValidator : IRobloxExecutableValidator
    {
        public int Calls { get; private set; }

        public bool TryValidate(string versionsRoot, string executablePath, out string error)
        {
            Calls++;
            error = string.Empty;
            return true;
        }
    }
}
