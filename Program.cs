using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FocusShade {
    internal static class Program {
        [STAThread] static void Main(string[] args) {
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length>0 && args[0]=="--test-windows") { Application.Run(new TestWindows()); return; }
            if (args.Length>0 && args[0]=="--inspect") { Inspect(); return; }
            if (args.Length>0 && args[0]=="--verify") { Verify(); return; }
            if(args.Length>0 && (args[0]=="--toggle"||args[0]=="--off"||args[0]=="--exit")) {
                try { using(var signal=EventWaitHandle.OpenExisting("Local\\FocusShade."+args[0].Substring(2))) signal.Set(); } catch(WaitHandleCannotBeOpenedException) { }
                return;
            }
            bool created; using(var mutex=new Mutex(true,"Local\\FocusShade.Desktop",out created)) {
                if(!created) return;
                try { using(var controller=new Controller()) Application.Run(controller); }
                catch(Exception ex) { try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.txt"),ex.ToString()); } catch { } MessageBox.Show("FocusShade 已停止并解除遮罩。\n"+ex.Message,"FocusShade"); }
            }
        }
        static void Inspect() {
            var b=new StringBuilder(); b.AppendLine("OS="+Environment.OSVersion+" foreground="+Native.GetForegroundWindow());
            IntPtr fg=Native.GetForegroundWindow(); b.AppendLine("FG "+fg+" "+Native.Class(fg)+" ["+Native.Title(fg)+"] visible="+Native.IsWindowVisible(fg)+" cloaked="+Native.Cloaked(fg)+" root="+Native.GetAncestor(fg,2)+" owner="+Native.GetAncestor(fg,3)+" "+Native.Bounds(fg));
            foreach(Screen s in Screen.AllScreens) b.AppendLine("MONITOR "+s.DeviceName+" "+s.Bounds+" WORK "+s.WorkingArea);
            Native.EnumWindows(delegate(IntPtr h,IntPtr p) { if(Native.IsWindowVisible(h)) b.AppendLine(h+" "+Native.Class(h)+" ["+Native.Title(h)+"] cloak="+Native.Cloaked(h)+" "+Native.Bounds(h)); return true; },IntPtr.Zero);
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"windows.txt"),b.ToString());
        }
        static void Verify() {
            var report=new StringBuilder(); IntPtr mask=Native.FindWindow(null,"FocusShade Black Mask"),foreground=Native.GetForegroundWindow();
            report.AppendLine("mask="+mask+" visible="+Native.IsWindowVisible(mask)+" foreground="+foreground+" class="+Native.Class(foreground));
            report.AppendLine("fullMaskBounds="+Native.Bounds(mask)+" inputBlocked="+((Native.GetWindowLongPtr(mask,-20).ToInt64()&Native.TRANSPARENT)==0));
            IntPtr dc=Native.GetDC(IntPtr.Zero);
            try {
                foreach(Screen screen in Screen.AllScreens) {
                    Point[] points={new Point(screen.Bounds.Left+20,screen.Bounds.Top+20),new Point(screen.Bounds.Left+screen.Bounds.Width/2,screen.Bounds.Bottom-10)};
                    foreach(Point point in points) report.AppendLine(screen.DeviceName+" "+point+" RGB=0x"+Native.GetPixel(dc,point.X,point.Y).ToString("X6")+" hit="+Native.WindowFromPoint(new Native.XY(point.X,point.Y)));
                }
            } finally { Native.ReleaseDC(IntPtr.Zero,dc); }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification.txt"),report.ToString());
        }

    }
    internal class PassiveForm : Form {
        public PassiveForm() { SetStyle(ControlStyles.Selectable,false); FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.Manual; }
        protected override void Select(bool directed,bool forward) { }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=Native.NOACTIVATE|Native.TOOLWINDOW|8; return p; } }
        protected override void WndProc(ref Message m) { if(m.Msg==0x21) { m.Result=new IntPtr(3); return; } base.WndProc(ref m); }
        public void Raise() { if(Native.IsIconic(Handle)) Native.ShowWindow(Handle,4); Native.SetWindowPos(Handle,Native.TOPMOST,0,0,0,0,0x13); }
    }
    internal sealed class MaskForm : PassiveForm {
        public MaskForm() { Text="FocusShade Black Mask"; BackColor=Color.Black; }
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=Native.LAYERED; return p; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.SetLayeredWindowAttributes(Handle,0,255,2); }
        public void Apply(Rectangle desktop,bool raise) {
            if(Native.IsIconic(Handle)) Native.ShowWindow(Handle,4);
            if(Bounds!=desktop) Bounds=desktop;
            if(!Visible) Show(); if(raise) Raise();
        }
        public void CoverDesktop(Rectangle desktop,FloatingButton button) { button.Raise(); Apply(desktop,false); if(!PlaceBelow(button.Handle)) throw new InvalidOperationException("无法恢复桌面遮罩层级"); }
        public bool PlaceBelow(IntPtr window) { return Native.SetWindowPos(Handle,window,0,0,0,0,0x213); }
    }
    internal sealed class FloatingButton : PassiveForm {
        public Action Toggle,Exit,Changed; bool active;
        public bool Active { get { return active; } set { if(active==value) return; active=value; UpdateShape(); } }
        public int PaintCount { get; private set; }
        Point downCursor,downLocation; bool collapsed;
        readonly DragSession drag=new DragSession();
        readonly System.Windows.Forms.Timer dragTimer=new System.Windows.Forms.Timer { Interval=20 };
        DockEdge edge; Rectangle expanded,area;
        readonly System.Windows.Forms.Timer collapseTimer=new System.Windows.Forms.Timer { Interval=650 };
        public FloatingButton() {
            Text="FocusShade Floating Button"; Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); DoubleBuffered=true;
            ApplyAccent();
            Size=new Size(39,39); var s=Screen.PrimaryScreen; Location=new Point(s.WorkingArea.Right-100,s.WorkingArea.Top+120);
            collapseTimer.Tick+=delegate { collapseTimer.Stop(); if(!drag.Active && edge!=DockEdge.None && !Bounds.Contains(Cursor.Position)) Collapse(); };
            dragTimer.Tick+=delegate { if(!drag.Active) { dragTimer.Stop(); return; } TrackDrag(); if(Native.GetAsyncKeyState(1)>=0) FinishDrag(); };
        }
        protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=Native.LAYERED; return p; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); }
        protected override void OnShown(EventArgs e) { base.OnShown(e); UpdateShape(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if(IsHandleCreated) UpdateShape(); }
        void ApplyAccent() { uint color; bool opaque; Color accent=Native.DwmGetColorizationColor(out color,out opaque)==0?ButtonVisuals.Accent(color):SystemColors.Highlight; if(BackColor==accent) return; BackColor=accent; ForeColor=ButtonVisuals.Ink(accent); UpdateShape(); }
        void UpdateShape() { if(!IsHandleCreated || !Visible || Width<1 || Height<1) return; using(var image=ButtonRenderer.Draw(ClientSize,BackColor,ForeColor,collapsed,Active)) ButtonRenderer.Present(Handle,Location,image,ButtonVisuals.Alpha(collapsed)); PaintCount++; }
        int Dips(int n) { return Math.Max(1,(int)Math.Round(n*Native.GetDpiForWindow(Handle)/96.0)); }
        public void FitDpi() { if(!collapsed) { Size=new Size(Dips(39),Dips(39)); expanded=Bounds; } }
        public void RecoverPosition() { CancelDrag(); edge=DockEdge.None; collapsed=false; var s=Screen.FromPoint(Cursor.Position); Size=new Size(Dips(39),Dips(39)); Location=new Point(s.WorkingArea.Right-Width-Dips(20),s.WorkingArea.Top+Dips(100)); UpdateShape(); }
        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e); if(e.Button==MouseButtons.Right) { if(Exit!=null) Exit(); return; }
            if(e.Button!=MouseButtons.Left) return;
            Expand(); downCursor=Cursor.Position; downLocation=Location; drag.Start(); Capture=true; collapseTimer.Stop(); dragTimer.Start();
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e); if(drag.Active) TrackDrag();
        }
        void TrackDrag() {
            Point p=Cursor.Position;
            if(Math.Abs(p.X-downCursor.X)+Math.Abs(p.Y-downCursor.Y)>Dips(5)) drag.Moved=true;
            if(drag.Moved) { edge=DockEdge.None; Location=new Point(downLocation.X+p.X-downCursor.X,downLocation.Y+p.Y-downCursor.Y); }
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); if(e.Button==MouseButtons.Left) FinishDrag();
        }
        void FinishDrag() {
            if(!drag.Active) return; bool moved=drag.Moved; CancelDrag();
            if(moved) Snap(); else if(Toggle!=null) Toggle();
            if(Changed!=null) Changed();
        }
        public void CancelDrag() { drag.Cancel(); dragTimer.Stop(); collapseTimer.Stop(); if(Capture) Capture=false; }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture) CancelDrag(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if(!Visible) CancelDrag(); }
        void Snap() {
            FitDpi(); area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,Left)),Math.Max(area.Top,Math.Min(area.Bottom-Height,Top)));
            edge=Geometry.Snap(Bounds,area,Dips(24));
            switch(edge) { case DockEdge.Left: Left=area.Left; break; case DockEdge.Right: Left=area.Right-Width; break; case DockEdge.Top: Top=area.Top; break; case DockEdge.Bottom: Top=area.Bottom-Height; break; }
            expanded=Bounds; if(edge!=DockEdge.None) collapseTimer.Start();
        }
        void Collapse() { if(collapsed) return; expanded=Bounds; collapsed=true; Bounds=Geometry.Collapsed(expanded,area,edge,Dips(6)); UpdateShape(); if(Changed!=null) Changed(); }
        void Expand() { collapseTimer.Stop(); if(!collapsed) return; collapsed=false; Bounds=expanded; UpdateShape(); if(Changed!=null) Changed(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Expand(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if(edge!=DockEdge.None) collapseTimer.Start(); }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x14) { m.Result=new IntPtr(1); return; }
            if(m.Msg==0xF) { Native.ValidateRect(Handle,IntPtr.Zero); m.Result=IntPtr.Zero; return; }
            base.WndProc(ref m);
            if(m.Msg==0x2E0) { if(collapsed) Expand(); FitDpi(); if(Changed!=null) Changed(); }
            if(m.Msg==0x320 || m.Msg==0x1A) ApplyAccent();
            if(m.Msg==0x7E || m.Msg==0x1A && m.WParam.ToInt32()==0x2F) { RecoverPosition(); if(Changed!=null) Changed(); }
        }
        protected override void Dispose(bool disposing) { if(disposing) { CancelDrag(); collapseTimer.Dispose(); dragTimer.Dispose(); } base.Dispose(disposing); }
        public string DockStatus { get { return edge+" collapsed="+collapsed; } }
    }
    internal sealed class Controller : ApplicationContext {
        readonly MaskForm mask=new MaskForm(); readonly FloatingButton button=new FloatingButton();
        readonly ShadeState state=new ShadeState(); readonly List<IntPtr> hooks=new List<IntPtr>();
        readonly WindowLayers appLayers=new WindowLayers();
        readonly List<MaskForm> taskbarMasks=new List<MaskForm>(); DateTime promotionStarted;
        DateTime desktopTransitionUntil; bool moving; HashSet<IntPtr> snapBars=new HashSet<IntPtr>();
        Native.WinEvent eventProc; Native.KeyProc keyProc; IntPtr keyboard;
        readonly System.Windows.Forms.Timer checkTimer=new System.Windows.Forms.Timer { Interval=750 };
        readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer { Interval=16 };
        IntPtr target; bool queued,disposed; string lastStatus="",lastRegion="";
        ShellSurfaceKind switchIntent; IntPtr currentSelector,layerTarget; bool compositionPath,stackDirty=true; string maskMode="off";
        string emergencyKey,exitKey; readonly ToolTip tip=new ToolTip();
        readonly List<EventWaitHandle> commandSignals=new List<EventWaitHandle>();
        readonly List<RegisteredWaitHandle> commandWaits=new List<RegisteredWaitHandle>();
        readonly string directory=AppDomain.CurrentDomain.BaseDirectory;
        public Controller() {
            target=Native.GetForegroundWindow();
            button.Toggle=delegate { state.Enabled=!state.Enabled; SafeUpdate(); };
            button.Exit=delegate { ExitThread(); }; button.Changed=Queue;
            button.Show(); button.FitDpi(); mask.CreateControl();
            emergencyKey=RegisterRecoveryKey(1,new uint[] { 3,6,7 });
            exitKey=RegisterRecoveryKey(2,new uint[] { 7,6,3 });
            if(emergencyKey==null || exitKey==null) { Native.UnregisterHotKey(button.Handle,1); Native.UnregisterHotKey(button.Handle,2); button.Dispose(); mask.Dispose(); throw new InvalidOperationException("所有候选紧急快捷键均被占用，拒绝启用遮罩"); }
            tip.SetToolTip(button,"点击：遮罩开关；拖动：移动；右键：退出\n紧急解除："+emergencyKey+"\n完全退出："+exitKey);
            File.WriteAllText(Path.Combine(directory,"HOTKEYS.txt"),"紧急解除遮罩："+emergencyKey+Environment.NewLine+"完全退出："+exitKey);
            HotkeyFilter filter=new HotkeyFilter(this); Application.AddMessageFilter(filter); hotkeyFilter=filter;
            eventProc=OnEvent; keyProc=OnKey;
            Hook(3,0x17); Hook(0x8000,0x8004); Hook(0x800B,0x800B); Hook(0x8017,0x8018);
            keyboard=Native.SetWindowsHookEx(13,keyProc,Native.GetModuleHandle(null),0);
            if(keyboard==IntPtr.Zero) throw new InvalidOperationException("无法安装切换键监听");
            updateTimer.Tick+=delegate { updateTimer.Stop(); queued=false; SafeUpdate(); };
            checkTimer.Tick+=delegate { SafeUpdate(); }; checkTimer.Start(); Update();
            Command("toggle",delegate { state.Enabled=!state.Enabled; SafeUpdate(); });
            Command("off",Emergency); Command("exit",ExitThread);
        }
        readonly HotkeyFilter hotkeyFilter;
        void Command(string name,Action action) {
            var signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\FocusShade."+name); commandSignals.Add(signal);
            commandWaits.Add(ThreadPool.RegisterWaitForSingleObject(signal,delegate(object data,bool timeout) { if(!disposed) { try { button.BeginInvoke(action); } catch(InvalidOperationException) { } } },null,Timeout.Infinite,false));
        }
        string RegisterRecoveryKey(int id,uint[] modifiers) {
            foreach(uint mod in modifiers) foreach(uint key in new uint[] { 0x7B,0x77,0x78,0x79,0x7A,0x13 }) {
                if(Native.RegisterHotKey(button.Handle,id,0x4000|mod,key)) return ((mod&2)!=0?"Ctrl+":"")+((mod&1)!=0?"Alt+":"")+((mod&4)!=0?"Shift+":"")+(key==0x13?"Pause":"F"+(key-0x6F));
            } return null;
        }
        void Hook(uint min,uint max) { IntPtr h=Native.SetWinEventHook(min,max,IntPtr.Zero,eventProc,0,0,2); if(h==IntPtr.Zero) throw new InvalidOperationException("无法监听窗口事件"); hooks.Add(h); }
        void Queue() { if(disposed || queued) return; queued=true; updateTimer.Start(); }
        void OnEvent(IntPtr hook,uint ev,IntPtr hwnd,int obj,int child,uint thread,uint time) {
            if(disposed) return;
            if(ev==0x8001 && WindowEvents.IsWindowObject(obj,child)) { appLayers.ForgetDestroyed(hwnd); snapBars.Remove(hwnd); }
            if(ev==0xA) moving=true;
            if(ev==0xB) moving=false;
            if(ev==0x8002 && Native.Class(hwnd)=="XamlExplorerHostIslandWindow") snapBars=ShellWindows.SnapBars(moving,snapBars);
            if(ev==0x8003 && WindowEvents.IsWindowObject(obj,child)) snapBars.Remove(hwnd);
            if(ev==0x16 || ev==0x17) { desktopTransitionUntil=DateTime.UtcNow.AddMilliseconds(1500); if(ev==0x17) appLayers.WindowRestored(hwnd); }
            bool desktopReorder=ev==0x8004 && child==0 && Native.Class(hwnd)=="#32769";
            if(ev==0x8004 && !desktopReorder && !WindowEvents.IsWindowObject(obj,child)) return;
            if(ev==0x8004) appLayers.InvalidateStack();
            if(ev==0x8018 && WindowEvents.IsWindowObject(obj,child)) appLayers.WindowRestored(hwnd);
            if(ev==0x14 && switchIntent!=ShellSurfaceKind.TaskView) { switchIntent=ShellSurfaceKind.AltTab; state.AltSwitch=true; BeginAltTab(); }
            if(ev==0x15) { state.AltSwitch=false; state.PendingUntil=DateTime.UtcNow.AddMilliseconds(180); }
            if(ev==0x800B && hwnd!=target && Native.GetAncestor(hwnd,3)!=Native.GetAncestor(target,3)) return;
            if(ev==0x800B && compositionPath && hwnd==target) return; // DWM owns live movement/resize.
            if(obj!=0 && ev>=0x8000 && !desktopReorder) return; Queue();
            if(ev!=0x800B) stackDirty=true;
        }
        IntPtr OnKey(int code,IntPtr wp,IntPtr lp) {
            try {
                if(code>=0) {
                    var k=(Native.KeyData)Marshal.PtrToStructure(lp,typeof(Native.KeyData));
                    bool down=wp.ToInt32()==0x100 || wp.ToInt32()==0x104;
                    if(down && k.Key==0x44 && (Native.GetAsyncKeyState(0x5B)<0 || Native.GetAsyncKeyState(0x5C)<0)) { desktopTransitionUntil=DateTime.UtcNow.AddMilliseconds(1500); checkTimer.Interval=16; Queue(); }
                    if(down && k.Key==9) {
                        bool alt=(k.Flags&0x20)!=0 || Native.GetAsyncKeyState(0x12)<0;
                        bool win=Native.GetAsyncKeyState(0x5B)<0 || Native.GetAsyncKeyState(0x5C)<0;
                        if(alt || win) { switchIntent=win?ShellSurfaceKind.TaskView:ShellSurfaceKind.AltTab; state.AltSwitch=alt; state.PendingUntil=DateTime.UtcNow.AddSeconds(win?2:0.3); if(win) Suspend(); else BeginAltTab(); Queue(); }
                    }
                    if(!down && (k.Key==0xA4 || k.Key==0xA5 || k.Key==0x12)) { state.EndAltSwitch(DateTime.UtcNow); Queue(); }
                }
            } catch { state.Emergency(); appLayers.Dispose(); HideTaskbarMasks(); mask.Hide(); button.Show(); }
            return Native.CallNextHookEx(keyboard,code,wp,lp);
        }
        void Suspend() { HideTaskbarMasks(); appLayers.Dispose(); compositionPath=false; layerTarget=IntPtr.Zero; mask.Hide(); button.Hide(); checkTimer.Interval=100; lastRegion=""; currentSelector=IntPtr.Zero; }
        void BeginAltTab() {
            appLayers.Dispose(); compositionPath=false; layerTarget=IntPtr.Zero; button.Hide(); checkTimer.Interval=100;
            // Before Windows constructs the chooser, make its backdrop black too.
            if(SwitcherPolicy.MaskDuringAltTab(state.Enabled)) {
                Rectangle desktop=SystemInformation.VirtualScreen; string signature="alt|"+desktop;
                if(signature!=lastRegion || !mask.Visible) { mask.Apply(desktop,false); lastRegion=signature; }
            } else mask.Hide();
        }
        bool AppWindow(IntPtr h) {
            if(h==IntPtr.Zero || !Native.IsWindow(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h) || Native.Cloaked(h)) return false;
            uint pid; Native.GetWindowThreadProcessId(h,out pid); if(pid==(uint)Process.GetCurrentProcess().Id) return false;
            string c=Native.Class(h);
            return c!="Progman" && c!="WorkerW" && c!="Shell_TrayWnd" && c!="Shell_SecondaryTrayWnd" && c!="MultitaskingViewFrame" && c!="TaskSwitcherWnd";
        }
        void SafeUpdate() { Safety.FailOpen(Update,delegate(Exception ex) { state.Emergency(); appLayers.Dispose(); compositionPath=false; HideTaskbarMasks(); mask.Hide(); button.RecoverPosition(); button.Show(); try { File.AppendAllText(Path.Combine(directory,"error.txt"),DateTime.Now+" "+ex+Environment.NewLine); } catch { } }); }
        void Update() {
            if(disposed) return;
            snapBars=ShellWindows.SnapBars(moving,snapBars);
            ShellSurface surface=ShellWindows.Find(switchIntent);
            state.ShellView=surface.Kind==ShellSurfaceKind.TaskView;
            if(surface.Kind!=ShellSurfaceKind.None) state.PendingUntil=DateTime.MinValue;
            bool pending=DateTime.UtcNow<state.PendingUntil;
            bool suspended=pending && switchIntent==ShellSurfaceKind.TaskView && surface.Kind==ShellSurfaceKind.None || !Native.InputDesktopAvailable();
            bool altMode=surface.Kind!=ShellSurfaceKind.None || state.AltSwitch || pending && switchIntent==ShellSurfaceKind.AltTab;
            IntPtr foreground=Native.GetForegroundWindow();
            if(suspended) { maskMode="task-view"; Suspend(); }
            else if(altMode) {
                bool entering=currentSelector!=surface.Window; maskMode=state.ShellView?"task-view":"alt-tab";
                BeginAltTab();
                if(state.Enabled && surface.Kind!=ShellSurfaceKind.None && (entering || currentSelector!=surface.Window || stackDirty)) {
                    if(!mask.PlaceBelow(surface.Window)) throw new InvalidOperationException("无法把遮罩放在任务切换面板下方");
                    currentSelector=surface.Window;
                }
                if(state.Enabled && state.ShellView) ShowTaskbarMasks(); else HideTaskbarMasks();
            } else {
                HideTaskbarMasks(); switchIntent=ShellSurfaceKind.None; currentSelector=IntPtr.Zero; checkTimer.Interval=WindowEvents.RecheckInterval(DateTime.UtcNow,desktopTransitionUntil);
                uint pid=0; if(foreground!=IntPtr.Zero) Native.GetWindowThreadProcessId(foreground,out pid);
                if(foreground!=IntPtr.Zero && !snapBars.Contains(foreground) && pid!=(uint)Process.GetCurrentProcess().Id) target=AppWindow(foreground)?foreground:IntPtr.Zero;
                bool buttonHidden=!button.Visible; if(buttonHidden) button.Show(); button.Active=state.Enabled;
                if(!state.Enabled) {
                    maskMode="off"; appLayers.Dispose(); compositionPath=false; layerTarget=IntPtr.Zero; mask.Hide(); lastRegion="";
                    if(buttonHidden || stackDirty) button.Raise();
                } else {
                    Rectangle desktop=SystemInformation.VirtualScreen;
                    if(compositionPath && !appLayers.StackCorrect(mask.Handle,button.Handle,target,snapBars)) stackDirty=true;
                    bool changed=layerTarget!=target || !mask.Visible || lastRegion!="composition|"+desktop;
                    bool reorder=changed || stackDirty && !appLayers.StackCorrect(mask.Handle,button.Handle,target,snapBars);
                    if(reorder || !compositionPath) {
                        var visible=new HashSet<IntPtr>();
                        if(AppWindow(target)) {
                            visible.Add(target); IntPtr owner=Native.GetAncestor(target,3);
                            Native.EnumWindows(delegate(IntPtr h,IntPtr p) { if(AppWindow(h) && Native.GetAncestor(h,3)==owner) visible.Add(h); return true; },IntPtr.Zero);
                        }
                        appLayers.Retain(visible); appLayers.Remember(visible);
                        if(changed || reorder) {
                            mask.CoverDesktop(desktop,button); lastRegion="composition|"+desktop;
                            appLayers.InvalidateStack(); if(changed || compositionPath) promotionStarted=DateTime.UtcNow;
                        }
                        bool success=true;
                        foreach(var h in visible) if(!appLayers.KeepAbove(h,button.Handle)) success=false;
                        layerTarget=target; compositionPath=success && appLayers.StackCorrect(mask.Handle,button.Handle,target,snapBars);
                        maskMode=compositionPath?"composition":"waiting-for-layer";
                        if(!success || !compositionPath && DateTime.UtcNow>promotionStarted.AddSeconds(1)) throw new InvalidOperationException("应用窗口未能进入遮罩上方，已解除遮罩："+appLayers.Problem);
                        if(!compositionPath) Queue();
                    }
                }
            }
            stackDirty=false; WriteStatus(suspended,foreground);
        }
        void HideTaskbarMasks() { foreach(var form in taskbarMasks) form.Hide(); }
        void ShowTaskbarMasks() {
            var rectangles=new List<Rectangle>();
            Native.EnumWindows(delegate(IntPtr h,IntPtr p) { string c=Native.Class(h); if((c=="Shell_TrayWnd" || c=="Shell_SecondaryTrayWnd") && Native.IsWindowVisible(h)) rectangles.Add(Native.Bounds(h)); return true; },IntPtr.Zero);
            while(taskbarMasks.Count<rectangles.Count) taskbarMasks.Add(new MaskForm());
            for(int i=0;i<taskbarMasks.Count;i++) { if(i<rectangles.Count) { if(!taskbarMasks[i].Visible || taskbarMasks[i].Bounds!=rectangles[i]) taskbarMasks[i].Apply(rectangles[i],true); } else taskbarMasks[i].Hide(); }
        }
        void WriteStatus(bool suspended,IntPtr foreground) {
            string text="mode="+maskMode+" snapBars="+snapBars.Count+" moving="+moving+" composition="+compositionPath+" selector="+currentSelector+" enabled="+state.Enabled+" suspended="+suspended+" shellView="+state.ShellView+" altSwitch="+state.AltSwitch+" mask="+mask.Visible+" button="+button.Visible+" target="+target+" foreground="+foreground+" targetClass="+Native.Class(target)+" targetBounds="+Native.Bounds(target)+" buttonBounds="+button.Bounds+" paints="+button.PaintCount+" dock="+button.DockStatus+" monitors="+Screen.AllScreens.Length+" emergency="+emergencyKey+" exit="+exitKey;
            if(text==lastStatus) return; lastStatus=text;
            try { File.WriteAllText(Path.Combine(directory,"status.txt"),text); File.AppendAllText(Path.Combine(directory,"events.log"),DateTime.Now.ToString("O")+" "+text+Environment.NewLine); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        public void Emergency() { state.Emergency(); switchIntent=ShellSurfaceKind.None; appLayers.Dispose(); compositionPath=false; stackDirty=true; HideTaskbarMasks(); mask.Hide(); button.RecoverPosition(); SafeUpdate(); }
        protected override void ExitThreadCore() { Dispose(); base.ExitThreadCore(); }
        protected override void Dispose(bool disposing) {
            if(disposed) return; disposed=true;
            if(disposing) {
                appLayers.Dispose(); foreach(var form in taskbarMasks) form.Dispose(); mask.Hide(); foreach(IntPtr h in hooks) Native.UnhookWinEvent(h); if(keyboard!=IntPtr.Zero) Native.UnhookWindowsHookEx(keyboard);
                if(hotkeyFilter!=null) Application.RemoveMessageFilter(hotkeyFilter);
                Native.UnregisterHotKey(button.Handle,1); Native.UnregisterHotKey(button.Handle,2);
                foreach(var wait in commandWaits) wait.Unregister(null); foreach(var signal in commandSignals) signal.Dispose();
                checkTimer.Dispose(); updateTimer.Dispose(); tip.Dispose(); mask.Dispose(); button.Dispose();
            } base.Dispose(disposing);
        }
        class HotkeyFilter : IMessageFilter {
            readonly Controller controller; public HotkeyFilter(Controller c) { controller=c; }
            public bool PreFilterMessage(ref Message m) { if(m.Msg!=0x312) return false; if(m.WParam.ToInt32()==1) controller.Emergency(); else if(m.WParam.ToInt32()==2) controller.ExitThread(); return true; }
        }
    }
    internal sealed class TestWindows : ApplicationContext {
        int remaining=2;
        public TestWindows() { Create("FocusShade Test A",Color.LightSkyBlue,new Rectangle(140,160,630,460)); Create("FocusShade Test B",Color.PeachPuff,new Rectangle(820,290,580,400)); }
        void Create(string title,Color color,Rectangle bounds) {
            var f=new Form { Text=title,BackColor=color,StartPosition=FormStartPosition.Manual,Bounds=bounds };
            f.Controls.Add(new Label { Text="FocusShade 桌面测试窗口\n可拖动、缩放、最大化，并在下方输入文字。",AutoSize=true,Location=new Point(30,35),Font=new Font("Microsoft YaHei",14) });
            f.Controls.Add(new TextBox { Location=new Point(30,125),Width=450,AccessibleName="Test input" });
            f.FormClosed+=delegate { if(--remaining==0) ExitThread(); }; f.Show();
        }
    }
}
