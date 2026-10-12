using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Settings;

/// <summary>User settings persisted between runs.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public ContactSheetOptions Options { get; set; } = new();

    public string LastSourceDirectory { get; set; } = string.Empty;

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }
}

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in the per-user config folder.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DirectoryPath { get; } = ResolveDirectory();

    public static string FilePath { get; } = Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            return Deserialize(json);
        }
        catch
        {
            // A corrupt or unreadable settings file should never stop the app from starting.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, Serialize(settings));
        }
        catch
        {
            // Persisting settings is best-effort.
        }
    }

    /// <summary>Serializes settings to JSON (exposed for testing).</summary>
    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, JsonOptions);

    /// <summary>Deserializes settings from JSON (exposed for testing).</summary>
    public static AppSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

    private static string ResolveDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "DigitalContactSheet");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support", "DigitalContactSheet")
            : Path.Combine(home, ".config", "DigitalContactSheet");
    }
}
