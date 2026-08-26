namespace DeskStealth;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length == 2 && string.Equals(args[0], "--render-golden", StringComparison.OrdinalIgnoreCase))
        {
            GoldenRenderer.Render(args[1]);
            return;
        }

        using var instance = new SingleInstanceCoordinator();
        if (!instance.IsPrimary)
        {
            instance.SignalPrimary();
            return;
        }

        using var form = new MainForm(TimeProvider.System);
        _ = form.Handle;
        instance.StartListening(form.ShowClockFromSecondInstance);
        Application.Run(form);
    }
}
