using System;
using System.Collections.Generic;
namespace FocusShade {
    // Keep visible app windows over an opaque mask. DWM then moves/resizes them
    // in the same composition frame, without chasing their bounds with a timer.
#if DESKTOP_TESTS
    // Native fixture helpers are excluded from the shipping application.
    internal sealed class WindowLayers : IDisposable {
        readonly Dictionary<IntPtr,bool> original=new Dictionary<IntPtr,bool>();
        readonly Dictionary<IntPtr,bool> restoring=new Dictionary<IntPtr,bool>();
        readonly Dictionary<IntPtr,ulong> identities=new Dictionary<IntPtr,ulong>();
        readonly HashSet<IntPtr> raised=new HashSet<IntPtr>();
        readonly HashSet<IntPtr> settled=new HashSet<IntPtr>();
        public void InvalidateStack() { raised.Clear(); settled.Clear(); }
        public void WindowRestored(IntPtr h) { raised.Remove(h); settled.Remove(h); }
        static ulong Identity(IntPtr h) { uint pid; uint thread=Native.GetWindowThreadProcessId(h,out pid); return ((ulong)pid<<32)|thread; }
        public void ForgetDestroyed(IntPtr h) { original.Remove(h); restoring.Remove(h); identities.Remove(h); raised.Remove(h); settled.Remove(h); }
        void RememberOne(IntPtr h) {
            if(original.ContainsKey(h)) return;
            bool baseline;
            if(restoring.TryGetValue(h,out baseline) && identities[h]==Identity(h)) { original.Add(h,baseline); restoring.Remove(h); identities.Remove(h); }
            else { restoring.Remove(h); identities.Remove(h); original.Add(h,(Native.GetWindowLongPtr(h,-20).ToInt64()&8)!=0); }
        }
        public void Remember(HashSet<IntPtr> windows) { foreach(var h in windows) RememberOne(h); }
        public bool KeepAbove(IntPtr window) { return KeepAbove(window,Native.TOPMOST); }
        public bool KeepAbove(IntPtr window,IntPtr anchor) {
            if(window==IntPtr.Zero || !Native.IsWindow(window)) return false;
            RememberOne(window);
            if(raised.Contains(window)) {
                if(!settled.Contains(window) || (Native.GetWindowLongPtr(window,-20).ToInt64()&8)!=0) return true;
                WindowRestored(window);
            }
            bool success=Native.SetWindowPos(window,anchor,0,0,0,0,0x4213); if(success) raised.Add(window); return success;
        }
        public string Problem="";
        public bool Confirmed() { bool success=true; foreach(var h in raised) { if(!Native.IsWindow(h) || (Native.GetWindowLongPtr(h,-20).ToInt64()&8)==0) success=false; else settled.Add(h); } return success; }
        public bool StackCorrect(IntPtr mask,IntPtr button,IntPtr target) { return StackCorrect(mask,button,target,new HashSet<IntPtr>()); }
        public bool StackCorrect(IntPtr mask,IntPtr button,IntPtr target,HashSet<IntPtr> shellSurfaces) {
            return StackCorrect(mask,button,target,shellSurfaces,IntPtr.Zero);
        }
        public bool StackCorrect(IntPtr mask,IntPtr button,IntPtr target,HashSet<IntPtr> shellSurfaces,IntPtr popup) {
            Problem=""; if(!Confirmed()) { Problem="promotion not confirmed"; return false; }
            var above=new HashSet<IntPtr>(); var ranks=new Dictionary<IntPtr,int>(); IntPtr h=Native.GetWindow(mask,3); int limit=512;
            while(h!=IntPtr.Zero && limit-->0) {
                above.Add(h);
                ranks[h]=ranks.Count;
                if(h!=button && !original.ContainsKey(h) && !shellSurfaces.Contains(h) && Native.IsWindowVisible(h) && !Native.Cloaked(h)) { Problem="unexpected-above-mask="+h+" class="+Native.Class(h); return false; }
                h=Native.GetWindow(h,3);
            }
            foreach(var pair in original) if(Native.IsWindowVisible(pair.Key) && !above.Contains(pair.Key)) { Problem="app-below-mask="+pair.Key+" class="+Native.Class(pair.Key); return false; }
            if(!above.Contains(button)) { Problem="button-below-mask"; return false; }
            foreach(var pair in original) if(pair.Key!=popup && Native.IsWindowVisible(pair.Key) && ranks[pair.Key]>ranks[button]) { Problem="app-above-button="+pair.Key; return false; }
            if(popup!=IntPtr.Zero && Native.IsWindowVisible(popup)) {
                if(!ranks.ContainsKey(popup) || ranks[popup]>ranks[button]) { Problem="popup-not-below-button"; return false; }
                foreach(var pair in original) if(pair.Key!=popup && Native.IsWindowVisible(pair.Key) && ranks[pair.Key]>ranks[popup]) { Problem="app-above-popup="+pair.Key; return false; }
            }
            return true;
        }
        public void Retain(HashSet<IntPtr> windows) {
            var remove=new List<IntPtr>(); foreach(var pair in original) if(!windows.Contains(pair.Key)) remove.Add(pair.Key);
            foreach(var h in remove) if(!original[h]) Restore(h);
            foreach(var h in remove) if(original.ContainsKey(h)) Restore(h);
        }
        void Restore(IntPtr h) {
            bool wasTop=original[h]; original.Remove(h); raised.Remove(h); settled.Remove(h);
            // Always queue restoration, including when a preceding promotion is still pending.
            if(Native.IsWindow(h)) { restoring[h]=wasTop; identities[h]=Identity(h); Native.SetWindowPos(h,wasTop?Native.TOPMOST:Native.NOTOPMOST,0,0,0,0,0x4213); }
        }
        public void Dispose() { Retain(new HashSet<IntPtr>()); }
    }
#endif
    internal enum ShellSurfaceKind { None, AltTab, TaskView, SystemPanel }
    internal struct ShellSurface { public IntPtr Window; public ShellSurfaceKind Kind; }
    internal static class ShellWindows {
        public static HashSet<IntPtr> TraySurfaces(System.Drawing.Point point) {
            var result=new HashSet<IntPtr>(); var monitor=System.Windows.Forms.Screen.FromPoint(point);
            Native.EnumWindows(delegate(IntPtr h,IntPtr p) {
                if(Native.IsWindowVisible(h) && !Native.Cloaked(h) && IsTraySurface(h) && System.Windows.Forms.Screen.FromRectangle(Native.Bounds(h)).DeviceName==monitor.DeviceName) result.Add(h);
                return true;
            },IntPtr.Zero);
            return result;
        }
        public static bool IsTraySurface(IntPtr window) {
            string className=Native.Class(window);
            if(!SwitcherPolicy.IsTraySurface(className,"explorer")) return false;
            uint pid; Native.GetWindowThreadProcessId(window,out pid);
            try { using(var process=System.Diagnostics.Process.GetProcessById((int)pid)) return SwitcherPolicy.IsTraySurface(className,process.ProcessName); }
            catch { return false; }
        }
        public static bool IsSnapBar(IntPtr h,bool dragging) {
            if(!dragging || !Native.IsWindowVisible(h) || Native.Cloaked(h) || Native.Class(h)!="XamlExplorerHostIslandWindow") return false;
            uint pid; Native.GetWindowThreadProcessId(h,out pid); bool shell=false;
            try { using(var p=System.Diagnostics.Process.GetProcessById((int)pid)) shell=p.ProcessName=="explorer"; } catch { }
            var bounds=Native.Bounds(h); var monitor=System.Windows.Forms.Screen.FromRectangle(bounds).Bounds;
            return SwitcherPolicy.IsSnapBar(Native.Class(h),Native.Title(h),shell,true,bounds,monitor);
        }
        public static HashSet<IntPtr> SnapBars(bool moving,HashSet<IntPtr> previous) {
            moving=Native.DragActive(Native.GetForegroundWindow(),moving);
            var found=new HashSet<IntPtr>();
            if(!moving && previous.Count==0) return found;
            Native.EnumWindows(delegate(IntPtr h,IntPtr data) { if(IsSnapBar(h,moving || previous.Contains(h))) found.Add(h); return true; },IntPtr.Zero);
            return found;
        }
        public static ShellSurface Inspect(IntPtr h,ShellSurfaceKind intent) {
            if(h==IntPtr.Zero || !Native.IsWindowVisible(h) || Native.Cloaked(h)) return new ShellSurface();
            string c=Native.Class(h);
            uint pid; Native.GetWindowThreadProcessId(h,out pid);
            try { using(var p=System.Diagnostics.Process.GetProcessById((int)pid)) {
                if(SwitcherPolicy.IsSystemPanel(c,p.ProcessName)) return new ShellSurface { Window=h,Kind=ShellSurfaceKind.SystemPanel };
                if(p.ProcessName!="explorer"&&p.ProcessName!="ShellExperienceHost") return new ShellSurface();
            } } catch { return new ShellSurface(); }
            if(!SwitcherPolicy.ForegroundShellView(c,true)) return new ShellSurface();
            string title=Native.Title(h);
            ShellSurfaceKind kind=ShellSurfaceKind.None;
            if(c=="TaskSwitcherWnd" || title=="任务切换" || title=="Task Switching" || title=="Task Switcher" || title=="タスクの切り替え") kind=ShellSurfaceKind.AltTab;
            else if(c=="MultitaskingViewFrame" || c=="TaskView" || title=="任务视图" || title=="Task View" || title=="タスク ビュー") kind=ShellSurfaceKind.TaskView;
            else kind=intent;
            return new ShellSurface { Window=h,Kind=kind };
        }
        public static ShellSurface Find(ShellSurfaceKind intent,IntPtr previous,ShellSurfaceKind previousKind) {
            var found=Inspect(Native.GetForegroundWindow(),intent); if(found.Kind!=ShellSurfaceKind.None) return found;
            // EnumWindows may omit immersive shell surfaces. Keep the actual known HWND through its close animation.
            found=Inspect(previous,previousKind); if(found.Kind!=ShellSurfaceKind.None) return found;
            Native.EnumWindows(delegate(IntPtr h,IntPtr p) { var candidate=Inspect(h,ShellSurfaceKind.None); if(candidate.Kind!=ShellSurfaceKind.None) { found=candidate; return false; } return true; },IntPtr.Zero);
            return found;
        }
    }
}
