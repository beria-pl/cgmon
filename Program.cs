using cgmon;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (_, e) =>
            Logger.LogError("Unhandled UI thread exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.LogError("Unhandled domain exception", e.ExceptionObject as Exception);

        using var ctx = new TrayAppContext();
        Application.Run(ctx);
    }
}
