using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HiddenIcons.AppWin;

/// <summary>
/// 纯 Win32 托盘图标（Shell_NotifyIcon + 隐藏消息窗口 + 原生上下文菜单）。
/// 不依赖 WinForms NotifyIcon；原生菜单在 Win11 上自动跟随系统深浅主题。
/// </summary>
public sealed class TrayIconWin32 : IDisposable
{
    private const uint WM_APP_TRAY = 0x8001; // WM_APP
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04;
    private const int NIM_ADD = 0x00, NIM_DELETE = 0x02;
    private const int MF_STRING = 0x00, MF_SEPARATOR = 0x800;
    private const uint TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 0x0002;
    private const int IDI_APPLICATION = 32512;

    private readonly EventHandler _show;
    private readonly EventHandler _exit;
    private readonly WndProcDelegate _wndProc; // 防 GC 回收
    private readonly IntPtr _hwnd;
    private IntPtr _icon;
    private bool _visible;
    private bool _added;

    public TrayIconWin32(EventHandler show, EventHandler exit)
    {
        _show = show;
        _exit = exit;
        _wndProc = WndProc;

        const string className = "HiddenIcons_TrayMsg";
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = className,
            hInstance = GetModuleHandleW(null),
        };
        RegisterClassExW(ref wc);
        // HWND_MESSAGE：消息专用窗口，不出现在任务栏和任何界面
        _hwnd = CreateWindowExW(0, className, "HiddenIcons tray", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法创建托盘消息窗口");

        _icon = LoadIconW(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
    }

    /// <summary>显示/隐藏托盘图标（对应旧版 NotifyIcon.Visible）。</summary>
    public void SetVisible(bool visible)
    {
        if (visible == _visible) return;
        if (visible && !_added)
        {
            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_APP_TRAY,
                hIcon = _icon,
                szTip = "Hidden Icons",
            };
            if (Shell_NotifyIconW(NIM_ADD, ref nid)) _added = true;
        }
        else if (!visible && _added)
        {
            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hwnd,
                uID = 1,
            };
            Shell_NotifyIconW(NIM_DELETE, ref nid);
            _added = false;
        }
        _visible = visible;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP_TRAY)
        {
            uint m = (uint)lParam.ToInt64();
            if (m == WM_LBUTTONDBLCLK) _show?.Invoke(this, EventArgs.Empty);
            else if (m == WM_RBUTTONUP) ShowMenu();
            return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        AppendMenuW(menu, MF_STRING, 1, "打开管理器");
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, MF_STRING, 2, "退出");
        GetCursorPos(out var pt);
        // TrackPopupMenu 前必须 SetForegroundWindow，否则点菜单外无法关闭
        SetForegroundWindow(_hwnd);
        var cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);
        if (cmd == 1) _show?.Invoke(this, EventArgs.Empty);
        else if (cmd == 2) _exit?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_added)
        {
            var nid = new NOTIFYICONDATAW { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(), hWnd = _hwnd, uID = 1 };
            Shell_NotifyIconW(NIM_DELETE, ref nid);
            _added = false;
        }
        if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd);
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
    }

    /* ------------------------------ P/Invoke ------------------------------ */

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, int uFlags, IntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
