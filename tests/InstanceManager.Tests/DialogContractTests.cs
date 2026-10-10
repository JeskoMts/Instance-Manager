using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using InstanceManager.Models;
using InstanceManager.Views;
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

    [Fact]
    public void EditingAFavorite_KeepsItsPrivateServer_UntilAJobIdOrAnotherGameReplacesIt()
    {
        const string jobId = "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab";
        var saved = new FavoriteGame { Name = "Old", PlaceId = 920587237, PrivateServerLinkCode = "123" };

        FavoriteGame renamed = FavoriteEditorDialog.Edited(saved, "New", 920587237, null);
        Assert.Equal(saved.Id, renamed.Id);
        Assert.Equal("New", renamed.Name);
        Assert.Equal("123", renamed.PrivateServerLinkCode);

        Assert.Null(FavoriteEditorDialog.Edited(saved, "New", 606849621, null).PrivateServerLinkCode);

        FavoriteGame withJob = FavoriteEditorDialog.Edited(saved, "New", 920587237, jobId);
        Assert.Null(withJob.PrivateServerLinkCode);
        Assert.Equal(jobId, withJob.DefaultJobId);
    }

    [Theory]
    [InlineData("ConfirmDialog.xaml")]
    [InlineData("FavoriteEditorDialog.xaml")]
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
