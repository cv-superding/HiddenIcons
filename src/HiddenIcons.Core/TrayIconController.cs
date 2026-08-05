using System.Drawing;
using System.Windows.Forms;

namespace HiddenIcons.Core;

public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconController(string tooltip, EventHandler show, EventHandler exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开管理器", null, show);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, exit);
        _notifyIcon = new NotifyIcon
        {
            Text = tooltip,
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += show;
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    public void Dispose() => _notifyIcon.Dispose();
}
