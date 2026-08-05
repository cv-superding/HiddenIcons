using Microsoft.Win32;

namespace HiddenIcons.Core;

public static class StartupRegistration
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Prefix = "HiddenIcons.Profile.";
    private const string ManagerValue = "HiddenIcons.Manager";

    public static void Apply(IEnumerable<LaunchProfile> profiles, string? managerExe = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunPath, writable: true)
            ?? throw new InvalidOperationException("无法打开当前用户启动项。");
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            var valueName = Prefix + profile.Id.ToString("N");
            if (profile.Mode == LoadMode.RunKey && File.Exists(profile.ExecutablePath))
            {
                key.SetValue(valueName, BuildCommand(profile.ExecutablePath, profile.Arguments));
                active.Add(valueName);
            }
            else key.DeleteValue(valueName, false);
        }

        if (profiles.Any(p => p.Mode == LoadMode.Tray) && !string.IsNullOrWhiteSpace(managerExe))
            key.SetValue(ManagerValue, BuildCommand(managerExe, "--tray"));
        else key.DeleteValue(ManagerValue, false);

        foreach (var value in key.GetValueNames().Where(n => n.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && !active.Contains(n)))
            key.DeleteValue(value, false);
    }

    private static string BuildCommand(string exe, string args) =>
        $"\"{exe.Replace("\"", "\\\"")}\"{(string.IsNullOrWhiteSpace(args) ? string.Empty : " " + args)}";
}
