using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HiddenIcons.Core;

/// <summary>
/// Win11 Fluent / Mica 视觉层：仅负责渲染外观（云母背景、圆角、深浅模式、配色），
/// 不涉及任何业务逻辑与控件布局。所有默认传统样式经由本层替换。
/// </summary>
public static class FluentTheme
{
    /* ---------------------------- DWM (窗口级效果) ---------------------------- */

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;   // 标题栏跟随深浅模式
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;  // 圆角
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;       // Mica 云母背景（Win11 22H2+）
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2;                // Mica

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>当前系统应用主题是否为浅色（读不到注册表时按浅色处理）。</summary>
    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v) return v != 0;
        }
        catch { /* 注册表不可用时回退浅色 */ }
        return true;
    }

    /// <summary>
    /// 应用窗口级 Win11 效果：圆角 + Mica 背景 + 标题栏深浅模式。
    /// 返回 Mica 是否成功启用（旧系统返回 false，调用方应回退到实色背景）。
    /// </summary>
    public static bool ApplyWindowChrome(IntPtr hwnd, bool light)
    {
        if (hwnd == IntPtr.Zero) return false;
        int dark = light ? 0 : 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        int mica = DWMSBT_MAINWINDOW;
        return DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int)) == 0;
    }

    /* -------------------------------- 配色板 -------------------------------- */

    public static Palette GetPalette(bool light) => light ? LightPalette() : DarkPalette();

    private static Palette LightPalette() => new(
        text: Color.FromArgb(0x1B, 0x1B, 0x1B),
        secondaryText: Color.FromArgb(0x5D, 0x5D, 0x5D),
        card: Color.FromArgb(0xFC, 0xFC, 0xFC),
        header: Color.FromArgb(0xF3, 0xF3, 0xF3),
        gridLine: Color.FromArgb(0xE9, 0xE9, 0xE9),
        control: Color.FromArgb(0xF9, 0xF9, 0xF9),
        controlBorder: Color.FromArgb(0xD9, 0xD9, 0xD9),
        controlHover: Color.FromArgb(0xF0, 0xF0, 0xF0),
        controlPressed: Color.FromArgb(0xE4, 0xE4, 0xE4),
        accent: SystemAccent(fallback: Color.FromArgb(0x00, 0x67, 0xC0)),
        accentText: Color.White,
        menuBg: Color.FromArgb(0xF9, 0xF9, 0xF9),
        menuBorder: Color.FromArgb(0xE0, 0xE0, 0xE0),
        menuHover: Color.FromArgb(0xEC, 0xEC, 0xEC),
        separator: Color.FromArgb(0xE5, 0xE5, 0xE5));

    private static Palette DarkPalette() => new(
        text: Color.FromArgb(0xFF, 0xFF, 0xFF),
        secondaryText: Color.FromArgb(0xC5, 0xC5, 0xC5),
        card: Color.FromArgb(0x27, 0x27, 0x27),
        header: Color.FromArgb(0x2E, 0x2E, 0x2E),
        gridLine: Color.FromArgb(0x36, 0x36, 0x36),
        control: Color.FromArgb(0x2D, 0x2D, 0x2D),
        controlBorder: Color.FromArgb(0x4A, 0x4A, 0x4A),
        controlHover: Color.FromArgb(0x38, 0x38, 0x38),
        controlPressed: Color.FromArgb(0x1F, 0x1F, 0x1F),
        accent: SystemAccent(fallback: Color.FromArgb(0x4C, 0xC2, 0xFF)),
        accentText: Color.FromArgb(0x10, 0x10, 0x10),
        menuBg: Color.FromArgb(0x2C, 0x2C, 0x2C),
        menuBorder: Color.FromArgb(0x3F, 0x3F, 0x3F),
        menuHover: Color.FromArgb(0x3A, 0x3A, 0x3A),
        separator: Color.FromArgb(0x3F, 0x3F, 0x3F));

    /// <summary>读取系统强调色（HKCU\DWM\ColorizationColor，ABGR 存储）。</summary>
    private static Color SystemAccent(Color fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("ColorizationColor") is int argb)
            {
                var raw = Color.FromArgb(argb); // 0xAABBGGRR：R/B 通道是反的
                return Color.FromArgb(255, raw.B, raw.G, raw.R);
            }
        }
        catch { /* 读不到就用 Fluent 标准强调色 */ }
        return fallback;
    }

    /* ------------------------------ 控件换肤 ------------------------------ */

    /// <summary>按钮：扁平 Fluent 外观 + 圆角裁剪（保持原有尺寸、文本与点击行为）。</summary>
    public static void StyleButton(Button button, Palette p)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = p.Control;
        button.ForeColor = p.Text;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = p.ControlBorder;
        button.FlatAppearance.MouseOverBackColor = p.ControlHover;
        button.FlatAppearance.MouseDownBackColor = p.ControlPressed;
        button.SizeChanged += (_, _) => RoundControl(button, 6);
        RoundControl(button, 6);
    }

    /// <summary>表格：Fluent 卡片化外观（隐藏边框、扁平表头、强调色选区、水平发丝线）。</summary>
    public static void StyleGrid(DataGridView grid, Palette p)
    {
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false; // 脱离经典主题表头
        grid.BackgroundColor = p.Card;
        grid.GridColor = p.GridLine;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        grid.ColumnHeadersDefaultCellStyle.BackColor = p.Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = p.SecondaryText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Header;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.SecondaryText;
        grid.RowHeadersDefaultCellStyle.BackColor = p.Card;
        grid.RowHeadersDefaultCellStyle.ForeColor = p.SecondaryText;

        grid.DefaultCellStyle.BackColor = p.Card;
        grid.DefaultCellStyle.ForeColor = p.Text;
        grid.DefaultCellStyle.SelectionBackColor = p.Accent;
        grid.DefaultCellStyle.SelectionForeColor = p.AccentText;

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column is DataGridViewComboBoxColumn combo) combo.FlatStyle = FlatStyle.Flat;
        }
    }

    private static void RoundControl(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(control.Width - d, 0, d, d, 270, 90);
        path.AddArc(control.Width - d, control.Height - d, d, d, 0, 90);
        path.AddArc(0, control.Height - d, d, d, 90, 90);
        path.CloseFigure();
        control.Region = new Region(path);
    }
}

