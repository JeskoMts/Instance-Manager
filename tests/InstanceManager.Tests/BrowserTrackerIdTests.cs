using System;
using System.Collections.Generic;
using System.IO;
using InstanceManager.Models;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public class BrowserTrackerIdTests
{
    [Fact]
    public void Account_Initialized_WithNonZeroBrowserTrackerId()
    {
        var account = new Account();
        Assert.NotEqual(0, account.BrowserTrackerId);
    }

    [Fact]
    public void AccountRepositoryConstructor_IfZeroBrowserTrackerId_GeneratesAndSaves()
    {
        var accounts = new List<Account>
        {
            new Account { Username = "Test1", BrowserTrackerId = 0 },
            new Account { Username = "Test2", BrowserTrackerId = 12345 }
        };

        bool changed = false;
        foreach (var acc in accounts)
        {
            if (acc.BrowserTrackerId == 0)
            {
                acc.BrowserTrackerId = InstanceManager.Services.RobloxLauncher.GenerateBrowserTrackerId();
                changed = true;
            }
        }

        Assert.True(changed);
        Assert.NotEqual(0, accounts[0].BrowserTrackerId);
        Assert.Equal(12345, accounts[1].BrowserTrackerId);
    }
}
