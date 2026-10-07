# 全屏 FPS：实现与验证

## 当前实现：2.8.0 显示 FPS 与帧节奏

2026-10-07。采用 MIT 授权的 PresentMon 2.6.0 原生分析源码（固定提交 `e13fce6acdb55a808fd8318175a56863e532d95f`），编译为随曜屏分发的 DLL，运行在现有授权采集进程中。无需另装 PresentMon、RTSS、VC 运行时或新驱动。构建与源码适配见 [集成说明](../PanelDeck.PresentMon/README.md)。

统计成功且完成显示链分析的 `Presented` 事件中的 `Displayed` 时间戳，包含可追踪生成帧，排除 `Repeated`、失败、丢弃、丢失和其他进程/已知其他窗口。接收迟到的生成帧并按显示时间排序、去重；最多保留 8 条交换链，每条 32768 个时间戳，保留最多约 30 秒历史，只选一条近期活跃链；FPS 和平均帧时间使用约 2 秒窗口。数据不足或异常显示 `—`，不使用旧提交计数器兜底。

仅开启显示与帧类型跟踪，关闭 GPU/GPU 视频耗时、输入、PC 延迟、应用时间标记、外部测量和着色器编译分析。保留显示链关联所需的系统/DWM 事件，不能沿用旧版“全部事件只针对一个 PID”的说法。ETW 缓冲为 64 KiB、最少 16/最多 128 块，禁止按 CPU 分配，1 秒投递；丢事件、缓冲丢失或帧环溢出会清空窗口并等待恢复。不开日志落盘、录制、悬浮层和高精度定时器。手机更新与亮屏租约沿用现有机制。

这是系统报告的显示时序，不是光学测量。不同驱动、图形 API、合成路径及帧生成模式仍需实测；目前不能宣称所有 DLSS 倍数均已正确覆盖，也不输出“原生/生成帧”分拆。当前输出显示 FPS、平均显示帧间隔，以及最近 30 秒最慢 1% 帧平均耗时换算的 1% Low；其他可选项见 [指标说明](presentmon-metrics.md)。

原生/托管合成测试、构建、手机连接与缺失值显示通过。后续已在《007FirstLight》动态帧生成场景读取真实数据，并完成连续游戏约 74 秒的资源采样；计数校准与性能 A/B 仍待验证，详见 [验证记录](../VALIDATION.md)。旧版测得的 35 FPS 和 0.129% CPU 不代表新版结果。

---

## 历史实现：2.6.0 提交计数（以下不描述当前版本）

2026-10-07，PC / Android 2.6.0 本地构建。以下保留当时的实现及验收证据。

## 使用

PC 与手机均升级，在 PC 点击“授权完整读取（UAC）”，再打开全屏游戏。手机底部显示 FPS、进程名及采集口径。设置 → 采集与运行可以关闭该功能。默认开启，但未授权不会自动请求 UAC。

不安装 RTSS、Afterburner、PresentMon 或额外驱动；帧率功能本身不需要 PawnIO。采集运行在既有的本次授权进程中，主程序仍为普通权限，无新增 NuGet 依赖或独立 EXE。原有 CPU / 主板硬件读数仍可能需要 PawnIO。

## 采集范围与口径

- 通过前台窗口客户区覆盖所在显示器来识别全屏，包括独占与无边框全屏、负坐标副屏；不使用游戏名单。普通最大化窗口、桌面及曜屏自身不采集。
- 自动选择一个前台进程，PID 与进程创建时间共同确定目标，切换进程或窗口重新建立计数。无法仅凭窗口区分游戏与其他全屏 3D / 视频程序。
- DXGI / D3D9 按线程关联 Present 开始和结束，只计成功的提交，排除测试、遮挡返回、失败与 DONOTFLIP。约 2 秒滑动窗口，以 QPC 事件时间计算；ETW 成批投递不会变成帧率尖峰。
- 多交换链选近期最活跃的一条，不把多个交换链帧率相加。窗口内不满足最低统计跨度、3 秒没有新帧或跟踪异常时返回空值，不以 0 冒充读数。
- Vulkan / OpenGL 等使用 DxgKrnl Present_Info 事件作兼容路径，核对 PID、目标 HWND 和返回状态；有运行时提交事件时优先使用运行时口径，避免两条路径重复相加。兼容路径显示“呈现 FPS（兼容）”，不是完整 PresentMon 显示链分析。
- 没有分析最终扫描输出、丢帧或原生/生成帧身份。由于不同插帧方案经过的提交路径不同，既不能保证排除生成帧，也不能保证覆盖所有显示帧；不能将当前计数严格称作原生 FPS 或显示 FPS。不能保证每个驱动、游戏及反作弊环境都产生可用事件。未知版本和缺失事件显示 `—`，本次未实测 Vulkan / OpenGL。

## 开销与生命周期

仅开启 DXGI 42/43/55/56、D3D9 1/2 和 DxgKrnl 184。每个提供程序同时使用 PID 与事件 ID 过滤；过滤失败直接报不可用，不退回全系统采集。DxgKrnl 仅使用 Base / Present 关键字，避免 Performance 关键字及 GPU 队列、堆栈、磁盘追踪。

