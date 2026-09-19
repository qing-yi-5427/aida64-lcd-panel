# PC 界面设计依据

本次采用 macOS 风格的信息层级与布局：浅雾灰侧栏、白色圆角卡片、蓝色选中态、克制的分隔线、大号指标数字。保留 Windows 原生标题栏、键盘焦点、窗口缩放与原生设置输入控件；不伪造 macOS 窗口按钮，不增加 WebView 或透明合成依赖。

## 调研来源

- [Apple Human Interface Guidelines · Sidebars](https://developer.apple.com/design/human-interface-guidelines/sidebars)：侧栏作为稳定的导航区域，和内容区形成清晰层级。本项目将概览、设置和面板入口放到侧栏；设置使用同样的分类导航。
- [Apple Human Interface Guidelines · Layout](https://developer.apple.com/design/human-interface-guidelines/layout)：优先考虑内容关系、窗口变化和足够的内容空间。本项目将连接、CPU/GPU、其他指标、屏幕动作按使用频率排列。
- [Apple Human Interface Guidelines · Windows](https://developer.apple.com/design/human-interface-guidelines/windows)：沿用宿主系统可预期的窗口行为。本项目保留 Windows 原生窗口和托盘行为。
- [Bjango iStat Menus · Sensors](https://bjango.com/help/istatmenus4/sensors/)：温度、风扇与功耗作为关联的传感器信息呈现。本项目把 CPU/GPU 使用率作为主数字，温度和功耗作为同卡片内的辅助信息，风扇与内存使用放入紧凑明细。
- [Bjango iStat Menus 7 · Welcome](https://register.bjango.com/help/istatmenus7/welcome/)：监控工具按功能分组，让常用状态能快速浏览，进一步设置集中管理。

## 实现与验证

- 使用现有 WinForms：UI 线程不做硬件读取；没有常驻动画、网页渲染器或背景模糊，减少游戏时的额外负担。
- 自绘用于圆角卡片、按钮与文字，输入框、选择框和数值输入仍为原生控件；支持 Tab 焦点与可见焦点边框。
- 没有数据时显示破折号，不将缺失数据当作零；读数来源可悬停查看。
- “授权完整读取（UAC）”仍由用户主动点击，本次运行授权状态和失败反馈均保留。
- 设置保存、配对时效、撤销配对、端口与网卡、采样间隔、风扇接口、登录启动、托盘启动及旧服务兼容项保留。
- `DesktopPreview.Render(directory)` 只绘制脱离窗口的控件，不显示窗口、不生成托盘图标、不启动控制器；生成示例数据与空状态预览，以及 100%、125%、150% 几何与字体缩放预览；还包括连接页和采集页滚动到底部的预览，共 15 张。
- 离屏缩放预览不是显示器 DPI 切换实测。原生下拉框在 DrawToBitmap 中不显示选中项文字，已单独断言 SelectedIndex/Text 存在。真实跨屏 DPI、原生下拉框弹出和实际点击需用户打开程序审阅；本轮不操作 PC 桌面。

离屏验收已修复主数值裁切、明细标签覆盖、缩放后右侧按钮移出窗口、侧栏底部文字锚点和卡片周围的背景色差。保留清晰的缺失数据状态；未展示任何 PC 窗口或触发授权。

## 2.4.1：真实 DPI 回归修正

上一轮脱离窗体并手动缩放的预览漏掉了初始化顺序问题，现已删除。新预览保留完整隐藏窗体/原生句柄和真实144 DPI，不打开窗口，98项几何检查通过。主窗和设置改用自定义无系统标题栏窗口，文字统一微软雅黑 UI / Segoe UI Semibold。

缩放根因见 [WinForms ContainerControl 源码](https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/ContainerControl.cs)：切换非默认 AutoScaleMode 会清空设计基准，必须随后再设置 AutoScaleDimensions。窗口边界实现参考 [Microsoft 自定义窗口框架](https://learn.microsoft.com/en-us/windows/win32/dwm/customframe)。跨物理显示器和真实拖动仍未操作。