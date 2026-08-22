using System.Drawing;
using System.Windows.Forms;

namespace HiddenIcons.Core;

public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;

    public TrayIconController(string tooltip, EventHandler show, EventHandler exit)
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("打开管理器", null, show);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出", null, exit);
        _notifyIcon = new NotifyIcon
        {
            Text = tooltip,
            Icon = SystemIcons.Application,
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += show;
        ApplyTheme(FluentTheme.IsLightTheme());
    }

    /// <summary>托盘菜单换 Fluent 皮肤（仅渲染，菜单项与行为不变）。</summary>
    public void ApplyTheme(bool light)
    {
        var p = FluentTheme.GetPalette(light);
        _menu.Renderer = new FluentMenuRenderer(p);
        foreach (ToolStripItem item in _menu.Items)
        {
            item.BackColor = p.MenuBg;
            item.ForeColor = p.Text;
        }
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    public void Dispose() => _notifyIcon.Dispose();
}
