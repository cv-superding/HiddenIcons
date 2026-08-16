using HiddenIcons.Core;
using System.Diagnostics;

namespace HiddenIcons.App;

public sealed class MainForm : Form
{
    private readonly ConfigStore _store = new();
    private readonly BindingSource _source = new();
    private readonly DataGridView _grid = new();
    private readonly Button _add = new() { Text = "添加程序" };
    private readonly Button _remove = new() { Text = "删除" };
    private readonly Button _save = new() { Text = "保存配置" };
    private readonly Button _settings = new() { Text = "打开系统托盘设置" };
    private readonly TrayIconController _tray;
    private AppConfig _config;
    // 配置读取失败时置 false：退出不再自动保存，避免空配置覆盖磁盘上的真实配置；
    // 用户显式保存成功一次后恢复自动保存。
    private bool _autoSaveOnClose = true;

    public MainForm(bool startHidden = false)
    {
        Text = "Hidden Icons 管理器";
        MinimumSize = new Size(760, 420);
        StartPosition = FormStartPosition.CenterScreen;
        _tray = new TrayIconController("Hidden Icons", (_, _) => ShowFromTray(), (_, _) => Application.Exit());
        _config = _store.Load();
        _source.DataSource = _config.Profiles;
        if (_store.LastLoadFailed)
        {
            _autoSaveOnClose = false;
            MessageBox.Show(
                "配置读取失败。为避免覆盖现有配置，关闭窗口时将不会自动保存；\n手动点击「保存配置」才会写入。",
                "Hidden Icons", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            try { StartupRegistration.Apply(_config.Profiles, Application.ExecutablePath); }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
        _tray.Visible = !_config.Profiles.Any(p => p.HideOwnTrayIcon);
        StartTrayProfiles();

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "名称", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ExecutablePath", HeaderText = "程序", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Arguments", HeaderText = "参数", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewComboBoxColumn { DataPropertyName = "Mode", HeaderText = "加载模式", DataSource = Enum.GetValues<LoadMode>(), Width = 110 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "StartMinimized", HeaderText = "启动最小化", Width = 90 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "RestartOnExit", HeaderText = "崩溃重启", Width = 78 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "HideOwnTrayIcon", HeaderText = "隐藏管理器图标", Width = 110 });
        _grid.DataSource = _source;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8), WrapContents = false };
        toolbar.Controls.AddRange(new Control[] { _add, _remove, _save, _settings });
        Controls.Add(_grid);
        Controls.Add(toolbar);

        _add.Click += AddProfile;
        _remove.Click += RemoveProfile;
        _save.Click += (_, _) => Save();
        _settings.Click += (_, _) => OpenTaskbarSettings();
        FormClosing += (_, _) => { if (_autoSaveOnClose) Save(); };
        if (startHidden) Load += (_, _) => Hide();
    }

    private void AddProfile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var profile = new LaunchProfile
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExecutablePath = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
            Mode = LoadMode.Tray
        };
        _config.Profiles.Add(profile);
        _source.ResetBindings(false);
    }

    private void RemoveProfile(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.DataBoundItem is not LaunchProfile profile) return;
        _config.Profiles.Remove(profile);
        _source.ResetBindings(false);
    }

    private void Save()
    {
        try
        {
            _store.Save(_config);
            StartupRegistration.Apply(_config.Profiles, Application.ExecutablePath);
            _tray.Visible = !_config.Profiles.Any(p => p.HideOwnTrayIcon);
            _autoSaveOnClose = true; // 显式保存成功后，配置已是有效状态，恢复退出自动保存
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void StartTrayProfiles()
    {
        foreach (var profile in _config.Profiles.Where(p => p.Mode == LoadMode.Tray && File.Exists(p.ExecutablePath)))
        {
            // 目标已在运行（如用户手动开过、或上一个管理器实例启动的）就不再拉起，避免双开
            var processName = Path.GetFileNameWithoutExtension(profile.ExecutablePath);
            if (System.Diagnostics.Process.GetProcessesByName(processName).Length > 0) continue;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = profile.ExecutablePath,
                    Arguments = profile.Arguments,
                    WorkingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
                        ? Path.GetDirectoryName(profile.ExecutablePath) ?? Environment.CurrentDirectory
                        : profile.WorkingDirectory,
                    UseShellExecute = true,
                    WindowStyle = profile.StartMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
                });
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
    }

    private void OpenTaskbarSettings()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法打开设置", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tray.Dispose();
        base.Dispose(disposing);
    }
}
