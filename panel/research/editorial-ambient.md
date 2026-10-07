# 纸面 / 夜桌布局设计

> 历史设计调研：以下记录当时的参考与布局演进，旧版尺寸、数据数量及截图不作为当前规范。九主题当前布局与验证入口见 [主题说明](../THEMES.md)。

2026-09-19。只修改 `layout-editorial.css` 与 `layout-ambient.css`，使用既有 DOM 和数据绑定，所有规则均限制在各自 `body[data-theme]` 下。

## 参考来源与采用范围

- [awesome-design-md / WIRED](https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/wired/DESIGN.md)：参考以字体层次、章节和细分隔线构成界面的思路；不复刻商标、品牌字体或网站界面。
- [awesome-design-md / Notion](https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/notion/DESIGN.md)：核对中性色、表格行和暖灰表面。该文档当前偏官网营销视觉，纸面方案不是“Notion 官方规范”。
- [awesome-design-md / Apple](https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/apple/DESIGN.md)：参考单一主视觉、低装饰和大留白的层次；夜桌把桌面时钟作为主视觉，不使用品牌资源、图片、外部字体或磨砂模糊。

## editorial / 纸面

暖纸背景、墨色数字，少量土红与墨绿区分 CPU/GPU。竖屏是从上至下的 CPU/GPU 章节，每章有两行规格条目；功耗与内存作为独立账目条带放在底部。没有旧版两张竖卡。横屏为左侧时间日期栏，右侧两条四项规格章节及底部汇总。

大数值使用系统无衬线字体，只有时钟、CPU/GPU 章节标题使用系统衬线字体。保持数值易读，避免装饰字体影响频繁扫描。

## ambient / 夜桌

近黑底、降低发光面积与文字亮度，主时钟居中，硬件是下方紧凑双行数据带。横屏则把大时钟放左边、数据放右边。所有 14 项原始指标继续可见；仅隐藏重复表达利用率的装饰环。

夜桌改变视觉亮度，不写入系统或手机亮度设置，也不改变自动亮度。

## 排版与性能约束

- 数值与单位共同处在 flex baseline 排版内，间距统一为 7 设计像素。
- 单位字号均小于数值字号的 60%。长型号最多两行，横屏纸面为单行省略，避免挤压指标。
- 不添加网络请求、字体下载、图片、动画、额外计时器、模糊滤镜或虚构曲线。
- 缺失值沿用 `—`；离线状态沿用原有 stale 样式，RAM/VRAM 百分比和实际值保留。

## 验证

隔离 headless Edge 检查两个主题在 1200×2200、1200×3200、2000×1040、2000×1700 画布的数值可见性与容器溢出，共 8 种组合，未发现溢出。输入含长硬件名称、100%、9999 RPM、6000 MHz、499.9 W、100.0 GB。另检查中间尺寸 1200×2800、2000×1500。

已人工查看纸面竖屏、夜桌横屏及竖屏截图。截图位于 `artifacts/theme-agent/`。未使用 computer use，未打开可见 PC 窗口，未触发 UAC。
