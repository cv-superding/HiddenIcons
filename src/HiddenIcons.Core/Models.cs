namespace HiddenIcons.Core;

public enum LoadMode
{
    Disabled = 0,
    RunKey = 1,
    Tray = 2,
    Service = 3
}

public sealed class LaunchProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新程序";
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public LoadMode Mode { get; set; } = LoadMode.Disabled;
    public bool StartMinimized { get; set; }
    public bool RestartOnExit { get; set; }
    public bool HideOwnTrayIcon { get; set; }
}

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public List<LaunchProfile> Profiles { get; set; } = new();
}
