# 曜屏 2.4.2 内存优化

> 历史记录：以下保留当时的方案、环境与验收状态，不作为当前使用说明或性能结论。当前版本请看 [使用指南](../README.md) 和 [验证记录](../VALIDATION.md)。

2026-09-19。没有操作用户的 PC 窗口、弹 UAC 或停止正在使用的曜屏。验证使用普通权限、隔离端口和独立生命期管道，不写入用户设置。RTSS / RTSSHooksLoader 已无运行进程；旧曜屏里的注入模块仍可能保留到该进程退出。

## 原因与修复

此前 238.19 MiB 是两个旧进程的私有提交总量，不等于独占驻留物理内存。上一轮只查应用层，漏掉了硬件库内部历史和 RTSS 全局注入。本轮补充核实：LHM 0.9.6 Sensor 默认保存一天历史；当前 RTSSHooks64.dll 的 `.data` 段约 51.87 MiB。模块大小本身不能当作实际节省的物理 RAM。

只切换工作站 GC 的同类模拟测试仅节省约 15 MiB。增加 RTSS 排除配置、在 `[STAThread]` 入口内设置策略，也没有解决注入：STA 初始化发生在 Main 之前。改成普通入口先设置本进程策略、再创建 STA UI 线程后，同类后台通信测试从约 73 MiB 降至约 22 MiB，模块清单也确认无 RTSS。临时排除配置已清理，既有 RTSS 配置摘要未改变。

正式实现包含：

- 通过 Windows 进程策略禁止本进程的旧式扩展点，不修改游戏或全局系统策略，不强卸载其他程序 DLL。现代 TSF 输入法可用；需要旧式 IME/钩子时可用启动环境变量 `PANELDECK_ALLOW_LEGACY_HOOKS=1`。
- 基础采集也由同一 EXE 的普通权限子进程承担，主程序不加载 GPU 监控库。主动 UAC 成功后替换基础进程；取消授权保留基础进程。没有新增安装组件或常驻服务。
- 随机名字的生命期管道只传达 EOF 退出，不执行命令。父用户与 SYSTEM 完全访问，管理员仅只读以支持跨账号 UAC，拒绝网络身份。授权等待不持锁，代次号保证基础启动不会覆盖较新的授权启动，释放后不能发布新 worker。
- 托盘启动不创建主窗口；打开才创建，关闭释放控件，后台连接保持。最小化停 UI timer，恢复立即取样。修复重复 Dispose 和授权期间关窗后访问已释放控件。
- LHM 历史窗口设为零，仅保存当前值；缓存传感器元数据，避免反复生成相同字符串。风扇选择及导出仍有当前全部读数。
- 不变状态复用快照；SSE 检查不再构造无用快照/字符串；管道直接序列化 UTF-8，读取用可归还缓冲，保留 256 KiB 限制和 1.5 秒超时。
- 最终 GC 配置为 Server=true、HeapCount=1、ConserveMemory=9、NoAffinitize=true。比较了工作站 GC 和硬性 64 MiB 堆预算，最终不采用硬性上限。不调用 GC.Collect，也不清空工作集制造低读数。
- 保留旧服务重试；收到新鲜服务样本后退出基础 worker，避免重复持有硬件库。

## 测量

隔离测试调用产品的 Controller、LanServer、SessionCollector、CollectorService 和 HardwareMonitor。真实基础采集，2 秒间隔，连续 SSE 与 10 秒手机心跳；随机 loopback 端口，不创建可见窗口/托盘图标，不读写用户配置。统计主程序与实际采集子进程。测试器还包含计数器采样和模拟手机客户端，其开销并非零。

最终候选六分钟测试，排除前约 30 秒后有 66 个完整双进程记录：私有提交均值 **66.61 MiB**，范围 **56.87–78.20 MiB**，末值 **76.14 MiB**；总工作集合计均值 **150.36 MiB**，包含共享页重复。整机 CPU 约 **0.096%**（16 逻辑处理器）。111 个传感器项、14 个面板指标正常；权限不足项明确为空。主程序未加载 NVML 或 RTSS。

此前两分钟测试均值为 58.05 MiB，不能作为长期常驻值。六分钟内主进程尚未经历首次 GC，提交量整体仍有上升，**本轮没有证实稳定平台或完整回收周期**。不能将这组数字描述成长期占用上限，或宣称已排除泄漏；未通过强制 GC 或工作集清空制造低读数。

原始数据：项目 `artifacts/memory-measurements/release-basic.json`、`release-processes.json`、`release-summary.json`。较早两分钟数据保留为 `single-heap-basic.json`、`single-heap-processes.json`、`single-heap-summary.json`。

## 验证与边界

LAN 配对/认证、心跳、SSE、电源联动检查通过。管道 20 次连续循环、中文大帧扩容、超限拒绝和取消检查通过。采集阻塞时屏幕状态仍响应，窗口恢复立即请求新样本，相同状态不重复分配。父退出及生命期关闭取消 worker；重复清理安全。98 项真实隐藏窗口检查通过，144 DPI，无窗口显示。Release 发布无警告、无错误。

子代理复核已处理授权并发覆盖、跨账号管道 ACL、关闭后访问控件和重复释放问题。没有做实际跨账号 UAC、完整授权内存复测、游戏帧时间对照或小时级泄漏测试。旧 238.19 MiB 与新版基础采集模式不同，不能据此计算严格的纯代码节省百分比。竞品同口径数字未知，见[对比报告](monitor-memory-comparison.md)。

## 依据和交付

- [LHM 0.9.6 Sensor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Sensor.cs)：默认一天历史、零时间窗口停止累积。
- [Microsoft 进程策略](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessmitigationpolicy)：在初始化阶段设置，只影响本进程。
- [Microsoft GC 配置](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector)：堆数、节约内存和硬性上限的定义。

发布包 `artifacts/PanelDeck-Desktop-2.4.2/PanelDeck.exe`。现有 Android 2.4.1 APK 协议兼容。没有自动重启旧程序；必须完整退出旧版后，新实现才会生效，旧注入模块才会释放。
