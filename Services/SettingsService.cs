using System.Text.Json;

namespace cgmon.Services;

public class SettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "cgmon");

    private static readonly string File = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public AppSettings Load()
    {
        try
        {
            if (System.IO.File.Exists(File))
            {
                var json = System.IO.File.ReadAllText(File);
                return JsonSerializer.Deserialize<AppSettings>(json, Opts) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load settings", ex);
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(settings, Opts);
            System.IO.File.WriteAllText(File, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save settings", ex);
        }
    }
}
