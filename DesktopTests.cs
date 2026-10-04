using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FocusShade;
internal static class DesktopTests {
 static int count; static List<string> lines=new List<string>();
 static void Check(bool ok,string name) { lines.Add((ok?"PASS ":"FAIL ")+name); if(!ok) throw new Exception(name); count++; }
 static void Pump(int ms) { DateTime until=DateTime.UtcNow.AddMilliseconds(ms); while(DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(10); } }
 static bool Top(Form f) { return (Native.GetWindowLongPtr(f.Handle,-20).ToInt64()&8)!=0; }
 static bool OpacityMatches(FloatingButton button,double alpha) {
  uint pixel=Pixel(new Point(button.Left+button.Width/2,button.Top+button.Height/2)); Color c=button.BackColor;
  return Math.Abs((pixel&255)-c.R*alpha)<4 && Math.Abs(((pixel>>8)&255)-c.G*alpha)<4 && Math.Abs(((pixel>>16)&255)-c.B*alpha)<4;
 }
 static uint Pixel(Point p) { IntPtr dc=Native.GetDC(IntPtr.Zero); try { return Native.GetPixel(dc,p.X,p.Y); } finally { Native.ReleaseDC(IntPtr.Zero,dc); } }
 [STAThread] static void Main(string[] args) {
  if(args.Length>0) { Application.Run(new BlockingWindow()); return; }
  try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); Application.EnableVisualStyles();
   using(var app=new Form { Text="FocusShade layer test",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(300,300,500,350),BackColor=Color.Lime })
   using(var existing=new Form { Text="FocusShade original topmost test",TopMost=true,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(850,300,300,250) })
   using(var mask=new MaskForm()) using(var button=new FloatingButton()) using(var layers=new WindowLayers()) {
    app.Show(); existing.Show(); app.Activate(); Pump(150);
    IntPtr before=Native.GetForegroundWindow(); bool original=Top(app); Check(!original && Top(existing),"fixture initial window flags");
    mask.Apply(SystemInformation.VirtualScreen,true);
    var keep=new HashSet<IntPtr>{app.Handle,existing.Handle}; layers.Remember(keep);
    Check(layers.KeepAbove(app.Handle)&&layers.KeepAbove(existing.Handle),"native promote without activation");
    button.Show(); button.Raise(); Pump(200);
    Check(Native.GetForegroundWindow()!=mask.Handle && Native.GetForegroundWindow()!=button.Handle,"neither mask nor button becomes foreground");
    Check(Native.WindowFromPoint(new Native.XY(20,Screen.PrimaryScreen.Bounds.Bottom-10))==mask.Handle,"black region intercepts mouse hit testing");
    Check(Pixel(new Point(app.Left+100,app.Top+100))==0x00FF00,"focused application visible above opaque mask");
    foreach(var screen in Screen.AllScreens) Check(Pixel(new Point(screen.Bounds.Left+20,screen.Bounds.Bottom-10))==0,"taskbar area black "+screen.DeviceName);
    Screen last=Screen.PrimaryScreen; foreach(var screen in Screen.AllScreens) if(screen.Bounds.Left!=0) { last=screen; break; }
    app.Bounds=new Rectangle(last.Bounds.Left+250,last.Bounds.Top+220,640,480); Pump(150);
    Check(Pixel(new Point(app.Left+100,app.Top+100))==0x00FF00,"cross monitor move and resize visible without mask rebuild");
    Check(Pixel(new Point(400,400))==0,"previous app location black without mask rebuild");
    Check(OpacityMatches(button,.9),"native rendered button opacity 90 percent");
    Check(button.Width==39 && button.Height==39,"button diameter reduced to 70 percent");
    using(var image=FocusShade.ButtonRenderer.Draw(button.ClientSize,button.BackColor,button.ForeColor,false,false)) {
     bool partial=false; for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) { int alpha=image.GetPixel(x,y).A; if(alpha>0 && alpha<255) partial=true; }
     Check(image.GetPixel(0,0).A==0 && partial,"per pixel circular edge has fractional alpha antialiasing");
    }
    mask.Raise(); button.Raise(); Check(!layers.StackCorrect(mask.Handle,button.Handle,app.Handle),"topmost flag alone cannot confirm target above mask");
    layers.InvalidateStack(); layers.KeepAbove(app.Handle,button.Handle); layers.KeepAbove(existing.Handle,button.Handle);
    Check(layers.StackCorrect(mask.Handle,button.Handle,app.Handle),"actual application and button order confirmed");
    Native.SetWindowPos(app.Handle,Native.NOTOPMOST,0,0,0,0,0x13);
    layers.KeepAbove(app.Handle,button.Handle); Pump(50);
    Check(layers.StackCorrect(mask.Handle,button.Handle,app.Handle),"late window restore invalidates formerly successful promotion cache");
    var type=typeof(FloatingButton); type.GetField("edge",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(button,DockEdge.Top);
    type.GetField("area",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(button,Screen.PrimaryScreen.WorkingArea);
    type.GetMethod("Collapse",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(button,null); Pump(100);
    Check(OpacityMatches(button,.6),"native rendered capsule opacity 60 percent");
    Check(button.Height==6,"native collapsed capsule dimensions");
    using(var image=FocusShade.ButtonRenderer.Draw(button.ClientSize,button.BackColor,button.ForeColor,true,false)) {
     bool partial=false; for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) { int alpha=image.GetPixel(x,y).A; if(alpha>0 && alpha<255) partial=true; }
     Check(image.GetPixel(0,0).A==0 && partial,"capsule rounded ends use fractional alpha antialiasing");
    }
    type.GetMethod("Expand",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(button,null); Pump(100);
    Check(button.Width==39 && button.Height==39,"expanded button restores shape and opacity");
    Pump(100); int paints=button.PaintCount; Pump(2000); Check(button.PaintCount==paints,"idle button does not repaint periodically");
    using(var intruder=new Form { Text="FocusShade background layer test",Bounds=new Rectangle(1200,300,100,100) }) {
     intruder.Show(); Native.SetWindowPos(intruder.Handle,Native.TOPMOST,0,0,0,0,0x13); Pump(50);
     Check(!layers.StackCorrect(mask.Handle,button.Handle,app.Handle),"detect background window reasserting topmost");
     Check(layers.StackCorrect(mask.Handle,button.Handle,app.Handle,new HashSet<IntPtr>{intruder.Handle}),"recognized snap surface preserves app and opaque mask layers");
     mask.Raise(); layers.InvalidateStack(); layers.KeepAbove(app.Handle); layers.KeepAbove(existing.Handle); button.Raise();
     Check(layers.StackCorrect(mask.Handle,button.Handle,app.Handle),"repair background topmost ordering");
    }
    layers.Dispose(); Check(!Top(app)&&Top(existing),"restore ordinary and originally topmost windows");
    Native.SetWindowPos(existing.Handle,Native.TOPMOST,0,0,0,0,0x13);
    using(var desktopLayers=new WindowLayers()) {
     mask.CoverDesktop(SystemInformation.VirtualScreen,button);
     Check(desktopLayers.StackCorrect(mask.Handle,button.Handle,IntPtr.Zero),"show desktop taskbar reassertion keeps mask and button above background");
     Native.ShowWindow(button.Handle,7); Native.ShowWindow(mask.Handle,7); Pump(50);
     mask.CoverDesktop(SystemInformation.VirtualScreen,button); Pump(50);
     Check(!Native.IsIconic(button.Handle) && !Native.IsIconic(mask.Handle) && desktopLayers.StackCorrect(mask.Handle,button.Handle,IntPtr.Zero),"show desktop minimized tool surfaces restored without activation");
    }
    mask.Hide();
   }
   using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--blocked") { UseShellExecute=false,CreateNoWindow=true })) {
    IntPtr h=IntPtr.Zero; for(int i=0;i<100 && h==IntPtr.Zero;i++) { Pump(20); h=Native.FindWindow(null,"FocusShade blocked fixture"); }
    Check(h!=IntPtr.Zero,"start external hung-window fixture"); Pump(100);
    using(var external=new WindowLayers()) {
     var clock=System.Diagnostics.Stopwatch.StartNew(); bool accepted=external.KeepAbove(h); clock.Stop();
     Check(accepted && clock.ElapsedMilliseconds<500,"external blocked window cannot block emergency UI");
     Pump(2200); Check((Native.GetWindowLongPtr(h,-20).ToInt64()&8)!=0,"blocked fixture completes initial promotion");
     external.Dispose(); external.Remember(new HashSet<IntPtr>{h}); external.KeepAbove(h); external.Dispose(); Pump(6500);
     Check((Native.GetWindowLongPtr(h,-20).ToInt64()&8)==0,"pending promotion followed by original-state restore");
    }
    if(!child.HasExited) child.Kill(); child.WaitForExit();
   }
   lines.Add("TOTAL "+count+" native checks passed");
  } catch(Exception ex) { lines.Add("ERROR "+ex); Environment.ExitCode=1; }
  File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-test-results.txt"),lines.ToArray()); foreach(var line in lines) Console.WriteLine(line);
 }
 sealed class BlockingWindow : Form {
  bool armed;
  public BlockingWindow() { Text="FocusShade blocked fixture"; Bounds=new Rectangle(300,300,300,200); ShowInTaskbar=false; }
  protected override void OnShown(EventArgs e) { base.OnShown(e); armed=true; }
  protected override void WndProc(ref Message m) { if(armed && m.Msg==0x46) Thread.Sleep(2000); base.WndProc(ref m); }
 }
}

