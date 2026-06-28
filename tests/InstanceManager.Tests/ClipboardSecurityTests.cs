using System;
using System.IO;
using System.Linq;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ClipboardSecurityTests
{
    [Theory]
    [InlineData(0, 64, true)]
    [InlineData(128, 64, true)]
    [InlineData(130, 64, false)]
    public void IsUnicodeTextSizeAllowed_BoundsAllocationBeforeReading(
        ulong bytes,
        int maxCharacters,
        bool expected)
    {
        Assert.Equal(
            expected,
            BoundedClipboardTextReader.IsUnicodeTextSizeAllowed(
                new UIntPtr(bytes),
                maxCharacters));
    }

    [Fact]
    public void ThemeImport_DoesNotUseUnboundedWpfClipboardTextRead()
    {
        string source = File.ReadAllText(FindWorkspaceFile(
            "src", "InstanceManager", "ViewModels", "ThemeViewModel.cs"));

        Assert.DoesNotContain("Clipboard.GetText()", source, StringComparison.Ordinal);
        Assert.Contains("BoundedClipboardTextReader.TryRead", source, StringComparison.Ordinal);
    }

    private static string FindWorkspaceFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
