using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class BoundedHttpContentReaderTests
{
    [Fact]
    public async Task ReadAsync_RejectsDeclaredBodyAboveLimit()
    {
        using var content = new ByteArrayContent(new byte[32]);
        content.Headers.ContentLength = 10_000;

        byte[]? result = await BoundedHttpContentReader.ReadAsync(content, 128);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadAsync_StopsChunkedBodyWhenLimitIsExceeded()
    {
        using var source = new MemoryStream(new byte[257]);
        using var content = new StreamContent(source);
        content.Headers.ContentLength = null;

        byte[]? result = await BoundedHttpContentReader.ReadAsync(content, 256);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadAsync_ReturnsBodyWithinLimit()
    {
        byte[] expected = { 1, 2, 3, 4 };
        using var content = new ByteArrayContent(expected);

        byte[]? result = await BoundedHttpContentReader.ReadAsync(content, 256);

        Assert.Equal(expected, result);
    }
}
