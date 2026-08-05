using HiddenIcons.Core;

namespace HiddenIcons.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))));
    }
}