/// <summary>Fluent 配色（跟随系统深浅模式生成）。</summary>
public sealed class Palette
{
    internal Palette(
        Color text, Color secondaryText, Color card, Color header, Color gridLine,
        Color control, Color controlBorder, Color controlHover, Color controlPressed,
        Color accent, Color accentText, Color menuBg, Color menuBorder, Color menuHover, Color separator)
    {
        Text = text;
        SecondaryText = secondaryText;
        Card = card;
        Header = header;
        GridLine = gridLine;
        Control = control;
        ControlBorder = controlBorder;
        ControlHover = controlHover;
        ControlPressed = controlPressed;
        Accent = accent;
        AccentText = accentText;
        MenuBg = menuBg;
        MenuBorder = menuBorder;
        MenuHover = menuHover;
        Separator = separator;
    }

    public Color Text { get; }
    public Color SecondaryText { get; }
    public Color Card { get; }
    public Color Header { get; }
    public Color GridLine { get; }
    public Color Control { get; }
    public Color ControlBorder { get; }
    public Color ControlHover { get; }
    public Color ControlPressed { get; }
    public Color Accent { get; }
    public Color AccentText { get; }
    public Color MenuBg { get; }
    public Color MenuBorder { get; }
    public Color MenuHover { get; }
    public Color Separator { get; }
}

/// <summary>托盘/工具栏菜单的 Fluent 渲染器（替换经典 Professional 渲染路径）。</summary>
public sealed class FluentMenuRenderer : ToolStripProfessionalRenderer
{
    public FluentMenuRenderer(Palette p) : base(new FluentColorTable(p)) { }
}

internal sealed class FluentColorTable : ProfessionalColorTable
{
    private readonly Palette _p;
    internal FluentColorTable(Palette p) => _p = p;

    public override Color MenuBorder => _p.MenuBorder;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => _p.MenuHover;
    public override Color MenuItemSelectedGradientBegin => _p.MenuHover;
    public override Color MenuItemSelectedGradientEnd => _p.MenuHover;
    public override Color MenuItemPressedGradientBegin => _p.MenuHover;
    public override Color MenuItemPressedGradientEnd => _p.MenuHover;
    public override Color ToolStripDropDownBackground => _p.MenuBg;
    public override Color ImageMarginGradientBegin => _p.MenuBg;
    public override Color ImageMarginGradientMiddle => _p.MenuBg;
    public override Color ImageMarginGradientEnd => _p.MenuBg;
    public override Color SeparatorDark => _p.Separator;
    public override Color SeparatorLight => _p.Separator;
    public override Color ToolStripBorder => _p.MenuBorder;
    public override Color ToolStripGradientBegin => _p.MenuBg;
    public override Color ToolStripGradientMiddle => _p.MenuBg;
    public override Color ToolStripGradientEnd => _p.MenuBg;
    public override Color CheckBackground => _p.MenuHover;
    public override Color CheckSelectedBackground => _p.MenuHover;
    public override Color ButtonSelectedBorder => _p.MenuBorder;
    public override Color ButtonSelectedHighlight => _p.MenuHover;
    public override Color ButtonPressedBorder => _p.MenuBorder;
    public override Color ButtonPressedHighlight => _p.MenuHover;
}

/// <summary>
/// 透底容器：跳过 GDI 背景填充，让 DWM 的 Mica 材质从工具栏区域透出。
/// 布局行为与 FlowLayoutPanel 完全一致。
/// </summary>
public sealed class MicaFlowPanel : FlowLayoutPanel
{
    public MicaFlowPanel()
    {
        SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { /* 由 DWM 绘制云母背景 */ }
}
