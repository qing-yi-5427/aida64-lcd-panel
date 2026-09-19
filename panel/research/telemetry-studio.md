# Telemetry / Studio design notes

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
