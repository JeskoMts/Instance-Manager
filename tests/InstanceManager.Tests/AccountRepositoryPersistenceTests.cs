using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using InstanceManager.Models;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AccountRepositoryPersistenceTests
{
    [Fact]
    public void Load_MigratesLegacyGroupMembershipAndPersistsIt()
    {
        string directory = NewDirectory();
        string path = Path.Combine(directory, "accounts.json");
        Guid groupId = Guid.NewGuid();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { new Account { GroupId = groupId } }));

            var repository = new AccountRepository(path);

            Account migrated = Assert.Single(repository.All);
            Assert.Null(migrated.GroupId);
            Assert.Equal(new[] { groupId }, migrated.GroupIds);
            Assert.Equal(new[] { groupId }, Assert.Single(new AccountRepository(path).All).GroupIds);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void UpsertMany_PersistsAllAccountsInOneBatch()
    {
        string directory = NewDirectory();
        string path = Path.Combine(directory, "accounts.json");
        try
        {
            var repository = new AccountRepository(path);
            repository.UpsertMany(new List<Account>
            {
                new() { UserId = 1, Username = "One" },
                new() { UserId = 2, Username = "Two" }
            });

            Assert.Equal(2, new AccountRepository(path).All.Count);
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
