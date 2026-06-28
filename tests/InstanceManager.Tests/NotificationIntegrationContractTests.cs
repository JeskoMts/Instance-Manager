using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InstanceManager.Tests;

public sealed class NotificationIntegrationContractTests
{
    [Fact]
    public void ShellAndFeatureViewModels_PublishTypedNotifications()
    {
        string shell = Read("src", "InstanceManager", "ViewModels", "ShellViewModel.cs");
        string accounts = Read("src", "InstanceManager", "ViewModels", "AccountListViewModel.cs");
        string launch = Read("src", "InstanceManager", "ViewModels", "LaunchPanelViewModel.cs");
        string games = Read("src", "InstanceManager", "ViewModels", "GamesViewModel.cs");

        Assert.Contains("Notifications.Show(id, kind", shell, StringComparison.Ordinal);
        Assert.Contains("_shell.Notify(NotificationId.", accounts, StringComparison.Ordinal);
        Assert.Contains("_shell.Notify(NotificationId.", launch, StringComparison.Ordinal);
        Assert.Contains("NotificationId.GameSelected", shell, StringComparison.Ordinal);
        Assert.Contains("ApplyGameTarget(card.PlaceId, card.Name)", games, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }
}
