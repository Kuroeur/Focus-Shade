# FocusShade

Windows 11 桌面专注遮罩工具，WinForms / Win32，.NET Framework 4.x，x64。

## 使用

双击 `FocusShade.exe`，初始遮罩关闭。点击悬浮按钮开启/关闭；按钮不接管前台焦点。图标采用 Windows 当前主题色，开启时中心出现实心圆点。

- 正常按钮为39 DIP双圆图标，圆形和窄条均用逐像素抗锯齿绘制，90% 不透明度；拖到任一显示器工作区边缘，鼠标离开约650ms后收起为6 DIP窄条，两端圆角、60%不透明度。鼠标移入展开。
- 右键点击按钮退出。
- 紧急解除快捷键自动避开冲突。本机当前为 **Ctrl+Alt+F9**；完全退出为 **Ctrl+Alt+Shift+F12**。每次启动以同目录 `HOTKEYS.txt` 和悬停提示为准。紧急解除会展开按钮并移回可见区域。
- Alt+Tab / Ctrl+Alt+Tab：仅系统选择面板位于黑色遮罩上方，按钮隐藏，其他显示器和面板周围保持黑色。根据实际 shell 窗口动态调整层级，不写死主显示器或面板坐标。
- Win+Tab：任务视图位于黑色层上方，按钮隐藏；各显示器任务栏额外保持黑色并阻止点击。等真正关闭后恢复普通遮罩，而非按键松开就恢复。检测不依赖快捷键入口。
- 将应用拖到显示器顶部时，Windows 分屏布局栏可正常显示和操作；拖动结束后保持遮罩。
- 黑色区域会拦截鼠标操作，不会误点下面的窗口或任务栏。使用 Alt+Tab / Win+Tab 切换应用；聚焦窗口仍可操作。键盘输入仍交给当前前台应用。

所有紧急键候选被占用时拒绝启动。候选按 Ctrl+Alt、Ctrl+Shift、Ctrl+Alt+Shift 尝试 F12、F8、F9、F10、F11、Pause；退出优先 Ctrl+Alt+Shift。

## 构建

无需 .NET SDK。在源码目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

使用系统 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，运行30项核心测试并生成 `FocusShade.exe`。也提供 .NET Framework 4.8 的传统 Visual Studio 项目。

原生桌面回归测试会短暂显示测试窗口与黑色遮罩，只操作自身夹具窗口；完成后自动恢复：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -DesktopTests
```

## 命令

```powershell
.\FocusShade.exe --off          # 紧急解除
.\FocusShade.exe --exit         # 退出
.\FocusShade.exe --toggle       # 开关
.\FocusShade.exe --test-windows # 两扇临时人工测试窗口
.\FocusShade.exe --inspect      # 本机窗口/显示器诊断
.\FocusShade.exe --verify       # 实际屏幕像素采样
```

诊断可能包含窗口标题；不会上传。命令仅控制当前 Windows 会话中的工具实例。

## 实现与限制

普通模式采用完整黑色覆盖层，临时让真实前台应用及其可见 owned popup 位于遮罩上方；切换、关闭遮罩和正常退出时请求恢复原有置顶属性。Windows 桌面合成器直接处理窗口移动、缩放和跨屏，不需要工具按显示器刷新率追踪空洞，因此没有持续60/144Hz轮询。外部窗口层级请求采用异步机制，核查应用与按钮确实在黑色层上方；等待时保持完整黑色，不再挖任何空洞。系统拒绝或一秒内未能确认层级时解除遮罩。异常、系统拒绝、程序被强制杀死或应用卡住时，不能保证外部窗口原属性立即恢复。

按钮与遮罩采用 NOACTIVATE / TOOLWINDOW。遮罩覆盖完整虚拟桌面，物理坐标支持负数，DPI 使用 PerMonitorV2。按钮只有状态/尺寸/主题变化时重绘，移除了原来的定时重绘与无条件置顶。主要依靠 WinEvent 事件；普通750ms核查，shell选择界面100ms核查，事件16ms合并；仅按钮拖动期间20ms补充跟踪鼠标。

系统 shell 类名/标题并非稳定公开接口，当前验证为 Windows 11 build26200中文环境；后续版本、第三方 shell 和其他语言可能需适配。安全桌面、独占全屏、提升权限/受保护窗口及反复抢置顶的程序不保证支持。系统选择界面的动画和特殊置顶窗口仍有兼容性限制；工具不是安全防窥边界。混合DPI、负坐标真实布局及每个刷新率下的逐帧性能尚未验证。详细证据见 [TEST-REPORT.md](TEST-REPORT.md)。
