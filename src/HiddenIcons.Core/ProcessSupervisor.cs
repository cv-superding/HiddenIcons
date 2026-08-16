using System.Diagnostics;

namespace HiddenIcons.Core;

public sealed class ProcessSupervisor
{
    private readonly Dictionary<Guid, Process> _started = new();
    // 已成功拉起后又退出的 profile：未开启 RestartOnExit 时本轮服务生命周期内不再拉起，
    // 否则“崩溃重启”开关形同虚设——按进程名探测会发现它没在运行而每 5 秒重启一次。
    private readonly HashSet<Guid> _exited = new();

    public void Reconcile(IEnumerable<LaunchProfile> profiles)
    {
        var list = profiles.ToList();
        var serviceIds = new HashSet<Guid>(list.Where(p => p.Mode == LoadMode.Service).Select(p => p.Id));

        foreach (var profile in list.Where(p => p.Mode == LoadMode.Service && File.Exists(p.ExecutablePath)))
        {
            if (_started.TryGetValue(profile.Id, out var running))
            {
                if (!HasExited(running)) continue;
                // 上个周期还活着、这个周期退出了：记录并清理
                _started.Remove(profile.Id);
                _exited.Add(profile.Id);
                try { running.Dispose(); } catch (InvalidOperationException) { }
            }

            if (_exited.Contains(profile.Id) && !profile.RestartOnExit) continue;

            // 同名进程已在运行（可能是用户手动启动的），避免重复拉起
            if (IsRunning(profile)) continue;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = profile.ExecutablePath,
                    Arguments = profile.Arguments,
                    WorkingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
                        ? Path.GetDirectoryName(profile.ExecutablePath) ?? Environment.CurrentDirectory
                        : profile.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var process = Process.Start(psi);
                if (process is not null)
                {
                    _started[profile.Id] = process;
                    _exited.Remove(profile.Id);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Trace.WriteLine($"Failed to start {profile.ExecutablePath}: {ex.Message}");
            }
        }

        // profile 被删除或改模式后清理跟踪状态（不杀已启动的进程，与服务停止时的显式停止行为保持区分）
        foreach (var id in _started.Keys.Where(id => !serviceIds.Contains(id)).ToList())
        {
            try { _started[id].Dispose(); } catch (InvalidOperationException) { }
            _started.Remove(id);
        }
        foreach (var id in _exited.Where(id => !serviceIds.Contains(id)).ToList())
        {
            _exited.Remove(id);
        }
    }

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true; // 句柄失效/无权限访问时视同已退出，交给上层按需重建
        }
    }

    private static bool IsRunning(LaunchProfile profile)
    {
        var fileName = Path.GetFileNameWithoutExtension(profile.ExecutablePath);
        return Process.GetProcessesByName(fileName).Length > 0;
    }

    public void StopOwnedProcesses()
    {
        foreach (var process in _started.Values)
        {
            try
            {
                // 服务运行在 Session 0，CloseMainWindow 对其他会话中有窗口的进程无效；
                // 优雅关闭失败就结束整棵进程树，避免服务停止后子进程遗留。
                if (!HasExited(process) && !process.CloseMainWindow())
                    process.Kill(entireProcessTree: true);
                process.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                Trace.WriteLine($"Failed to stop owned process: {ex.Message}");
            }
        }
        _started.Clear();
    }
}
