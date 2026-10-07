# 构建与测试

所有命令从仓库根目录执行。源码和构建脚本入库；发行安装包放在 GitHub Releases，构建缓存、设备诊断、密钥和临时截图不入库。

## 环境与构建顺序

需要 Windows、.NET 10 SDK、JDK 17+、Android SDK，以及 x64 MSVC C++20 工具集和 Windows SDK。Android SDK 路径放在不提交的 `local.properties` 或环境变量中。

首先构建随包分发的 PresentMon DLL。将下面路径替换为本机实际版本：

```powershell
./desktop/build-presentmon.ps1 -MsvcRoot "C:/path/to/VC/Tools/MSVC/version" -SdkIncludeRoot "C:/path/to/Windows Kits/10/Include/version" -SdkLibRoot "C:/path/to/Windows Kits/10/Lib/version"
./desktop/build-desktop.ps1 -FrameworkDependent
./gradlew.bat assembleDebug
```

不传原生工具链参数时，脚本查找忽略提交的 `artifacts/native-tools/`；新克隆仓库不包含这些工具。DLL 产出、源码固定版本及适配见 [原生集成说明](../desktop/PanelDeck.PresentMon/README.md)。

| 产物 | 默认位置 |
| --- | --- |
| PC 精简版 | `artifacts/PanelDeck-Desktop-Lite/` |
| PC 自包含版 | `artifacts/PanelDeck-Desktop-<项目版本>/` |
| Android Debug APK | `app/build/outputs/apk/debug/app-debug.apk` |

省略 `-FrameworkDependent` 可生成附带 .NET 运行时的自包含包。`-OutputDirectory` 可指定独立输出目录，避免覆盖运行中的版本；`-IncludeDriver` 可额外打包 PawnIO 安装程序。运行包不是源码目录，更新前应从托盘完整退出旧程序。

## 验证入口

根据修改范围运行相关检查，不必为纯文档修改重新安装应用：

```powershell
dotnet run --project desktop/PanelDeck.Tests -c Release
dotnet run --project desktop/PanelDeck.WindowsTests -c Release
./desktop/build-presentmon.ps1 -TestOnly
./gradlew.bat assembleDebug lintDebug
./desktop/test-android-policy.ps1
node desktop/test-panel.cjs
node desktop/test-panel-themes.cjs
node desktop/render-panel.cjs
node desktop/test-panel-preview.cjs
```

原生测试使用与构建相同的工具链路径参数。浏览器渲染和预览检查需要 Node.js、Playwright 与 Microsoft Edge；Playwright 必须能被 Node 解析。当前九主题渲染产物位于 `artifacts/ui-2.8.0/`，均为明确标注的示例数据。`render-panel-gallery.cjs` 是旧版四方案对比工具，不作为当前九主题验收入口。

普通测试不会安装运行时、配置防火墙或请求 UAC。真机测试使用独立夹具与临时 ADB 转发，仅用于测试，不属于产品的日常连接方式。

Android 仪器测试结束后会停止被测试应用。需重新启动 `com.paneldeck.aida/.MainActivity`，必要时使用唤醒 action `com.paneldeck.aida.WAKE_PANEL`，并检查 `dumpsys activity service com.paneldeck.aida/.DesktopLinkService` 的连接和数据更新；仅启动命令成功不能证明已恢复面板。

## 发布与验收

发布前确认两端版本、原生 DLL、第三方许可证及完整包内容一致，安装包随 Release 提供 SHA-256 校验文件。不要把配对令牌、设备配置或签名私钥放入仓库。

合成事件与布局检查不等于真实游戏兼容性或性能校准。当前已完成与尚未完成的范围见 [验证记录](../desktop/VALIDATION.md)，旧测量见 [历史索引](../desktop/research/README.md)。
