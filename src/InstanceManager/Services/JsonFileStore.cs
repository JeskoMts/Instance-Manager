using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace InstanceManager.Services;

public sealed class JsonFileStore
{
    internal const int MaxFileBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly object _gate = new();

    private byte[]? _lastHash;

    public JsonFileStore(string path) => _path = path;

    public T Load<T>(Func<T> fallback)
    {
        try
        {
            if (!File.Exists(_path))
                return fallback();

            byte[] bytes;
            using (var stream = new FileStream(
                       _path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 1,
                       FileOptions.SequentialScan))
            {
                if (stream.Length > MaxFileBytes)
                    return fallback();
                bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
            }

            ReadOnlySpan<byte> utf8 = bytes;
            string? text = null;
            if (utf8.StartsWith(Encoding.UTF8.Preamble))
                utf8 = utf8[Encoding.UTF8.Preamble.Length..];
            else if (DetectUnicodeEncoding(bytes) is { } unicode)
                text = unicode.GetString(bytes, unicode.Preamble.Length, bytes.Length - unicode.Preamble.Length);
            if (text == null && !Utf8.IsValid(utf8))
                text = Encoding.UTF8.GetString(utf8);

            if (text != null)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return fallback();
                T fromText = JsonSerializer.Deserialize<T>(text, Options) ?? fallback();
                _lastHash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
                return fromText;
            }

            if (IsWhiteSpace(utf8))
                return fallback();
            T value = JsonSerializer.Deserialize<T>(utf8, Options) ?? fallback();
            _lastHash = SHA256.HashData(utf8);
            return value;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return fallback();
        }
    }

    public bool Save<T>(T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        if (json.Length > MaxFileBytes)
            return false;
        byte[] hash = SHA256.HashData(json);

        lock (_gate)
        {
            if (_lastHash != null && hash.AsSpan().SequenceEqual(_lastHash))
                return true;

            string tmp = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                string? dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(tmp, json);

                if (File.Exists(_path))
                    File.Replace(tmp, _path, null);
                else
                    File.Move(tmp, _path);

                _lastHash = hash;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { File.Delete(tmp); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                return false;
            }
        }
    }

    private static Encoding? DetectUnicodeEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF32.Preamble))
            return Encoding.UTF32;
        if (bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble))
            return Encoding.Unicode;
        if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
            return Encoding.BigEndianUnicode;
        if (bytes.AsSpan().StartsWith(new UTF32Encoding(bigEndian: true, byteOrderMark: true).Preamble))
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        return null;
    }

    private static bool IsWhiteSpace(ReadOnlySpan<byte> utf8)
    {
        foreach (byte b in utf8)
        {
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                return false;
        }
        return true;
    }
}
