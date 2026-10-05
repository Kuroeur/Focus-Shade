using System;
using System.Drawing;
namespace FocusShade {
    public enum DockEdge { None, Left, Right, Top, Bottom }
    public static class Geometry {
        public static Rectangle Popup(Point anchor,Size size,Rectangle work) {
            int width=Math.Min(size.Width,work.Width),height=Math.Min(size.Height,work.Height);
            int left=anchor.X>=work.Left+work.Width/2?anchor.X-width:anchor.X;
            int top=anchor.Y>=work.Top+work.Height/2?anchor.Y-height:anchor.Y;
            return new Rectangle(Math.Max(work.Left,Math.Min(work.Right-width,left)),Math.Max(work.Top,Math.Min(work.Bottom-height,top)),width,height);
        }
        public static Rectangle Collapsed(Rectangle full, Rectangle area, DockEdge edge, int strip) {
            switch (edge) {
                case DockEdge.Left: return new Rectangle(area.Left, full.Top, strip, full.Height);
                case DockEdge.Right: return new Rectangle(area.Right-strip, full.Top, strip, full.Height);
                case DockEdge.Top: return new Rectangle(full.Left, area.Top, full.Width, strip);
                case DockEdge.Bottom: return new Rectangle(full.Left, area.Bottom-strip, full.Width, strip);
                default: return full;
            }
        }
        public static DockEdge Snap(Rectangle full, Rectangle area, int threshold) {
            int[] d = { Math.Abs(full.Left-area.Left), Math.Abs(full.Right-area.Right), Math.Abs(full.Top-area.Top), Math.Abs(full.Bottom-area.Bottom) };
            int best = 0; for (int i=1;i<4;i++) if(d[i]<d[best]) best=i;
            return d[best]<=threshold ? (DockEdge)(best+1) : DockEdge.None;
        }
    }
    public static class SwitcherPolicy {
        public static bool IsTraySurface(string className,string processName) {
            return processName=="explorer" && (className=="Shell_TrayWnd" || className=="Shell_SecondaryTrayWnd" || className=="TopLevelWindowForOverflowXamlIsland" || className=="NotifyIconOverflowWindow");
        }
        public static bool IsSystemPanel(string className,string processName) {
            bool host=processName=="SearchHost" || processName=="SearchApp" || processName=="SearchUI" || processName=="StartMenuExperienceHost";
            return host && (className=="Windows.UI.Core.CoreWindow" || className=="XamlExplorerHostIslandWindow" || className=="XamlExplorerHostIslandWindow_WASDK" || className=="Windows.UI.Composition.DesktopWindowContentBridge");
        }
        public static bool IsSnapBar(string className,string title,bool shellProcess,bool moving,Rectangle bounds,Rectangle monitor) {
            return moving && shellProcess && className=="XamlExplorerHostIslandWindow" && String.IsNullOrEmpty(title)
                && bounds.Top==monitor.Top && bounds.Height>0 && bounds.Height<monitor.Height
                && bounds.Width>=monitor.Width*3/4 && bounds.Left>=monitor.Left && bounds.Right<=monitor.Right;
        }
        public static bool MaskDuringAltTab(bool enabled) { return enabled; }
        public static bool ForegroundShellView(string className,bool shellProcess) {
            return shellProcess && (className=="XamlExplorerHostIslandWindow" || className=="MultitaskingViewFrame" || className=="TaskSwitcherWnd" || className=="TaskView");
        }
    }
    public static class ButtonVisuals {
        public static Region CreateRegion(Size size) {
            using(var path=new System.Drawing.Drawing2D.GraphicsPath()) {
                float w=Math.Max(1,size.Width),h=Math.Max(1,size.Height),d=Math.Min(w,h);
                if(w==h) path.AddEllipse(0,0,w,h);
                else {
                    path.AddArc(0,0,d,d,180,90); path.AddArc(w-d,0,d,d,270,90);
                    path.AddArc(w-d,h-d,d,d,0,90); path.AddArc(0,h-d,d,d,90,90); path.CloseFigure();
                }
                return new Region(path);
            }
        }
        public static byte Alpha(bool collapsed) { return collapsed?(byte)153:(byte)230; }
        public static Color Accent(uint argb) { return Color.FromArgb(255,(int)(argb>>16)&255,(int)(argb>>8)&255,(int)argb&255); }
        public static Color Ink(Color accent) { return (accent.R*299+accent.G*587+accent.B*114)>160000?Color.FromArgb(24,24,24):Color.White; }
    }
    public sealed class ShadeState {
        public bool Enabled, AltSwitch, ShellView;
        public DateTime PendingUntil;
        public bool Suspended(DateTime now) { return Enabled && (AltSwitch || ShellView || now<PendingUntil); }
        public void EndAltSwitch(DateTime now) { if(!AltSwitch) return; AltSwitch=false; PendingUntil=now.AddMilliseconds(180); }
        public void Emergency() { Enabled=false; AltSwitch=false; ShellView=false; PendingUntil=DateTime.MinValue; }
    }
    public sealed class DragSession {
        public bool Active,Moved;
        public void Start() { Active=true; Moved=false; }
        public void Cancel() { Active=false; Moved=false; }
    }
    public static class WindowEvents {
        public static bool IsWindowObject(int obj,int child) { return obj==0 && child==0; }
    }
    public static class Safety {
        public static void FailOpen(Action work,Action<Exception> recover) { try { work(); } catch(Exception ex) { recover(ex); } }
    }
}
