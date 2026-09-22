using System.Text.Json;

namespace InvoiceGenerator.Services;

public static class AppSettingsService
{
    private const string SettingsFileName = "app.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        string settingsPath =
            Path.Combine(
                AppContext.BaseDirectory,
                SettingsFileName
            );

        if (!File.Exists(settingsPath))
        {
            AppSettings defaultSettings = new();

            Save(
                settingsPath,
                defaultSettings
            );

            return defaultSettings;
        }

        string json =
            File.ReadAllText(settingsPath);

        AppSettings? settings =
            JsonSerializer.Deserialize<AppSettings>(
                json,
                JsonOptions
            );

        return settings ?? new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        string settingsPath =
            Path.Combine(
                AppContext.BaseDirectory,
                SettingsFileName
            );

        Save(
            settingsPath,
            settings
        );
    }

    public static string ResolvePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return AppContext.BaseDirectory;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                configuredPath
            )
        );
    }

    private static void Save(
        string settingsPath,
        AppSettings settings)
    {
        string? settingsFolder =
            Path.GetDirectoryName(settingsPath);

        if (!string.IsNullOrWhiteSpace(settingsFolder))
        {
            Directory.CreateDirectory(settingsFolder);
        }

        string json =
            JsonSerializer.Serialize(
                settings,
                JsonOptions
            );

        File.WriteAllText(
            settingsPath,
            json
        );
    }
}
