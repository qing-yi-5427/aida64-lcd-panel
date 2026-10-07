# 曜屏 · PanelDeck

一个 Windows 程序 + 一个 Android APK，让闲置手机成为桌面硬件监控面板。

## 2.8.0 本地更新

- 手机九主题加入全屏 FPS：本次 UAC 授权后，自动跟随前台独占全屏／无边框全屏程序，无需游戏名单或额外监控软件。
- 仅手机在线且面板亮屏时读取目标进程的 Windows 呈现事件，沿用数据通道；退出全屏自动暂停。
- 主数字使用 PresentMon 系统显示事件计算的显示 FPS，包含可追踪的生成帧，排除丢弃与重复显示；不支持或采集异常时显示 `—`，不退回提交 FPS。详见 [FPS 说明与验证](desktop/research/game-fps.md)。
- 九主题在原 FPS 色块内加入较小的平均帧时间与 1% Low，保留时钟优先和硬件布局。帧时间使用约 2 秒窗口；1% Low 使用最近 30 秒最慢 1% 显示帧的平均耗时换算，预热期间显示统计中。复用现有显示事件，不新增 GPU 或输入跟踪。

## 2.5.1

- PC 独立读取 CPU、GPU、内存、风扇和上下行网速，经局域网 HTTP/SSE 发送到手机；日常不依赖 AIDA64、HWiNFO 或 ADB。
- 电脑开机且未睡眠时手机保持亮屏，睡眠、关机或断连后息屏。锁屏、静置和游戏不会触发息屏；亮度由手机系统管理。
- PC 提供设置与临时码配对。主程序普通权限运行，完整硬件采集由用户主动点击 UAC 按钮授权，本次运行内有效；网速无需授权，包含局域网流量。
- 手机内置九套主题：经典、Material、WinUI、Flutter、玻璃，以及新增纸页、静夜、遥测、拼贴。所有主题保留完整时间，新增四套支持横竖屏独立布局，设置中可预览和切换。
- PC「设置 → 面板外观」支持主题选择与横竖屏预览。PC 和手机保存主题后双向同步，预览草稿不会覆盖已保存主题；“打开手机面板”默认跟随手机主题并按手机比例显示。
- PC 精简包约 2.6 MB，复用 x64 .NET 10 Desktop / ASP.NET Core 运行时；启动助手检测缺失组件，用户主动点击后才安装。

![四种新布局预览，图中为示例数据](docs/images/themes-2.5.0.png)

## 下载与使用

