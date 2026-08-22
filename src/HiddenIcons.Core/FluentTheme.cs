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

    /// <summary>系统「透明效果」是否开启；关闭时 Mica/亚克力都不会渲染，应回退实色。</summary>
    public static bool IsTransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("EnableTransparency") is int v) return v != 0;
        }
        catch { }
        return true;
    }

    /// <summary>
    /// 应用窗口级 Win11 效果：圆角 + 标题栏深浅模式。
    /// 注：24H2+ 上 DWM 云母材质走 DirectComposition 通道，GDI 黑键配方已失效
    /// （黑键区域只会显示纯黑），故云母效果改由 BuildMicaFill 模拟实现。
    /// </summary>
    public static void ApplyWindowChrome(IntPtr hwnd, bool light)
    {
        if (hwnd == IntPtr.Zero) return;
        int dark = light ? 0 : 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    /* ---------------------- 模拟云母（壁纸模糊 + 主题罩层） ---------------------- */

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, System.Text.StringBuilder pvParam, int fuWinIni);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private const int SPI_GETDESKTOPWALLPAPER = 0x0073;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>当前桌面壁纸文件路径（读不到返回 null）。</summary>
    public static string? GetWallpaperPath()
    {
        try
        {
            var sb = new System.Text.StringBuilder(1024);
            if (SystemParametersInfo(SPI_GETDESKTOPWALLPAPER, sb.Capacity, sb, 0)
                && File.Exists(sb.ToString())) return sb.ToString();
        }
        catch { }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (key?.GetValue("WallPaper") is string s && File.Exists(s)) return s;
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 生成窗口区域的「模拟云母」底图：壁纸覆盖拉伸 → 按窗口在虚拟屏幕上的位置裁切 →
    /// 强模糊（缩小再放大）→ 叠加主题色罩层（云母的实色占大头、壁纸若隐若现）。
    /// 返回 null 表示壁纸不可用，调用方应回退实色背景。
    /// </summary>
    public static Bitmap? BuildMicaFill(bool light, Rectangle windowRect, Size outputSize)
    {
        if (outputSize.Width <= 0 || outputSize.Height <= 0) return null;
        var path = GetWallpaperPath();
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            using var wall = new Bitmap(path);
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
            int vh = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));

            using var canvas = new Bitmap(vw, vh);
            using (var g = Graphics.FromImage(canvas))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                double scale = Math.Max((double)vw / wall.Width, (double)vh / wall.Height);
                int dw = Math.Max(1, (int)(wall.Width * scale));
                int dh = Math.Max(1, (int)(wall.Height * scale));
                g.DrawImage(wall, (vw - dw) / 2, (vh - dh) / 2, dw, dh);
            }

            int cx = Math.Max(0, Math.Min(windowRect.X - vx, vw - 1));
            int cy = Math.Max(0, Math.Min(windowRect.Y - vy, vh - 1));
            int cw = Math.Max(1, Math.Min(windowRect.Width, vw - cx));
            int ch = Math.Max(1, Math.Min(windowRect.Height, vh - cy));
            using var crop = canvas.Clone(new Rectangle(cx, cy, cw, ch), canvas.PixelFormat);

            // 模糊：先缩到 1/14 再高质量放大，得到无锯齿的磨砂感
            using var small = new Bitmap(crop, Math.Max(8, cw / 14), Math.Max(8, ch / 14));
            var fill = new Bitmap(outputSize.Width, outputSize.Height);
            using (var g2 = Graphics.FromImage(fill))
            {
                g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g2.DrawImage(small, 0, 0, fill.Width, fill.Height);
                using var tint = new SolidBrush(Color.FromArgb(
                    light ? 205 : 215,
                    light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32)));
                g2.FillRectangle(tint, 0, 0, fill.Width, fill.Height);
            }
            return fill;
        }
        catch
        {
            return null; // 壁纸解码失败等情况：回退实色
        }
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

    /// <summary>
    /// 按钮：扁平 Fluent 外观 + 圆角裁剪 + 现代尺寸（保持原有顺序、文本与点击行为）。
    /// primary=true 时用强调色实底（主操作按钮）。
    /// </summary>
    public static void StyleButton(Button button, Palette p, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.AutoSize = true;
        button.Font = new Font("Segoe UI", 9.75F);
        button.Padding = new Padding(16, 7, 16, 7);
        button.Margin = new Padding(2, 8, 10, 8);
        if (primary)
        {
            button.BackColor = p.Accent;
            button.ForeColor = p.AccentText;
            button.FlatAppearance.BorderColor = p.Accent;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(p.Accent, 0.12F);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(p.Accent, 0.05F);
        }
        else
        {
            button.BackColor = p.Control;
            button.ForeColor = p.Text;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = p.ControlBorder;
            button.FlatAppearance.MouseOverBackColor = p.ControlHover;
            button.FlatAppearance.MouseDownBackColor = p.ControlPressed;
        }
        button.SizeChanged += (_, _) => RoundControl(button, 6);
        RoundControl(button, 6);
    }

    /// <summary>
    /// 在数据绑定前调用的行模板预设：绑定后再改 RowTemplate 不会作用于已创建的行，
    /// 会导致行高不足、文字贴底出格（125% DPI 下尤其明显）。
    /// </summary>
    public static void PrepareGrid(DataGridView grid)
    {
        grid.RowTemplate.Height = 40;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
    }

    /// <summary>表格：Fluent 卡片化外观（无边框、扁平表头、加大行高、强调色选区、水平发丝线）。</summary>
    public static void StyleGrid(DataGridView grid, Palette p)
    {
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false; // 脱离经典主题表头
        grid.BackgroundColor = p.Card;
        grid.GridColor = p.GridLine;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersVisible = false; // 去掉左侧经典灰色行头列
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 40;
        grid.ColumnHeadersDefaultCellStyle.BackColor = p.Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = p.SecondaryText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Header;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.SecondaryText;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);

        grid.RowTemplate.Height = 40;
        grid.DefaultCellStyle.BackColor = p.Card;
        grid.DefaultCellStyle.ForeColor = p.Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        grid.DefaultCellStyle.SelectionBackColor = p.Accent;
        grid.DefaultCellStyle.SelectionForeColor = p.AccentText;
        // 归一化已存在的行高：绑定先于主题应用时，旧行仍是默认高度，文字会贴底出格
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Height < 40) row.Height = 40;
        }

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
/// 透底工具栏容器：背景透明（由父窗体绘制模拟云母底图）。布局行为与 FlowLayoutPanel 一致。
/// </summary>
public sealed class MicaFlowPanel : FlowLayoutPanel
{
    public MicaFlowPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }
}
