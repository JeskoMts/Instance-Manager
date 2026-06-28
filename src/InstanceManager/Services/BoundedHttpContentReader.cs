using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

internal static class BoundedHttpContentReader
{
    public static async Task<byte[]?> ReadAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (content.Headers.ContentLength is long declaredLength && declaredLength > maxBytes)
            return null;

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream(
            content.Headers.ContentLength is long length
                ? (int)Math.Min(length, maxBytes)
                : Math.Min(16 * 1024, maxBytes));

        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int total = 0;
            while (true)
            {
                int remainingWithSentinel = maxBytes - total + 1;
                int read = await source.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remainingWithSentinel)),
                    cancellationToken);
                if (read == 0)
                    return destination.ToArray();

                total += read;
                if (total > maxBytes)
                    return null;

                destination.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
