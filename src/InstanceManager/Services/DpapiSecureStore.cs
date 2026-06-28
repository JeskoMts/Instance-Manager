using System;
using System.Security.Cryptography;
using System.Text;

namespace InstanceManager.Services;

public sealed class DpapiSecureStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("InstanceManager.v1.cookie-vault");

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            byte[] encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }

    public string Unprotect(string protectedBase64)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedBase64);
        byte[] encrypted = Convert.FromBase64String(protectedBase64);
        byte[] data = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(data);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }

    public bool TryUnprotect(string protectedBase64, out string plaintext)
    {
        try
        {
            plaintext = Unprotect(protectedBase64);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            plaintext = string.Empty;
            return false;
        }
    }
}
