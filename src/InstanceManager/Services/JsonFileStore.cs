using System;
using System.IO;
using System.Text;
using System.Text.Json;

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
    private string? _lastJson;

    public JsonFileStore(string path) => _path = path;

    public T Load<T>(Func<T> fallback)
    {
        try
        {
            if (!File.Exists(_path))
                return fallback();

            using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length > MaxFileBytes)
                return fallback();

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false);
            string json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json))
                return fallback();

            T value = JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
            _lastJson = json;
            return value;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return fallback();
        }
    }

    public void Save<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes)
            return;

        lock (_gate)
        {
            if (string.Equals(json, _lastJson, StringComparison.Ordinal))
                return;

            string tmp = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                string? dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(tmp, json);

                if (File.Exists(_path))
                    File.Replace(tmp, _path, null);
                else
                    File.Move(tmp, _path);

                _lastJson = json;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { File.Delete(tmp); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
