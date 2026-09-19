# CPU 风扇通道识别调研

调研日期：2026-09-18。对象：GIGABYTE B650E AORUS ELITE X AX ICE、ITE IT8689E、LibreHardwareMonitorLib 0.9.6。仅查阅源码、官方资料、已有采样文件，以及普通权限只读 WMI；没有启动 AIDA64/HWiNFO/Fan Control，没有访问驱动、触发 UAC、操作 UI 或修改风扇。

## 结论

1. **不能根据最高转速判断 CPU_FAN。**转速是运行状态，接口名称是主板布线关系。本机已有采样中 Fan #1 约 1455 RPM、Fan #6 约 1439 RPM，两者非常接近；根据最大值选择会随风扇曲线和负载变化而切换对象。
2. **AIDA64、HWiNFO 也需要按主板适配。**公开的官方支持记录显示，接口名称错误需要主板型号、底层调试报告和软件修正，没有一个通用的“最高转速就是 CPU 风扇”的识别规则。
3. **Fan #1 → CPU_FAN 有较强的同系列证据，但本机仍应标为推定。**LibreHardwareMonitor 的相关映射曾参考 HWiNFO 校验；然而 0.9.6 的型号白名单是 `B650E AORUS ELITE AX ICE`，缺少本机名称中的 `X`，因此本机未命中。缺字可能是上游型号录入遗漏，但没有精确型号的实机对照记录，不能当作已证实。
4. **无需安装另一个监控软件解决标签问题。**继续复用 LHM，保存主板配置档、稳定传感器 ID 和映射来源，设置中允许手动确认或改选即可。对当前 CPU_FAN 可以显示“Fan #1（推定 CPU_FAN）”；不要悄悄当作已确认。

## 软件各自怎么处理

| 软件 | 可核验的实现或官方处理方式 | 对本项目的意义 |
| --- | --- | --- |
| LibreHardwareMonitor | 开源代码先识别主板厂家和型号，再在 SuperIO 分支中为各数值通道指定标签；匹配失败显示通用编号 | 直接复用，增加精确型号的配置档和映射置信状态 |
| HWiNFO | 官方论坛中，Gigabyte 主板风扇名称错误由作者索取 Debug File，定位问题后发布修复版 | 支持“按主板维护映射”的判断；内部数据库闭源，不能声称已经取得其本机映射表 |
| AIDA64 | 官方 FAQ 指出传感器寄存器布局没有统一标准；名称或读数不正确时提交主板型号和传感器信息。CPU 风扇误标成机箱风扇的支持案例要求 ISA Sensor Dump | 依赖硬件适配和诊断资料，换成 AIDA64 也不代表无需映射 |
| Fan Control | 官方仓库明确主程序闭源，主要硬件后端是 LHM；文档提供手动或自动配对转速传感器、校准风扇行为 | 可借鉴设置界面的显式选择；配对“控制通道与转速通道”不等同于证明 PCB 上的 CPU_FAN 名称 |

