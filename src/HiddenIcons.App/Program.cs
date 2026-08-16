using HiddenIcons.Core;

namespace HiddenIcons.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // 单实例：管理器常驻托盘时再次启动（双击快捷方式/开机自启动竞态），
        // 绝不能把所有 Tray 模式程序再拉起一遍。
        using var mutex = new Mutex(initiallyOwned: true, @"Local\HiddenIcons.Manager.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Hidden Icons 管理器已在运行，可在系统托盘中找到它。", "Hidden Icons",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new MainForm(args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))));
    }
}
