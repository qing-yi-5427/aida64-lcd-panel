# 实现调研与历史记录

当前使用入口在 [电脑端说明](../README.md)，当前实测结果在 [验证记录](../VALIDATION.md)。历史文档保留当时的设计、环境和证据，旧版路径、计数口径及待验收状态不代表当前版本。

## 实现专题

- [显示 FPS 与帧节奏](game-fps.md)：当前采集与统计。
- [PresentMon 指标](presentmon-metrics.md)：已接入和可扩展范围。
- [网速监控](network-monitor.md)：计数方案及当时的接口微基准。
- [风扇映射](fan-mapping.md)：特定主板的接口调研与未确认项。
- [原生 PresentMon 集成](../PanelDeck.PresentMon/README.md)：固定源码、构建和桥接测试。
- [手机主题](../../panel/THEMES.md)：当前布局与主题同步。

## 历史设计与测量

| 文档 | 历史范围 |
| --- | --- |
| [2.6.0 提交帧率](game-fps-2.6-history.md) | 已替换的有限提交计数器，不能用于解释当前显示 FPS |
| [2.5–2.7 及 2.4.2 补充验收](validation-2.5-2.7-history.md) | 主题、同步、FPS 演进和旧环境检查 |
| [2.4.1 验收](validation-2.4.1-history.md) | PC 窗口与手机对齐 |
| [2.4 验收](validation-2.4-history.md) | 早期五主题与界面 |
| [2.3 验收](validation-2.3-history.md) | 早期连接与授权 |
| [2.4.2 内存优化](memory-optimization-2.4.2.md) | 优化前后的当时测量 |
| [监控工具内存对照](monitor-memory-comparison.md) | 不同配置下的历史观察，不作当前排名 |
| [PC 性能检查](pc-performance-review.md) | 2.4.1 的资源占用，不代表 2.8.0 |
| [可靠性设计](reliability.md) | 2.2 阶段的设计与验收 |
| [PC 界面设计](desktop-ui.md) | 早期方案，含后续已改变的标题栏设计 |
| [手机设计方向](../../panel/research/design-directions.md) | 四套独立布局的选型 |
| [纸页与静夜](../../panel/research/editorial-ambient.md) | 设计来源与最初实现 |
| [遥测与拼贴](../../panel/research/telemetry-studio.md) | 设计来源与布局演进 |

历史文档中的 `artifacts/` 路径指未入库的本地诊断产物；新克隆仓库不会包含这些文件。源码与文档的 Git 历史保留完整修改过程。
