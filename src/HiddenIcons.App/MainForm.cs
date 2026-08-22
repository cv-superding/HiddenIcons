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
    // 模拟云母底图（壁纸模糊+主题罩层）；null 表示壁纸不可用，回退实色
    private Bitmap? _micaFill;
    private bool _lightTheme = true;
    private readonly System.Windows.Forms.Timer _micaDebounce = new() { Interval = 150 };
    private readonly MicaFlowPanel _toolbarHost = new();

    public MainForm(bool startHidden = false)
    {
        Text = "Hidden Icons 管理器";
        MinimumSize = new Size(940, 520); // 7 列表格不被截断的最小宽度
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

        var toolbar = _toolbarHost;
        toolbar.Dock = DockStyle.Top;
        toolbar.Height = 52;
        toolbar.Padding = new Padding(8, 8, 8, 4);
        toolbar.WrapContents = false;
        toolbar.Controls.AddRange(new Control[] { _add, _remove, _save, _settings });
        Controls.Add(_grid);
        Controls.Add(toolbar);

        _add.Click += AddProfile;
        _remove.Click += RemoveProfile;
        _save.Click += (_, _) => Save();
        _settings.Click += (_, _) => OpenTaskbarSettings();
        FormClosing += (_, _) => { if (_autoSaveOnClose) Save(); };
        if (startHidden) Load += (_, _) => Hide();

        // ---- Win11 Fluent / Mica 换肤（仅渲染外观：不改动控件、布局与交互）----
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Padding = new Padding(8);
        _micaDebounce.Tick += (_, _) => { _micaDebounce.Stop(); RegenMicaFill(); };
        Move += (_, _) => _micaDebounce.Start();
        ResizeEnd += (_, _) => _micaDebounce.Start();
        HandleCreated += (_, _) => ApplyTheme();
    }

    /// <summary>按当前系统主题应用 Fluent 皮肤（窗口效果 + 控件配色 + 云母底图 + 托盘菜单）。</summary>
    private void ApplyTheme()
    {
        _lightTheme = FluentTheme.IsLightTheme();
        FluentTheme.ApplyWindowChrome(Handle, _lightTheme);
        var p = FluentTheme.GetPalette(_lightTheme);
        BackColor = p.Card;
        ThemeLog($"light={_lightTheme} wallpaper={FluentTheme.GetWallpaperPath() ?? "none"} dpi={DeviceDpi} rows={_grid.RowCount}");
        try
        {
            FluentTheme.StyleButton(_add, p);
            FluentTheme.StyleButton(_remove, p);
            FluentTheme.StyleButton(_save, p, primary: true);
            FluentTheme.StyleButton(_settings, p);
        }
        catch (Exception ex) { ThemeLog("buttons FAILED: " + ex.Message); }
        try { FluentTheme.StyleGrid(_grid, p); }
        catch (Exception ex) { ThemeLog("grid FAILED: " + ex.Message); }
        try { _tray.ApplyTheme(_lightTheme); }
        catch (Exception ex) { ThemeLog("tray FAILED: " + ex.Message); }
        RegenMicaFill();
        ThemeLog("apply done");
    }

    /// <summary>重建模拟云母底图（壁纸不可用时回退实色卡片）。</summary>
    private void RegenMicaFill()
    {
        var old = _micaFill;
        try
        {
            _micaFill = FluentTheme.BuildMicaFill(
                _lightTheme, new Rectangle(Location, Size), ClientSize);
        }
        catch (Exception ex) { ThemeLog("mica FAILED: " + ex.Message); }
        if (_micaFill is not null) BackColor = _lightTheme ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
        Invalidate();
        old?.Dispose();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_micaFill is not null)
        {
            e.Graphics.DrawImage(_micaFill, ClientRectangle);
            return;
        }
        base.OnPaintBackground(e);
    }

    private static void ThemeLog(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hiddenicons-theme.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}\r\n");
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        if (m.Msg == WM_SETTINGCHANGE)
        {
            var section = m.LParam == IntPtr.Zero
                ? null
                : System.Runtime.InteropServices.Marshal.PtrToStringUni(m.LParam);
            if (section == "ImmersiveColorSet") ApplyTheme(); // 系统深浅模式切换时实时换肤
            else if (section is null || section.Contains("Wall")) _micaDebounce.Start(); // 换壁纸后重磨砂
        }
        base.WndProc(ref m);
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
        if (disposing)
        {
            _tray.Dispose();
            _micaDebounce.Stop();
            _micaDebounce.Dispose();
            _micaFill?.Dispose();
        }
        base.Dispose(disposing);
    }
}
