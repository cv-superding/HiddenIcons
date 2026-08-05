using System.Diagnostics;

namespace HiddenIcons.Core;

public sealed class ProcessSupervisor
{
    private readonly Dictionary<Guid, Process> _started = new();

    public void Reconcile(IEnumerable<LaunchProfile> profiles)
    {
        foreach (var profile in profiles.Where(p => p.Mode == LoadMode.Service && File.Exists(p.ExecutablePath)))
        {
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
                if (process is not null) _started[profile.Id] = process;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Trace.WriteLine($"Failed to start {profile.ExecutablePath}: {ex.Message}");
            }
        }

        foreach (var item in _started.ToArray())
        {
            if (item.Value.HasExited)
            {
                var profile = profiles.FirstOrDefault(p => p.Id == item.Key);
                _started.Remove(item.Key);
                if (profile is null || !profile.RestartOnExit) continue;
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }

    private bool IsRunning(LaunchProfile profile)
    {
        var fileName = Path.GetFileNameWithoutExtension(profile.ExecutablePath);
        return Process.GetProcessesByName(fileName).Any();
    }

    public void StopOwnedProcesses()
    {
        foreach (var process in _started.Values)
        {
            try
            {
                if (!process.HasExited) process.CloseMainWindow();
                process.Dispose();
            }
            catch (InvalidOperationException) { }
        }
        _started.Clear();
    }
}
