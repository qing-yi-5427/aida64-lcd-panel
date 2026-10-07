# Telemetry / Studio design notes

> 历史设计调研：以下记录当时的参考与布局演进，旧版尺寸、数据数量及截图不作为当前规范。九主题当前布局与验证入口见 [主题说明](../THEMES.md)。

Implemented as independent scoped CSS files after `panel.css`, using the existing 14-metric data contract. No HTML or JavaScript changes are required by either skin. All decorative elements are static; all visible measurements and memory tracks use existing real sensor values.

## References actually inspected

The requested [awesome-design-md collection](https://github.com/VoltAgent/awesome-design-md) contains third-party analyses of brand websites. These are design references, not official brand specifications and not instructions to copy branding.

- [IBM DESIGN.md](https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/ibm/DESIGN.md): flat surfaces, grid discipline, rules instead of shadows, readable technical hierarchy. Telemetry adopts a measured spacing system and separated data channels; it does not copy IBM blue, logos or fonts.
- [BMW M DESIGN.md](https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/bmw-m/DESIGN.md): restrained dark framing, precise rectangular boundaries and strong display versus supporting typography. Telemetry uses these principles in a monitoring context with original lime/cyan accents. No M stripes or automotive photos are used.
- [NVIDIA DESIGN.md](https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/nvidia/DESIGN.md): technical content hierarchy, a dark focal area contrasted with supporting surfaces, and compact factual grids. Studio uses an original warm canvas, forest-green GPU hero and terracotta power tile. It does not reproduce NVIDIA colors or identity.

## Layouts

### Telemetry — 遥测轨道

CPU and GPU each occupy a full-width lane. Portrait mode places utilization in a large left cell and three measurements in aligned rows to the right, making the numbers readable without shrinking four side-by-side measurements. Landscape mode turns each lane into a horizontal instrument rail with the hardware identity, utilization, temperature, fan and clock in separate columns. Power and memory form a compact lower strip. Thin bottom tick marks are purely frame decoration, with no implied numerical chart.

### Studio — 工作室拼贴

The GPU is the main dark block; GPU and CPU power are separate stacked tiles beside it. A full-width CPU strip anchors the composition below. RAM and VRAM occupy differently sized pastel blocks. In landscape, the GPU becomes a wide left hero and the four support blocks form a right-hand square, with CPU across the bottom. This changes information priority and composition instead of only changing the old two-column skin.

## Readability and cost

- System fonts only; no downloaded fonts or images.
- No filter, blur, animation, transition, new timer or fake graph.
- Every `strong` and unit stays in the same baseline flex group; 6–8 design-pixel gap and units below 60% of value size.
- Long hardware models wrap. Four-digit fan/clock readings and three-digit utilization are supported. Missing values retain the same geometry.
- Selectors are scoped to `body[data-theme="telemetry"]` or `body[data-theme="studio"]`.
- Landscape canvas: 2000 px wide, checked from 1040 through 1700 px tall. Portrait canvas: 1200 px wide, checked from 2200 through 3200 px tall.

## Validation

Isolated headless Edge rendered both themes at 1200×2200, 1200×2608, 1200×3200, 2000×1040, 2000×1300, 2000×1700, 393×852 and 1600×900. Inputs included 100%, 9999 RPM, 6000 MHz, 999.9 W, 99.9 GB and long CPU/GPU model names. Checks covered viewport clipping, clipped ancestors, label/reading overlap and value/unit spacing. Screenshots were visually inspected; the root task runs the full nine-theme regression and missing/stale-state tests.

Local screenshots: `artifacts/theme-agent/telemetry-393-852.png`, `artifacts/theme-agent/studio-393-852.png`, `artifacts/theme-agent/telemetry-2000-1040.png`, `artifacts/theme-agent/studio-2000-1040.png`.


## 2026-10-07 FPS 层级小幅调整

按用户反馈保留原布局，撤回统一卡片方案。再次核对 awesome-design-md 的 NVIDIA 分析，采用既有组件语言、克制强调色、一次只调整一个组件的原则，不照搬品牌配色或卡片几何。九主题时钟字号不变，FPS 从页脚移到时钟附近，76px 数字配27px单位，沿用主题分隔线与正文色。拼贴保持 GPU 大块、两个功耗色块、CPU 横条和不对称内存区；竖屏仅把原页脚96px空间移到标题区，横屏放入标题与时钟之间的空位。硬件主题样式文件保持原样，无新增定时器、动画或远程资源。

参考：https://github.com/VoltAgent/awesome-design-md/blob/main/design-md/nvidia/DESIGN.md （社区设计分析，不是 NVIDIA 官方规范）。

按追加反馈给 FPS 增加各主题协调的静态色块：拼贴为柔和浅绿，纸页为灰绿，暗色主题为低亮度深色；不增加块高度或改动时钟、硬件区域。


### 色块留白复核

真机复核确认第一版80px色块配76px字号、上下内边距为0，虽未溢出但比例过薄，不能把布局自动检查通过等同于视觉质量。现改为竖屏144px高、上下24px内边距、左右28px、时钟下方24px间距；数字保持76px。拼贴复用原VRAM色块的 #d7dfc0 与原卡片32px圆角，其他主题沿用现有表面色和几何。遥测横屏受原标题高度限制，使用64px次级读数与12px上下留白。保留所有主题硬件区域的原有组织方式，竖屏为色块多预留72px。以上尺寸是项目适配选择，不是 Awesome Design 规定的统一数值。
