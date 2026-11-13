namespace Rinse;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
            return SelfTest.Run(args.Length > 1 ? args[1] : "selftest.txt");

        using var mutex = new Mutex(true, "Rinse.SingleInstance.v1", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("Rinse is already running. Look for the teal drop in your system tray.",
                "Rinse", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool startHidden = args.Contains("--tray");
        Application.Run(new TrayApp(startHidden));
        return 0;
    }
}
