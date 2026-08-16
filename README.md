# Hidden Icons

一个面向 Windows 10/11 的托盘与后台启动管理器示例。仓库中的 `_research_mactype` 是调研用的 MacType 源码快照，不参与编译。

本版本优先保证可维护性，使用 .NET 8 self-contained 发布；它不是“几 MB 内存”的最终形态。若硬指标是极低常驻内存，应将 Core/Service 迁移为 C++/Win32（或单独评估 NativeAOT），界面仍可保留 WinForms/WPF。

## 先说清楚的系统边界

Windows 没有公开 API 允许一个普通程序删除任意第三方进程的通知区域图标。`Shell_NotifyIcon(NIM_DELETE)` 只能删除调用方自己的图标；通过读取 Explorer 内部 Toolbar、跨进程写内存、注入 DLL 或修改 `IconStreams/PastIconsStream` 来“强行隐藏”属于未公开实现，系统更新容易失效，也很容易触发杀毒软件拦截。因此本项目只隐藏自己的图标，并提供 `ms-settings:taskbar` 入口让用户在系统设置中管理第三方图标。

MacType 的公开源码（`snowie2000/mactype`）验证了这一点：它的 Service Mode 是服务 + Hook/EasyHook 组合，README 明确提示 64 位系统可能触发杀毒冲突；`MacLoader` 负责受控启动，真正的 loader/tuner 并不在公开源码中。这里借鉴的是“服务与用户会话托盘分离、模式可选”的产品结构，没有复制进程注入部分。

## 目录

* `src/HiddenIcons.Core`：配置模型、原子 JSON 存储、HKCU Run 注册、服务端进程监督器、托盘图标控制器。
* `src/HiddenIcons.App`：WinForms 用户界面。可添加 EXE、编辑参数/加载模式/启动最小化/崩溃重启、保存配置、打开系统托盘设置。单实例运行：托盘常驻时再次启动不会把 Tray 模式程序重复拉起。
* `src/HiddenIcons.Service`：Worker Service。以 `LocalService` 运行，每 5 秒读取 `ProgramData\HiddenIcons\config.json`，只启动用户选择为 `Service` 的程序。进程退出后按该 profile 的「崩溃重启」决定是否重新拉起（未开启则本轮不再拉起）；同名进程已在运行时跳过，避免重复启动；服务停止时会结束由它启动的进程树。
* `installer`：服务安装、卸载脚本。

## 加载模式

| 模式 | 实现 | 适用范围 |
| --- | --- | --- |
| Disabled | 不注册 | 临时停用 |
| RunKey | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | 需要桌面和托盘的普通用户程序 |
| Tray | 当前用户启动 HiddenIcons.App，再由它启动目标 | 需要用户会话的程序；可隐藏 HiddenIcons 自身图标 |
| Service | Windows Service (`LocalService`, `start=auto`) | 真正的后台程序、无 UI/无托盘依赖的程序 |

服务模式不能把 Session 0 中的服务变成可交互的桌面程序。目标软件如果依赖托盘或桌面窗口，应使用 Tray/RunKey；不要把 GUI 软件强行改成服务。

## 容错行为

- `config.json` 读取失败时，UI 不会在退出时自动保存、也不会清理自启动项，避免用空配置覆盖磁盘上的真实配置；手动点击「保存配置」仍会写入。
- RunKey 注册只看加载模式，不校验 EXE 当前是否存在（U 盘/网络盘路径暂时不可用时不误删自启动项）。

## 构建与安装

需要 Visual Studio 2022 或 .NET 8 SDK。如果 NuGet 还原因访问 nuget.org 校验签名失败（错误 NU1301），可强制只用仓库 `NuGet.Config` 里配置的国内镜像，给 dotnet 命令追加参数：`-p:RestoreSources=https://nuget.azure.cn/v3/index.json`。

在管理员 PowerShell 中执行：

```powershell
dotnet restore HiddenIcons.sln
dotnet publish src/HiddenIcons.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\app
dotnet publish src/HiddenIcons.Service -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\service
Copy-Item publish\service\HiddenIcons.Service.exe "$env:ProgramFiles\HiddenIcons\"
Copy-Item publish\app\HiddenIcons.App.exe "$env:ProgramFiles\HiddenIcons\"
Set-Location installer
.\install-service.ps1 -InstallRoot "$env:ProgramFiles\HiddenIcons"
```

卸载：

```powershell
.\uninstall-service.ps1
```

没有管理员权限时可以只安装当前用户托盘程序：

```powershell
.\install-user.ps1
```

它会安装到 `%LOCALAPPDATA%\HiddenIcons\app`，并写入当前用户的 RunKey；该模式不创建 Windows 服务。HiddenIcons 只会在存在 Tray 模式 profile 时写入/更新 `HiddenIcons.Manager` 自启动项，不会自动删除它；卸载时请自行清理 RunKey 中的 `HiddenIcons.Manager` 值。

安装脚本使用 `LocalService` 而不是 `LocalSystem`，并为 `ProgramData\HiddenIcons` 设置普通用户可修改权限，使托盘 UI 与服务共享配置。生产版应进一步把 ACL 收紧为安装时创建的专用组，并对配置中的路径做白名单校验。

## 杀毒软件与签名

程序无法“自注册为杀毒软件白名单”。要降低误报并获得厂商信任，需要：使用 EV/OV Authenticode 证书签名 EXE/MSI/脚本；保持固定发布地址和版本；不使用 DLL 注入、进程内存读写、驱动或自修改；在 Microsoft Defender Security Intelligence 等厂商渠道提交误报样本；在安装器中展示服务名称、账户、启动路径和卸载入口。任何声称可以通过代码强制加入主流杀软白名单的方案都不可靠。

## 后续工程化工作

1. 添加管理员安装器（WiX/MSIX），签名并创建专用服务账户/ACL。
2. 为服务增加命名管道 IPC，让 UI 可以查看状态、立即启动/停止和读取日志。
3. 对目标路径、参数、重启策略增加 schema 校验和审计日志。
4. 在真实 Windows 10/11、x64/ARM64、普通用户和 UAC 环境下做安装、升级、Explorer 重启、注销/登录测试。
