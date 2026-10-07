# 2.2 可靠性复查

> 历史记录：以下保留当时的方案、环境与验收状态，不作为当前使用说明或性能结论。当前版本请看 [使用指南](../README.md) 和 [验证记录](../VALIDATION.md)。

最终规则按用户要求简化：电脑开机且未睡眠即亮屏，电脑锁屏和输入空闲不参与判断。保留显式手动息屏。真实断电无法提前可靠发消息，手机以 45 秒无有效数据作为兜底。

| 问题 | 已实施处理 |
|---|---|
| 传感器读数阻塞亮灭屏 | 电源状态和硬件采样分开运行；阻塞采样的隔离测试通过 |
| 网络异常结束后立即重连导致忙循环 | 完整 SSE 事件解析，EOF/半包视为失败；1～15 秒重试退避，网络变更可中断等待 |
| 每分钟重建网络连接 | 改为持续 SSE；手机每约 10 秒报告电量和在线状态 |
| PC 与手机时钟不一致误判数据过期 | 电脑计算采样年龄，网页使用 performance.now 计算本地经过时间 |
| 电脑端自发写心跳导致误报手机在线 | 在线状态由手机请求确认，服务器发数据本身不算手机回执 |
| 手机持续持有 CPU 唤醒锁 | 默认关闭持续锁，保留仅充电且连接时的可选兼容模式 |
| 重连/旧连接之间竞态 | 使用连接代次，过时连接的数据不能覆盖当前会话 |
| 配对长期有效或泄露后无撤销入口 | 临时码 5 分钟、单次使用、最多 20 次尝试；设置增加撤销手机；令牌不进入硬件导出，APK 禁止备份 |
| 配置损坏导致退出 | 保留恢复副本，加载默认值并明确提示重新配对；日志轮换且不写令牌或响应正文 |
| 首次配置依赖手动命令 | PC 设置中主动触发一次配置；平时无提权。驱动复用、服务文件保护、安装前预检 |
| 联动模式仍显示旧时段设置 | 手机隐藏 AIDA64 时段、节假日及旧亮度选项，停止该模式下的节假日网络同步 |

## 参考资料

- Android [自动备份](https://developer.android.com/identity/data/autobackup)：仅 allowBackup=false 在部分厂商系统上仍允许换机迁移，因此额外排除 SharedPreferences，避免配对令牌随设置迁移。

- Android [网络状态与回调](https://developer.android.com/develop/connectivity/network-ops/reading-network-state)：网络变化使用回调通知，避免轮询整个网络状态。
- Android [唤醒锁实践](https://developer.android.com/develop/background-work/background-tasks/awake/wakelock/best-practices)：约束锁的持有时间，避免不必要的持续 CPU 唤醒。
- Android [Doze 与应用待机](https://developer.android.com/training/monitoring-device-state/doze-standby)：息屏后的后台网络受系统节能策略影响，不能仅凭前台服务保证所有系统都及时恢复。
- Microsoft [NamedPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0)：本机采集服务与普通权限界面使用命名管道；以隔离测试验证关闭后完整读出与取消。

这些资料解释实现取舍，不代替本机 Wi-Fi、实际物理息屏及新服务安装验收。风扇映射另见 fan-mapping.md。
