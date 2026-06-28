using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AccountRowAvatarTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task LoadAvatarAsync_PublishesFrozenImage()
    {
        AccountRowViewModel row = CreateRow(new FakeAvatarService(OnePixelPng));

        await row.LoadAvatarAsync();

        Assert.NotNull(row.AvatarImage);
        Assert.True(row.AvatarImage!.IsFrozen);
    }

    [Fact]
    public async Task LoadAvatarAsync_InvalidImageBytesKeepInitialsFallback()
    {
        AccountRowViewModel row = CreateRow(new FakeAvatarService(new byte[] { 1, 2, 3 }));

        await row.LoadAvatarAsync();

        Assert.Null(row.AvatarImage);
    }

    [Fact]
    public async Task LoadAvatarAsync_ServiceFailureKeepsInitialsFallback()
    {
        AccountRowViewModel row = CreateRow(new FakeAvatarService(error: new HttpRequestException("offline")));

        await row.LoadAvatarAsync();

        Assert.Null(row.AvatarImage);
    }

    [Fact]
    public async Task LoadAvatarAsync_DecodesLargeImageAtDisplayResolution()
    {
        AccountRowViewModel row = CreateRow(new FakeAvatarService(CreatePng(512, 512)));

        await row.LoadAvatarAsync();

        BitmapSource image = Assert.IsAssignableFrom<BitmapSource>(row.AvatarImage);
        Assert.Equal(72, image.PixelWidth);
        Assert.Equal(72, image.PixelHeight);
    }

    private static byte[] CreatePng(int width, int height)
    {
        int stride = width * 4;
        BitmapSource source = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, new byte[stride * height], stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static AccountRowViewModel CreateRow(IRobloxAvatarService avatars)
    {
        var parent = new AccountListViewModel(
            new EmptyAccountRepository(),
            new EmptyGroupRepository(),
            null!,
            null!,
            new InstanceTracker());
        var account = new Account { UserId = 42, Username = "Builderman", DisplayName = "Builderman" };
        return new AccountRowViewModel(account, parent, avatars);
    }

    private sealed class FakeAvatarService : IRobloxAvatarService
    {
        private readonly byte[]? _bytes;
        private readonly Exception? _error;

        public FakeAvatarService(byte[]? bytes = null, Exception? error = null)
        {
            _bytes = bytes;
            _error = error;
        }

        public Task<byte[]?> GetAvatarAsync(long userId, CancellationToken cancellationToken = default) =>
            _error is null ? Task.FromResult(_bytes) : Task.FromException<byte[]?>(_error);
    }

    private sealed class EmptyAccountRepository : IAccountRepository
    {
        public IReadOnlyList<Account> All { get; } = Array.Empty<Account>();
        public Account? FindByUserId(long userId) => null;
        public void Upsert(Account account) { }
        public void Remove(Account account) { }
    }

    private sealed class EmptyGroupRepository : IGroupRepository
    {
        public IReadOnlyList<AccountGroup> All { get; } = Array.Empty<AccountGroup>();
        public AccountGroup Add(string name, string colorHex) => throw new NotSupportedException();
        public void Update(AccountGroup group) { }
        public void Remove(AccountGroup group) { }
    }
}
