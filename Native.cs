using System;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
namespace FocusShade {
    internal static class Native {
        public const int NOACTIVATE=0x08000000, TOOLWINDOW=0x80, LAYERED=0x80000, TRANSPARENT=0x20;
        public static readonly IntPtr TOPMOST = new IntPtr(-1);
        public static readonly IntPtr NOTOPMOST = new IntPtr(-2);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; public Rectangle Rectangle { get { return Rectangle.FromLTRB(Left,Top,Right,Bottom); } } }
        [StructLayout(LayoutKind.Sequential)] public struct KeyData { public uint Key,Scan,Flags,Time; public IntPtr Extra; }
        public delegate void WinEvent(IntPtr hook,uint ev,IntPtr hwnd,int obj,int child,uint thread,uint time);
        public delegate IntPtr KeyProc(int code,IntPtr wp,IntPtr lp);
        public delegate bool EnumProc(IntPtr hwnd,IntPtr param);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(XY point);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h,uint flags);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc,IntPtr param);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h,EnumProc proc,IntPtr param);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder b,int n);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
        public static string Class(IntPtr h) { var b=new StringBuilder(256); GetClassName(h,b,b.Capacity); return b.ToString(); }
        public static string Title(IntPtr h) { var b=new StringBuilder(256); GetWindowText(h,b,b.Capacity); return b.ToString(); }
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int type,out int value,int size,out int needed);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        static bool Elevated(uint pid) {
            IntPtr process=OpenProcess(0x1000,false,pid),token=IntPtr.Zero;
            try { int value,needed; return process!=IntPtr.Zero && OpenProcessToken(process,8,out token) && GetTokenInformation(token,20,out value,4,out needed) && value!=0; }
            finally { if(token!=IntPtr.Zero) CloseHandle(token); if(process!=IntPtr.Zero) CloseHandle(process); }
        }
        public static bool HigherElevation(IntPtr window) {
            uint pid; GetWindowThreadProcessId(window,out pid);
            return pid!=0 && Elevated(pid) && !Elevated((uint)System.Diagnostics.Process.GetCurrentProcess().Id);
        }
        [StructLayout(LayoutKind.Sequential)] public struct XY { public int X,Y; public XY(int x,int y) { X=x; Y=y; } }
        [StructLayout(LayoutKind.Sequential,Pack=1)] public struct Blend { public byte Op,Flags,Alpha,Format; }
        [DllImport("user32.dll",SetLastError=true)] public static extern bool UpdateLayeredWindow(IntPtr h,IntPtr screen,ref XY position,ref XY size,IntPtr source,ref XY origin,uint key,ref Blend blend,uint flags);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr bitmap);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] public static extern bool ValidateRect(IntPtr h,IntPtr rect);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int cmd);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h,uint color,byte alpha,uint flags);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr h,out uint color,out byte alpha,out uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmGetColorizationColor(out uint color,out bool opaque);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint command);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr FindWindow(string className,string title);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h,IntPtr dc);
        [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr dc,int x,int y);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent proc,uint pid,uint tid,uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int type,KeyProc proc,IntPtr module,uint tid);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attr,out Rect r,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] public static extern int DwmInt(IntPtr h,int attr,out int value,int size);
        [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
        [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr h);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool GetUserObjectInformation(IntPtr h,int index,StringBuilder b,int size,out int needed);
        public static bool Cloaked(IntPtr h) { int c; return DwmInt(h,14,out c,4)==0 && c!=0; }
        public static Rectangle Bounds(IntPtr h) { Rect r; if(DwmGetWindowAttribute(h,9,out r,16)==0 && r.Right>r.Left && r.Bottom>r.Top) return r.Rectangle; return GetWindowRect(h,out r)?r.Rectangle:Rectangle.Empty; }
        public static bool InputDesktopAvailable() { IntPtr h=OpenInputDesktop(0,false,1); if(h==IntPtr.Zero) return false; try { int n; var b=new StringBuilder(256); return GetUserObjectInformation(h,2,b,512,out n)&&b.ToString()=="Default"; } finally { CloseDesktop(h); } }
    }
}
