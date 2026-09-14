using HiddenIcons.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Storage.Pickers;
using Windows.System;

namespace HiddenIcons.AppWin;

/// <summary>LaunchProfile 的可绑定包装：属性直接写回原对象，保存时无需二次同步。</summary>
public sealed class ProfileViewModel : INotifyPropertyChanged
{
    public LaunchProfile Profile { get; }

    public ProfileViewModel(LaunchProfile profile) => Profile = profile;

    public string Name
    {
        get => Profile.Name;
        set { Profile.Name = value; Raise(); }
    }

    public string ExecutablePath
    {
        get => Profile.ExecutablePath;
        set { Profile.ExecutablePath = value; Raise(); }
    }

    public string Arguments
    {
        get => Profile.Arguments;
        set { Profile.Arguments = value; Raise(); }
    }

    public int ModeIndex
    {
        get => (int)Profile.Mode;
        set { Profile.Mode = (LoadMode)value; Raise(); }
    }

    public bool StartMinimized
    {
        get => Profile.StartMinimized;
        set { Profile.StartMinimized = value; Raise(); }
    }

    public bool RestartOnExit
    {
        get => Profile.RestartOnExit;
        set { Profile.RestartOnExit = value; Raise(); }
    }

    public bool HideOwnTrayIcon
    {
        get => Profile.HideOwnTrayIcon;
        set { Profile.HideOwnTrayIcon = value; Raise(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed partial class MainWindow : Window
{
    private readonly List<ProfileViewModel> _vms = new();
    private TrayIconWin32? _tray;

    public MainWindow()
    {
        InitializeComponent();

        // Win11 原生云母背景 + 内容延伸进标题栏（深浅模式由系统主题资源自动处理）
        SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // 显式尺寸（物理像素），避免默认窗口在 125% DPI 下过小
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1160, 720));
        AppWindow.Move(new Windows.Graphics.PointInt32(120, 100));

        // 标题栏/任务栏用项目 logo（exe 文件图标由 csproj 的 ApplicationIcon 提供）
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app-logo.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);

        foreach (var profile in App.Config.Profiles) _vms.Add(new ProfileViewModel(profile));
        ProfileList.ItemsSource = _vms;
        UpdateEmptyState();
        LoadFailBar.IsOpen = App.Store.LastLoadFailed;

        StartTrayProfiles();
        _tray = new TrayIconWin32(ShowFromTray, ExitApp);
        _tray.SetVisible(!App.Config.Profiles.Any(p => p.HideOwnTrayIcon));

        Closed += (_, _) =>
        {
            if (!App.Store.LastLoadFailed) Save();
            _tray?.Dispose();
        };
    }

    public void HideToTray()
    {
        // 标题栏需要在首次布局后才有有效拖拽区；先隐藏窗口，托盘双击再唤起
        AppWindow.Hide();
    }

    private void ShowFromTray(object? sender, EventArgs e)
    {
        AppWindow.Show();
        Activate();
    }

    private void ExitApp(object? sender, EventArgs e)
    {
        if (!App.Store.LastLoadFailed) Save();
        Close();
        Application.Current.Exit();
    }

    private async void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".exe");
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var profile = new LaunchProfile
        {
            Name = Path.GetFileNameWithoutExtension(file.Path),
            ExecutablePath = file.Path,
            WorkingDirectory = Path.GetDirectoryName(file.Path) ?? string.Empty,
            Mode = LoadMode.Tray
        };
        App.Config.Profiles.Add(profile);
        _vms.Add(new ProfileViewModel(profile));
        UpdateEmptyState();
    }

    private void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileViewModel vm) return;
        App.Config.Profiles.Remove(vm.Profile);
        _vms.Remove(vm);
        UpdateEmptyState();
    }

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private async void TaskbarSettings_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:taskbar"));

    private void Save()
    {
        try
        {
            App.Store.Save(App.Config);
            // 与旧版一致：保存时同步 HKCU Run 自启动项（managerExe=自身）
            StartupRegistration.Apply(App.Config.Profiles, Environment.ProcessPath);
            _tray?.SetVisible(!App.Config.Profiles.Any(p => p.HideOwnTrayIcon));
            LoadFailBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            LoadFailBar.Title = "保存失败";
            LoadFailBar.Message = ex.Message;
            LoadFailBar.IsOpen = true;
        }
    }

    /// <summary>与旧版一致：启动时拉起 Tray 模式程序（已在运行的跳过，防双开）。</summary>
    private void StartTrayProfiles()
    {
        foreach (var profile in App.Config.Profiles.Where(p => p.Mode == LoadMode.Tray && File.Exists(p.ExecutablePath)))
        {
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
                    WindowStyle = profile.StartMinimized
                        ? System.Diagnostics.ProcessWindowStyle.Minimized
                        : System.Diagnostics.ProcessWindowStyle.Normal
                });
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
    }
}
