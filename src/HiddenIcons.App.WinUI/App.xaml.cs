using HiddenIcons.Core;
using Microsoft.UI.Xaml;

namespace HiddenIcons.AppWin;

public partial class App : Application
{
    public static ConfigStore Store { get; } = new();
    public static AppConfig Config { get; private set; } = new();

    private static Mutex? _singleInstance;

    public App()
    {
        InitializeComponent();

        // 与旧版相同的单实例语义：托盘常驻时再次启动直接退出
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\HiddenIcons.Manager.SingleInstance", out var createdNew);
        if (!createdNew) Exit();

        Config = Store.Load();
    }

    public MainWindow? Window { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase)))
            Window.HideToTray();
        else
            Window.Activate();
    }
}
