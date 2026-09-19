# 低占用网速监控

实现日期：2026-09-19。网速在主进程既有硬件采样循环中取得，普通采集与完整授权模式都可用，无需新驱动、管理员权限、外部程序或独立定时器。

## 方案与口径

Windows IP Helper API 的 `GetIfTable2` 一次返回全部接口的 64 位累计收发字节数及物理接口标志。以 `Stopwatch` 单调时钟计算相邻两次计数差，产生 `netDownload`、`netUpload` 两个指标，协议单位均为 `B/s`。

相较 `.NET NetworkInterface.GetAllNetworkInterfaces()` 再逐卡 `GetIPStatistics()`，此处原生一次枚举既减少调用，也可使用系统的 `HardwareInterface` 标志区分虚拟层，无须依赖设备名关键字。仅统计处于 Up 状态的物理以太网和 Wi-Fi，排除过滤器、VPN/VMware/Hyper-V 虚拟接口、回环、隧道、端点、断线和低功耗接口。同一接口按 LUID 去重；虚拟 VPN 流量只在实际承载它的物理网卡上计入一次。

**这是活动物理网卡吞吐量，包含局域网和曜屏自身通信，不是纯互联网下载速度，也不按应用区分。** 多张活动物理网卡独立求和，来源字段列出参与统计的网卡名称。未以“具有默认网关”为过滤条件，因为局域网专用接口同样有真实流量，VPN 也可能改变默认路由。在网桥/双物理接口转发等特殊拓扑中，同一包可能实际穿过两张物理卡；汇总口径会计入两次物理传输。这不等同于主机应用净流量。

## 异常与生命周期

- 首次采样、接口集合变化或索引变化：建立新基线，显示不可用，不用累计流量伪造速率。
- 计数回退或归零、读取失败：丢弃旧基线；下一个正常间隔恢复。
- 休眠/唤醒通知主动清除基线；间隔小于 250 ms、大于 90 s 或时钟异常也清除，避免尖峰。
- 没有有效物理接口：值为 `null`；有效接口确实没有收发时才为 `0`。
- 当前采样周期默认显示时 2 秒、后台 15 秒，遵循现有配置与节电机制。显示的是采样间隔内平均速率。
- Windows 分配的表在 `finally` 中调用 `FreeMibTable` 释放，不保留数据历史。

## 验证

`dotnet run --project desktop/PanelDeck.WindowsTests -c Release` 通过。新增测试覆盖首次/空闲、双接口求和、接口增删、索引改变、计数回退、超长/超短/负间隔、睡眠重置、64 位高值精度、原生读取失败恢复、虚拟与异常状态过滤，以及 Controller 字段接入。

本机只读验证发现 1 张符合条件的活动物理接口；其名称、类型和累计计数与 `.NET GetIPStatistics()` 对照一致。未输出 IP、MAC、凭据，未修改网络配置、未主动制造流量，也未打开 PC 窗口或触发 UAC。

本机隔离微基准（100 次连续原生读取）共 **79.91 ms**，每次约 **215 B 托管分配**，即约 **0.8 ms/次**。这只是本机 API 调用成本，不代表整个进程内存或长期 CPU 基准。最初使用 `Marshal.PtrToStructure` 导致每次约 74 KiB 分配，最终已改为直接按结构偏移读取必要字段，避免为大量虚拟接口复制和装箱完整结构。

## 官方参考

- [Microsoft GetIfTable2：枚举物理/逻辑接口与释放要求](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getiftable2)
- [Microsoft MIB_IF_ROW2：HardwareInterface、OperStatus、InOctets、OutOctets 和接口标识](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/ns-netioapi-mib_if_row2)
- [Microsoft IPInterfaceStatistics：接口累计收发字节](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipinterfacestatistics?view=net-10.0)

显示整合由手机面板完成，无需更改配对或通信端口。
