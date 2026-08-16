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
            // 只要模式是 RunKey 就注册，不用 File.Exists 校验：
            // U 盘/网络盘路径暂时不可用时不应该悄悄删掉用户的自启动项。
            if (profile.Mode == LoadMode.RunKey)
            {
                key.SetValue(valueName, BuildCommand(profile.ExecutablePath, profile.Arguments));
                active.Add(valueName);
            }
            else key.DeleteValue(valueName, false);
        }

        // 管理器自启动：有 Tray 模式 profile 时确保注册；
        // 没有时不再主动删除——install-user.ps1 显式安装的用户级自启动不该被悄悄清掉。
        if (profiles.Any(p => p.Mode == LoadMode.Tray) && !string.IsNullOrWhiteSpace(managerExe))
            key.SetValue(ManagerValue, BuildCommand(managerExe, "--tray"));

        foreach (var value in key.GetValueNames().Where(n => n.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && !active.Contains(n)))
            key.DeleteValue(value, false);
    }

    private static string BuildCommand(string exe, string args) =>
        $"\"{exe.Replace("\"", "\\\"")}\"{(string.IsNullOrWhiteSpace(args) ? string.Empty : " " + args)}";
}
