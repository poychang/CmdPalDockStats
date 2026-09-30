using System.Text.Json;
using CmdPalDockStats.Contracts;
using CmdPalDockStats.Core.Models;

namespace CmdPalDockStats.Core.Services;

public sealed class SettingsStore(string path)
{
    public bool CanWrite { get; private set; } = true;
    public string? Diagnostic { get; private set; }

    public AppSettings Load()
    {
        if (!File.Exists(path))
        {
            return new();
        }

        try
        {
            return Read(path);
        }
        catch (UnsupportedSchemaException exception)
        {
            CanWrite = false;
            Diagnostic = exception.Message;
            return new();
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Diagnostic = $"Settings could not be loaded: {exception.GetType().Name}.";
            try
            {
                File.Copy(path, $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", false);
                if (File.Exists(path + ".bak"))
                {
                    var backup = Read(path + ".bak");
                    Diagnostic += " Loaded the last valid backup; original file preserved.";
                    return backup;
                }
            }
            catch (Exception recoveryException) when (recoveryException is JsonException or IOException or
                InvalidDataException or UnauthorizedAccessException or UnsupportedSchemaException)
            {
                Diagnostic += $" Recovery: {recoveryException.GetType().Name}.";
                if (recoveryException is IOException or UnauthorizedAccessException)
                {
                    CanWrite = false;
                }
            }

            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException(Diagnostic ?? "Settings are read-only.");
        }

        settings.Validate();
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, SensorProtocol.JsonOptions);
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                string? backup = null;
                try
                {
                    Read(path);
                    backup = path + ".bak";
                }
                catch (Exception exception) when (exception is JsonException or InvalidDataException)
                {
                    backup = null;
                }

                File.Replace(temporary, path, backup);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static AppSettings Read(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        if (!document.RootElement.TryGetProperty("schemaVersion", out var schema) || !schema.TryGetInt32(out var version))
        {
            throw new InvalidDataException("Missing settings schema version.");
        }

        if (version != AppSettings.CurrentSchemaVersion)
        {
            throw new UnsupportedSchemaException($"Settings schema {version} is not supported; original file will not be overwritten.");
        }

        var settings = document.Deserialize<AppSettings>(SensorProtocol.JsonOptions)
            ?? throw new InvalidDataException("Empty settings.");
        settings.Validate();
        return settings;
    }

    private sealed class UnsupportedSchemaException(string message) : Exception(message);
}