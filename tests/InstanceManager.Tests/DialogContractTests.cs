using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InstanceManager.Tests;

public sealed class DialogContractTests
{
    [Fact]
    public void DialogService_UsesThemedConfirmationInsteadOfMessageBox()
    {
        string source = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "Services", "WpfDialogService.cs"));

        Assert.DoesNotContain("MessageBox.Show", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmDialog.Show", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ConfirmDialog.xaml")]
    [InlineData("GroupEditorDialog.xaml")]
    [InlineData("DropActionDialog.xaml")]
    public void ModernDialogs_AreValidXaml(string fileName)
    {
        XDocument.Load(FindWorkspaceFile("src", "InstanceManager", "Views", fileName));
    }

    private static string FindWorkspaceFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
