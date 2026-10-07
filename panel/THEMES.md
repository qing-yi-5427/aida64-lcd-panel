# 手机面板主题

九套主题共享本地 HTML 和数据更新逻辑，无远程字体、图片或组件库依赖。时钟保持首要层级，显示 FPS 次之，帧时间与 1% Low 作为较小的辅助读数；硬件区域沿用各主题原有布局。

| ID | 设置名称 | 视觉方向 |
| --- | --- | --- |
| classic | 经典 · 仪表盘 | 黑底、琥珀 CPU、蓝色 GPU、清晰仪表分区 |
| material | Material · 安卓 | 深绿底、浅绿与淡紫强调、圆角色调卡片 |
| winui | WinUI · 微软 | 浅灰蓝底、小圆角、细边框、蓝紫强调 |
| flutter | Flutter · 清新卡片 | 浅纸色、紫粉分组、活泼卡片 |
| glass | macOS · 玻璃质感 | 深蓝紫渐变、半透明表层、静态高光 |
| editorial | 纸页 · 数据周刊 | 暖纸色、章节与细分隔线；横屏左日期栏、右规格章节 |
| ambient | 静夜 · 桌面时钟 | 大时钟、紧凑硬件条目；横屏左时钟、右硬件 |
| telemetry | 遥测 · 性能座舱 | 深色工业数据轨道、负载主数与规格清单 |
| studio | 拼贴 · 硬件工作室 | GPU 主块、独立功耗块、CPU 横条、非对称内存区 |

## 布局与读数

所有主题保留完整时分秒、星期、日期和手机电量，并展示 19 项数据：14 项硬件、2 项网速、3 项帧率相关指标。无数据、断连或过期时显示缺失占位；不绘制伪造曲线或把未知值当作零。

竖屏以 1200px 宽画布设计，高度随比例调整为 2200–3200px。纸页、静夜、遥测、拼贴支持横屏独立重排，采用宽 2000px、高 1040–1700px 的设计画布；其余五套横屏等比居中。手机遵循系统旋转设置。

FPS 区域使用主题自身的表面色、圆角和留白。竖屏保持 144px 卡高，主 FPS 为 76px，帧时间和 1% Low 为 44px；拼贴与遥测横屏使用 64px / 34px 的紧凑两行布局。纸页横屏主数在上、两个辅助数并列在下，色块高度 204px。以上为设计画布尺寸，实际显示按屏幕缩放。

数值与单位保持紧邻及基线对齐，长硬件名称截断以保留读数空间。CPU 风扇标签统一为 FAN 1，标签不会改写真实传感器来源。网速以十进制 B/s 自适应单位显示；1% Low 标注 30s，完整窗口建立前显示 `—`，部分布局附“统计中”。统计定义见 [帧率说明](../desktop/research/game-fps.md)。

## 选择、同步与预览

手机面板向上滑动后打开设置，在“面板外观”滚动选择主题。“全屏预览 4 秒”只预览，保存后生效；取消恢复已保存主题，无需重新配对。主题仅用于内置面板，外部 AIDA64 网页使用自身样式。

PC 与手机保存主题后通过现有 SSE 同步，不增加手机轮询。首次迁移由 PC 接纳手机原主题；后续以 PC 保存状态为准。手机离线保存的选择会持久保留并在重连补发，最终按 PC 收到保存操作的顺序生效。远端修改不会覆盖正在编辑的草稿，取消后恢复最新已保存主题。

`window.PanelDeck.setTheme(id)` 返回最终使用的主题 ID，未知值回退为 `classic`。浏览器默认从快照读取主题，显式 `index.html#theme=material` 固定预览草稿。Android 本地页由 APK 控制选择，快照不覆盖预览。`preview.html` 仅增加手机比例外框，内嵌原始面板，不维护另一份 CSS。

## 设计依据与实现

前五套主题共享双列布局；四套独立布局分别使用 `layout-editorial.css`、`layout-ambient.css`、`layout-telemetry.css`、`layout-studio.css`，规则限定在各自主题作用域。帧率与网速共享 `network.css`，复用原有更新、暂停和屏幕联动机制，不新增页面定时器。

参考 [awesome-design-md](https://github.com/VoltAgent/awesome-design-md) 的排版、分组和留白分析，结合手机远距阅读调整。这是社区设计分析，并非品牌官方规范。Flutter 主题表达自定义卡片风格，不代表一套独立于 Material 的官方设计规范；玻璃主题使用静态渐变与高光，不是原生 Liquid Glass，不使用持续动画或模糊合成。

以下为当时的设计调研，旧尺寸和数据数量不作为当前实现说明：[方向调研](research/design-directions.md)、[纸页与静夜](research/editorial-ambient.md)、[遥测与拼贴](research/telemetry-studio.md)。

## 验证

从仓库根目录运行：

```powershell
node desktop/test-panel.cjs
node desktop/test-panel-themes.cjs
node desktop/render-panel.cjs
node desktop/test-panel-preview.cjs
```

覆盖连接与过期、九主题切换与回退、横竖屏、窄屏、缩放、缺失值、极值、长型号及草稿同步。渲染与预览检查需要 Playwright 和 Edge；当前截图输出到 `artifacts/ui-2.8.0/`，使用示例数据，不是游戏测量。已完成的验收和真实手机结果见 [验证记录](../desktop/VALIDATION.md)。