已发布版本在 [GitHub Releases](https://github.com/qing-yi-5427/aida64-lcd-panel/releases/latest) 下载，分别选择 `PanelDeck-PC-版本号-Lite.zip` 和 `PanelDeck-Android-版本号.apk`。APK 当前采用开发签名，供直接安装与测试，不是应用商店发行版本。主题双向同步需要两端均为 2.5.1 或更新版本。

1. 解压 PC 包，保留整个文件夹，运行 `PanelDeck.Launcher.exe`。更新前从托盘完整退出旧版。
2. 安装 APK，手机与电脑连接同一可信局域网。
3. 在 PC 设置中允许局域网连接、生成配对码，在手机设置中填入电脑地址和配对码并保存。日常 USB 仅用于充电。
4. 在手机“面板外观”中选择主题；需要 CPU 温度等完整读数时，在 PC 主界面主动点击“授权完整读取（UAC）”。

完整使用方法、HyperOS 权限与边界见 [电脑端说明](desktop/README.md)；样式说明见 [主题文档](panel/THEMES.md)；验证结果见 [验证记录](desktop/VALIDATION.md)。

## 构建两端

需要 Windows、.NET 10 SDK、JDK 17+ 和 Android SDK；原生分析库还需要 x64 MSVC C++20 与 Windows SDK，参见 [原生构建说明](desktop/PanelDeck.PresentMon/README.md)。Android 本地 SDK 路径配置在忽略提交的 `local.properties` 或环境变量中。

```powershell
./desktop/build-presentmon.ps1
./desktop/build-desktop.ps1 -FrameworkDependent
./gradlew.bat assembleDebug
```

PC 输出到 `artifacts/PanelDeck-Desktop-Lite/`，APK 输出到 `app/build/outputs/apk/debug/app-debug.apk`。省略 `-FrameworkDependent` 可构建附带运行时的 PC 包。源码、测试和构建脚本入库，安装包放在 Releases；构建缓存、设备数据、密钥及临时截图不入库。

本次本地最新 PC 目录为 `artifacts/PanelDeck-Desktop-2.8.0-Lite/`，手机安装包为 `artifacts/PanelDeck-Android-2.8.0.apk`。旧清理脚本仍以 `PanelDeck-Desktop-Update` 为保留目录，版本不一致时会停止；本次没有运行清理。

## 旧版 AIDA64 网页面板模式

以下为仍保留的兼容模式，可在手机设置中切回。原始 `.rslcd` 模板仍保留，过时的设计预览已清理。

一个面向闲置 Android 手机的 AIDA64 RemoteSensor 专用全屏显示器。它按工作日、周末和中国大陆法定节假日自动控制面板常亮/息屏。

<p align="center">
  <img src="AIDA64-LCD-1200x2608-Chronograph-preview.png" alt="曜屏 AIDA64 面板预览" width="320">
</p>

## 下载

前往 [GitHub Releases](https://github.com/qing-yi-5427/aida64-lcd-panel/releases/latest) 下载最新 APK。小米/HyperOS 用户安装后请连接 ADB，并运行仓库根目录的 `configure-hyperos-adb.ps1`。

## 已实现

- 沉浸式全屏显示网页，不保留任何常驻浏览器工具栏。
- 在面板上向上滑动才显示悬浮设置按钮；按钮 5 秒后自动隐藏。
- 向下滑动立即隐藏悬浮设置按钮。
- 支持双指放大、缩小，网页即使原本禁止缩放也会由应用重新启用。
- 顶部显示手机实时电量与充电状态；低电量、充电和正常状态使用不同仪表色提示。
- 设置提供面板主题、面板地址、重新加载与屏幕计划；亮度由手机系统管理。
- 支持局域网明文 HTTP，适配 AIDA64 RemoteSensor。
- 工作日默认 `02:00–20:30` 亮屏，其余时间息屏。
- 周末/法定节假日默认 `02:00–08:00` 亮屏，其余时间息屏。
- 周六、周日自动按休息日处理；中国大陆法定节假日和调休工作日每周自动同步。
- 内置国务院公布的 2026 年安排作为离线数据，断网时继续正常执行。
- 息屏采用纯黑遮罩并释放常亮锁，让 Android 自然关闭显示；恢复时通过系统允许的唤醒入口显示面板，始终使用系统亮度。
- 息屏时长按屏幕可临时唤醒 10 分钟。
- 开机及系统时间变化后自动重新安排计划。

## 构建

```powershell
./gradlew.bat assembleDebug
```

APK 输出到 `app/build/outputs/apk/debug/app-debug.apk`。

## 首次使用

1. 首次打开会自动显示设置；以后在面板页面向上滑动，再点击右下角悬浮按钮。
2. 将面板地址改为当前 AIDA64 RemoteSensor 地址，例如 `http://192.168.1.20:8080/`。
3. 点击“开启精确定时权限（推荐）”，在系统设置中允许。
4. 小米/HyperOS 设备连接 ADB 后运行 `./configure-hyperos-adb.ps1`，一次性允许锁屏显示、后台启动和忽略电池优化。

Android 16 上，HyperOS 的“后台弹出界面”授权可能仍不足以允许后台启动面板。Android 端 2.5.2 增加“自动打开面板”授权入口：在手机设置中允许曜屏“显示在其他应用上层”，或在明确同意此权限后运行 `./configure-hyperos-adb.ps1 -AllowBackgroundLaunch`。此权限用于系统支持的后台启动例外；程序不会创建悬浮窗，也不会在手机已解锁并使用其他应用时抢占前台。

HyperOS 还需在曜屏应用详情中将省电策略设为“无限制”。Android 的电池优化白名单与 HyperOS 的进程冻结策略不同：真机测试曾出现已有前台服务和白名单、仍被 `GreezeManager` 以 `tobg` 原因冻结的情况。手机设置中的“后台运行与省电设置”可打开应用详情。现有兼容模式也不能代替这项系统设置。

手机自动化测试结束后，Android 会停止被测试应用。测试完成必须重新启动 `com.paneldeck.aida/.MainActivity`（唤醒 action 为 `com.paneldeck.aida.WAKE_PANEL`），并使用 `dumpsys activity service com.paneldeck.aida/.DesktopLinkService` 确认 `connected=true`、数据持续更新；仅看到启动命令成功不能视为后台恢复。若手机停留锁屏，还需检查后台启动权限和实际前台页面。

## Android 限制

普通第三方应用不能无条件绕过 PIN/图案/密码；本应用也不会移除设备凭据。它会在计划结束时通过 `AlarmClock` 唤醒屏幕，并以系统允许的锁屏显示窗口直接展示面板。小米/HyperOS 必须额外授予“锁屏显示”权限，脚本已包含该配置。