来源：[LHM 主板入口源码](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/Motherboard.cs)、[HWiNFO 作者修复 Gigabyte 风扇标签案例](https://www.hwinfo.com/forum/threads/hwinfo-showing-wrong-motherboard-fans.9715/)、[AIDA64 官方 FAQ](https://www.aida64.com/support/faq/inaccurate-sensor-data)、[AIDA64 CPU 风扇误标案例](https://forums.aida64.com/topic/21598-fixed-case-fans-missing-cpu-fan-labeled-as-case-fan-asrock-b850-riptide-wifi/)、[Fan Control 官方仓库](https://github.com/Rem0o/FanControl.Releases)、[Fan Control 官方配对文档](https://getfancontrol.com/docs/)。

## 精确型号与通道证据

LHM 0.9.6 的 [Identification.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/Identification.cs) 使用不区分大小写的完整字符串比较，含 `B650E AORUS ELITE AX ICE`，不含 `B650E AORUS ELITE X AX ICE`。本机实际 SMBIOS 型号为后者，因此通用风扇标签是匹配失败的合理结果。

相关 [PR #1415](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/1415) 于 2024-08-08 合入。作者说明 WiFi/AX、V2、ICE 使用相同布局，并使用 HWiNFO 对照。这是同系列配置的来源，不能扩张为“已经验证所有后续主板修订”。

[SuperIOHardware.cs（v0.9.6）](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/SuperIOHardware.cs) 中，上述 B650(E) AORUS ELITE 配置的 IT8689E 风扇顺序如下：

| 本机稳定传感器 ID | 当前通用标签 | 上游同系列接口映射 | 已有采样 RPM |
| --- | --- | --- | ---: |
| `/lpc/it8689e/0/fan/0` | Fan #1 | CPU_FAN | 1454.74 |
| `/lpc/it8689e/0/fan/1` | Fan #2 | SYS_FAN1 | 711.27 |
| `/lpc/it8689e/0/fan/2` | Fan #3 | SYS_FAN2 | 831.28 |
| `/lpc/it8689e/0/fan/3` | Fan #4 | SYS_FAN3 | 0 |
| `/lpc/it8689e/0/fan/4` | Fan #5 | SYS_FAN4_PUMP | 0 |
| `/lpc/it8689e/0/fan/5` | Fan #6 | CPU_OPT | 1439.23 |

RPM 来自已存在的 `artifacts/hardware-probe-admin.json`，不是本次重新启动监控采集。表中“上游同系列接口映射”均为本机候选关系。0 RPM 本身不能证明接口没有接线：也可能是停转、无测速回传或当前读数状态。

技嘉该精确型号的 [官方手册](https://download.gigabyte.com/FileList/Manual/mb_manual_b650e-aorus-elite-x-ax-ice_1101_e.pdf) 第 21–22 页列出 CPU_FAN、SYS_FAN1/2/3、SYS_FAN4_PUMP、CPU_OPT 六个接口，接口集合与此映射一致；手册没有公布它们对应 IT8689E 的内部通道序号。因此“接口集合吻合”是辅助证据，不能单独证明顺序。PDF 网页全文提取不稳定，以上接口信息由官方文件的搜索索引和官方规格页交叉核对。

HWiNFO 官方论坛有 [该精确型号的 SDK 采样延迟帖子](https://www.hwinfo.com/forum/threads/high-latency-in-hwinfo-status-for-gigabyte-b650e-aorus-elite-x-ax-ice.10134/)，但没有公开风扇映射。此次未查到 AIDA64 对该精确型号公开的通道表，也没有取得 HWiNFO/AIDA64 在这台机器上同时采样的对照记录，不能据此宣称已完成精确适配验证。

## SMBIOS / WMI 能否直接识别

SMBIOS Type 2 能提供主板厂家、产品、版本，用来选择软件维护的配置档。它不会普遍提供 IT8689E 的通道编号。SMBIOS Type 27 可以描述冷却设备、额定转速、文本说明和温度探头关联；这也不是一个保证存在的 SuperIO 通道映射表。字段定义可查 [TianoCore EDK II 的 SMBIOS 3.8 类型定义](https://github.com/tianocore/edk2/blob/master/MdePkg/Include/IndustryStandard/SmBios.h)。

本次普通权限只读核查得到：

| 项目 | 结果 |
| --- | --- |
| Win32_BaseBoard.Manufacturer | Gigabyte Technology Co., Ltd. |
| Win32_BaseBoard.Product | B650E AORUS ELITE X AX ICE |
| Win32_BaseBoard.Version | x.x |
| Win32_Fan 实例数 | 0 |
| 原始 SMBIOS 表长度 | 2480 字节 |
| Type 27 / Type 34 / Type 35 结构数 | 均为 0 |

因此，本机没有可直接用于风扇映射的这些标准结构，`x.x` 也不能用于区分 PCB 修订版。不要因 WMI 查询成功就把它当作有完整硬件遥测能力。[Microsoft 的 Win32_Fan 文档](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-fan)还明确区分请求转速 `DesiredSpeed` 与测速传感器读到的实际转速。

## 建议落地方式

1. 映射键至少包含厂家、精确型号、芯片和稳定传感器 ID。已确认的 PCB 修订版可以加入配置条件；本机版本 `x.x` 不能被当作具体修订版。
2. 优先级为“用户确认并保存的选择 → 精确验证的配置档 → 同系列推定建议 → 未指定”。不要按 RPM 排序、CPU 温度相关性或风扇编号通用规则自动提升为确定结果。
3. 设置界面列出原始名称、实时 RPM、候选接口和映射来源；允许保存 CPU 风扇的传感器 ID。用户选定某通道后，通道丢失应显示不可用，不能悄悄切换到当前最快的风扇。
4. 当前可以保留通道 0 的候选建议，但主页面或设置中明确显示“推定，待核对”。用户确认“散热器插 CPU_FAN”只说明物理接线，尚未确认 CPU_FAN 对应哪个软件通道。
5. 最终确认可在用户方便时与 BIOS Smart Fan 6 的 CPU_FAN 读数对照，或读取用户主动打开的 HWiNFO/AIDA64/GCC 报告。同一次采样多个接口读数若非常接近，单个数字相近只能作为辅助证据，需要再核对接口/调试记录。不要为此自动启动软件、重启进 BIOS、拉满/停止风扇或做负载测试。
6. 若之后取得精确型号和修订版证据，可向 LHM 上游提交型号字符串修正及只读映射验证结果。当前不建议直接将整套电压、温度、PWM 控制映射也一并套用：显示一个 RPM 标签的推定不构成写入控制寄存器的依据。

置信度：通用标签源于型号匹配缺口——高；同系列通道 0 被映射 CPU_FAN——高；本机通道 0 确实连接 CPU_FAN——中等偏高、尚待精确验证；“最高 RPM 就是 CPU_FAN”——无可靠依据。
