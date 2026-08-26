using System.Text.Json;

namespace DeskStealth.Settings;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _directory;

    public SettingsStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskStealth");
    }

    public string SettingsPath => Path.Combine(_directory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            BackupCorruptSettings();
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        var temporaryPath = SettingsPath + ".tmp";
        try
        {
            settings.Normalize();
            Directory.CreateDirectory(_directory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(SettingsPath))
            {
                File.Replace(temporaryPath, SettingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, SettingsPath);
            }
            return true;
        }
        catch (IOException)
        {
            TryDelete(temporaryPath);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);
            return false;
        }
    }

    public void Clear()
    {
        TryDelete(SettingsPath);
        TryDelete(SettingsPath + ".tmp");
    }

    private void BackupCorruptSettings()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var suffix = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
            File.Move(SettingsPath, Path.Combine(_directory, $"settings.corrupt-{suffix}.json"));
        }
        catch (IOException)
        {
            // Recovery must never prevent the app from starting.
        }
        catch (UnauthorizedAccessException)
        {
            // Run with in-memory defaults when the directory is read-only.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Clearing settings is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Clearing settings is best effort.
        }
    }
}
