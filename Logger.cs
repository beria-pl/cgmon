namespace cgmon;

internal static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "cgmon", "errors.log");

    private static readonly object _lock = new();

    public static void LogError(string message, Exception? ex = null)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (ex != null)
                    line += $"\n  {ex.GetType().Name}: {ex.Message}";
                File.AppendAllText(LogPath, line + "\n");
            }
        }
        catch { }
    }
}