ETW 缓冲区每块 16 KiB、申请 4–16 块，禁用按 CPU 分配缓冲区；每秒投递，后台阻塞消费，不提高系统计时器精度。每条路径最多保留 8 条交换链，每条 4096 个时间戳，无逐帧分配。丢事件后清空历史，暂不输出读数。

仅手机在线、PC 未睡眠且面板未手动息屏时续租采集。前台识别每秒一次，仅租约活动时运行；无需求后仅阻塞等待本地管道。请求只接受一个暂停/启用字节，不接收路径、PID 或任意命令；管道限制为启动用户及授权进程用户，拒绝网络访问。15 秒未续租自动停止，父进程退出/替换通过既有生命期管道取消任务、关闭追踪。

手机数据仍复用硬件快照和 SSE，默认约 2 秒更新。新设备状态改变会请求一次采样；硬件读取阻塞时，采集租约仍会自行到期。手机断线判断有既有心跳宽限，不承诺物理断网瞬间停止。强杀、系统崩溃的 ETW 会话残留未验证，不等于已证明任何退出路径均无残留。

## 本次验证

- .NET Release 编译成功；Windows 测试覆盖时间统计、多个交换链、无效 Present、过期、目标重置、32/64 位事件负载解码、结构布局、跨显示器全屏几何、本地管道往返、租约超时及取消。
- 合成计数测试：12 万帧约 41.29 ms，预热后分配 40 字节（计时对象）；不包含 Windows 事件生产、投递或游戏开销，不能替代游戏性能实测。
- Android APK 构建与 lint 成功；前端缺失、离线和旧 PC 兼容检查通过；九主题 542 项布局/缩放检查、70 项主题预览检查通过。
- 已安装到手机 25102RKBEC，确认当前拼贴主题的 FPS 区域与真实硬件数据可见。手机保留的旧 8081 地址及旧令牌与当前 PC 不匹配，已改为现有 PC 的 8080 连接；其余偏好保持，旧连接配置备份位于应用私有目录 `files/fps-connection-backup.xml`。实际数据走局域网，没有 ADB 转发。
- 用户完成 UAC 授权并进入 `007FirstLight`。首次观察约 112 FPS，后续游戏场景的连续 15 个读数为 34.44–35.16 FPS；手机截图实际显示 35 FPS。不同场景间的变化不作为性能回归证据。尚未与游戏内计数器校准。
- 在该游戏运行期间采样 30.27 秒：授权采集进程约占整机 CPU 0.058%，主程序约 0.071%，合计约 **0.129%**（16 逻辑处理器；换算单核约 2.065%）。工作集约 88.2 / 129.8 MiB，私有提交约 45.6 / 47.9 MiB。这是含完整硬件采集和打开的 PC 概览的两个进程总占用，不是 FPS 独立增量；也不包含可能归到游戏/系统的事件开销。未做相同场景关闭/开启 FPS 的游戏帧时间 A/B 测试。
- 已通过真实链路执行一次手动息屏 → 自动恢复：息屏读数为 null，恢复后读到 34.77 FPS，手机服务 `connected=true` 且数据继续更新。手机已留在正常在线显示状态。

本地证据：`artifacts/fps-live-probe.json`、`artifacts/fps-pause-resume.json`、`artifacts/phone-fps-2.6.0-live.png`、`artifacts/ui-2.5.0/layout-results.json`。这些测试产物不入库。

## 参考依据

- [Microsoft EnableTraceEx2：提供程序与过滤器配置](https://learn.microsoft.com/en-us/windows/win32/api/evntrace/nf-evntrace-enabletraceex2)
- [Microsoft EVENT_TRACE_LOGFILEW：实时消费、权限与原始时间戳](https://learn.microsoft.com/en-us/windows/win32/api/evntrace/ns-evntrace-event_trace_logfilew)
- [PresentMon 事件定义：DXGI](https://github.com/GameTechDev/PresentMon/blob/main/PresentData/ETW/Microsoft_Windows_DXGI.h)、[D3D9](https://github.com/GameTechDev/PresentMon/blob/main/PresentData/ETW/Microsoft_Windows_D3D9.h)、[DxgKrnl](https://github.com/GameTechDev/PresentMon/blob/main/PresentData/ETW/Microsoft_Windows_DxgKrnl.h)
- [PresentMon 对 DxgKrnl Performance 关键字的开销说明](https://github.com/GameTechDev/PresentMon/blob/main/PresentData/PresentMonTraceSession.cpp)

事件常量同时通过本机 Windows 提供程序清单核对。实现为本工程的有限计数器，不捆绑或运行 PresentMon。

## 与 RTSS 的比较

RTSS 自身的帧率监控与其 PresentMon 数据源是可切换的统计来源，因此不能仅凭“RTSS 显示的 FPS”确定口径。普通未插帧 DirectX 游戏中，提交计数通常应接近，但本工程使用约 2 秒的滑动统计和默认 2 秒推送，时间窗口及刷新不同，不保证逐次读数一致。插帧、不同数据源和内核兼容路径需要在同一场景中对照验证。本次尚未进行这样的对照。

[RTSS 官方发布说明](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/)说明可在 RTSS 自身帧率与 PresentMon 采样帧率间切换。
