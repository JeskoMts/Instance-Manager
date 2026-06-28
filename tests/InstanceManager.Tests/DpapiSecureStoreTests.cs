using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public class DpapiSecureStoreTests
{
    [Fact]
    public void ProtectUnprotect_RoundTrips()
    {
        var store = new DpapiSecureStore();
        const string secret = "neutral-sensitive-test-payload-AB12cd34EF";

        string blob = store.Protect(secret);
        string back = store.Unprotect(blob);

        Assert.Equal(secret, back);
    }

    [Fact]
    public void Protect_DoesNotContainPlaintext()
    {
        var store = new DpapiSecureStore();
        const string secret = "sensitive-plaintext-test-value";

        string blob = store.Protect(secret);

        Assert.DoesNotContain(secret, blob);
    }

    [Fact]
    public void TryUnprotect_InvalidBlob_ReturnsFalse()
    {
        var store = new DpapiSecureStore();
        Assert.False(store.TryUnprotect("not-a-valid-base64-dpapi-blob!!", out string value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void TryUnprotect_ValidBlob_ReturnsTrue()
    {
        var store = new DpapiSecureStore();
        string blob = store.Protect("hello");
        Assert.True(store.TryUnprotect(blob, out string value));
        Assert.Equal("hello", value);
    }
}
