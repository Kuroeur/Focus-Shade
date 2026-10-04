# 设计

普通模式使用单个覆盖虚拟桌面的不透明黑色分层窗口。前台应用及同一 owner 链的可见窗口临时位于黑色层之上，DWM直接合成应用窗口，移动/缩放无需重建遮罩。保存原有TOPMOST属性，切换及退出提交恢复；跨进程SetWindowPos使用ASYNCWINDOWPOS避免目标应用阻塞工具线程。异步请求与完成状态分开，未确认时仅等待实际层级，一秒超时则解除，不使用任何镂空region。每次确认需应用及按钮真实位于黑色层上方，不只检查TOPMOST属性。

Alt+Tab使用实际shell HWND作为遮罩插入位置。Windows 11的宿主边界可能覆盖整块显示器，但其绘制只包含中央选择面板；让黑色层直接位于宿主之下，可保留面板且背景保持黑色。不依赖主屏索引、分辨率或UIAutomation返回的全屏矩形。Win+Tab实际shell位于全黑层之上，额外黑色窗口覆盖各任务栏，持续跟踪shell可见性，关闭后恢复。

39 DIP双圆/胶囊按钮使用4倍超采样、PArgb位图和UpdateLayeredWindow逐像素透明实现抗锯齿外形；DwmGetColorizationColor读取主题色，WM_DWMCOLORIZATIONCOLORCHANGED/WM_SETTINGCHANGE更新；分层窗口alpha分别230和153。空闲不周期Invalidate或置顶；检测实际Z顺序异常时才修复。

事件负责前台与显示/隐藏，普通750ms核查用来补足外部程序自行改变层级，系统选择界面100ms核查。按钮的20ms鼠标跟踪只在拖动中运行。屏幕区域与DPI均基于真实系统API，不更改主题、壁纸、任务栏、缩放或显示器布局。

限制：强制终止和权限/受保护窗口可能妨碍置顶恢复；多个进程持续抢层级无完整保证。安全桌面、独占全屏和shell更新不承诺支持。性能架构消除了位置追踪轮询，但尚无60/144/240Hz逐帧测量。

遮罩不使用WS_EX_TRANSPARENT，黑区拦截鼠标；保留NOACTIVATE与原生置顶样式，避免WinForms托管TopMost触发隐式焦点。
