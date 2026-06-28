using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace InstanceManager.Services;

public interface IRobloxExecutableValidator
{
    bool TryValidate(string versionsRoot, string executablePath, out string error);
}

public sealed class RobloxExecutableValidator : IRobloxExecutableValidator
{
    private const string ExpectedFileName = "RobloxPlayerBeta.exe";
    private const string ExpectedSigner = "Roblox Corporation";
    private readonly Func<string, bool> _isTrustedRobloxBinary;

    public RobloxExecutableValidator() : this(IsTrustedRobloxBinary)
    {
    }

    internal RobloxExecutableValidator(Func<string, bool> isTrustedRobloxBinary) =>
        _isTrustedRobloxBinary = isTrustedRobloxBinary;

    public bool TryValidate(string versionsRoot, string executablePath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(versionsRoot) || string.IsNullOrWhiteSpace(executablePath))
        {
            error = "The Roblox executable path is empty.";
            return false;
        }

        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionsRoot));
            string candidate = Path.GetFullPath(executablePath);

            if (!string.Equals(Path.GetFileName(candidate), ExpectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                error = "The selected file is not RobloxPlayerBeta.exe.";
                return false;
            }

            string relative = Path.GetRelativePath(root, candidate);
            if (Path.IsPathRooted(relative) ||
                string.Equals(relative, "..", StringComparison.Ordinal) ||
                relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                error = "The Roblox executable escapes the configured versions directory.";
                return false;
            }

            if (!File.Exists(candidate))
            {
                error = "RobloxPlayerBeta.exe was not found.";
                return false;
            }

            FileAttributes fileAttributes = File.GetAttributes(candidate);
            if ((fileAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                ContainsReparsePoint(root, Path.GetDirectoryName(candidate)!))
            {
                error = "Reparse-point Roblox executable paths are not allowed.";
                return false;
            }

            if (!_isTrustedRobloxBinary(candidate))
            {
                error = "RobloxPlayerBeta.exe does not have a valid Roblox Authenticode signature.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            error = "The Roblox executable could not be verified.";
            return false;
        }
    }

    private static bool ContainsReparsePoint(string root, string directory)
    {
        string current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        while (true)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;

            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                return false;

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent == null)
                return true;
            current = Path.TrimEndingDirectorySeparator(parent.FullName);
        }
    }

    private static bool IsTrustedRobloxBinary(string path)
    {
        if (WinTrust.VerifyEmbeddedSignature(path) != 0)
            return false;

        try
        {
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
            using var signer = new X509Certificate2(certificate);
            string simpleName = signer.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            return string.Equals(simpleName, ExpectedSigner, StringComparison.OrdinalIgnoreCase) ||
                   signer.Subject.Contains($"O={ExpectedSigner}", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static class WinTrust
    {
        private static readonly Guid GenericVerifyV2 =
            new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

        public static int VerifyEmbeddedSignature(string filePath)
        {
            var fileInfo = new WinTrustFileInfo(filePath);
            IntPtr fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            try
            {
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, fDeleteOld: false);
                var data = new WinTrustData(fileInfoPointer);
                Guid action = GenericVerifyV2;
                return WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            }
            finally
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int WinVerifyTrust(
            IntPtr hwnd,
            [In] ref Guid actionId,
            [In] ref WinTrustData trustData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public WinTrustFileInfo(string filePath)
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>();
                FilePath = filePath;
                FileHandle = IntPtr.Zero;
                KnownSubject = IntPtr.Zero;
            }

            private uint StructSize;
            [MarshalAs(UnmanagedType.LPWStr)]
            private string FilePath;
            private IntPtr FileHandle;
            private IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public WinTrustData(IntPtr fileInfo)
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustData>();
                PolicyCallbackData = IntPtr.Zero;
                SipClientData = IntPtr.Zero;
                UiChoice = 2;
                RevocationChecks = 0;
                UnionChoice = 1;
                FileInfo = fileInfo;
                StateAction = 0;
                StateData = IntPtr.Zero;
                UrlReference = IntPtr.Zero;
                ProviderFlags = 0x00001000;
                UiContext = 0;
                SignatureSettings = IntPtr.Zero;
            }

            private uint StructSize;
            private IntPtr PolicyCallbackData;
            private IntPtr SipClientData;
            private uint UiChoice;
            private uint RevocationChecks;
            private uint UnionChoice;
            private IntPtr FileInfo;
            private uint StateAction;
            private IntPtr StateData;
            private IntPtr UrlReference;
            private uint ProviderFlags;
            private uint UiContext;
            private IntPtr SignatureSettings;
        }
    }
}
