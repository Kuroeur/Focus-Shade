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
  if(args.Length>0) {
   if(args[0]=="--settings-startup") {
    Application.EnableVisualStyles();
    using(var ball=new FloatingButton()) using(var dialog=new SettingsDialog(new Preferences(),false,delegate { return null; })) using(var context=new ApplicationContext()) {
     ball.Show();
     ball.BeginInvoke(new Action(delegate {
      dialog.ShowForUser(); Pump(50);
      bool first=dialog.Visible && Native.IsWindowVisible(dialog.Handle);
      dialog.Hide(); dialog.ShowForUser(); Pump(50);
      bool repeat=dialog.Visible && Native.IsWindowVisible(dialog.Handle);
      ((Button)dialog.CancelButton).PerformClick(); bool closed=dialog.IsDisposed;
      bool reopened;
      using(var next=new SettingsDialog(new Preferences(),false,delegate { return null; })) { next.ShowForUser(); Pump(50); reopened=next.Visible && Native.IsWindowVisible(next.Handle); }
      Environment.ExitCode=first && repeat && closed && reopened?0:1;
      File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-startup-result.txt"),"first="+first+" repeat="+repeat+" CancelClosed="+closed+" reopened="+reopened);
      context.ExitThread();
     }));
     Application.Run(context);
    }
    return;
   }
   if(args[0]=="--transition") Application.Run(new Form { Text="FocusShade external transition fixture",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(300,300,300,200),BackColor=Color.Lime });
   else Application.Run(new BlockingWindow());
   return;
  }
  try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); Application.EnableVisualStyles();
   using(var startup=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SettingsStartupTests.exe"),"--settings-startup") { UseShellExecute=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden })) {
    bool ended=startup.WaitForExit(5000); if(!ended) { startup.Kill(); startup.WaitForExit(); }
    Check(ended && startup.ExitCode==0,"Hidden-started GUI process shows Settings in managed and native state");
   }
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
    button.SetOpacities(75,40); Pump(50); Check(OpacityMatches(button,.75),"configured normal opacity changes native button alpha"); button.SetOpacities(90,60);
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
    Rectangle preserved=button.Bounds; button.RecoverPosition(); Check(button.Bounds==preserved,"recovery preserves button position on an existing monitor");
    Pump(100); int paints=button.PaintCount; Pump(2000); Check(button.PaintCount==paints,"idle button does not repaint periodically");
    using(var intruder=new PassiveForm { Text="FocusShade background layer test",Bounds=new Rectangle(1200,300,100,100) }) {
     intruder.Show(); Pump(150); intruder.Raise();
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
   using(var fixture=new Form { Text="FocusShade transition fixture",Bounds=new Rectangle(300,300,300,200) }) {
    fixture.Show(); fixture.Activate(); Pump(50);
    using(var controller=new Controller(delegate { return fixture.Handle; })) {
     var fields=BindingFlags.NonPublic|BindingFlags.Instance;
     var controllerType=typeof(Controller);
     var state=(ShadeState)controllerType.GetField("state",fields).GetValue(controller);
     var shade=(MaskForm)controllerType.GetField("mask",fields).GetValue(controller);
     state.Enabled=true;
     controllerType.GetMethod("Update",fields).Invoke(controller,null);
     state.PendingUntil=DateTime.UtcNow.AddSeconds(1);
     controllerType.GetField("switchIntent",fields).SetValue(controller,ShellSurfaceKind.TaskView);
     controllerType.GetMethod("Update",fields).Invoke(controller,null);
     Check(!shade.Visible && state.Enabled,"task view suspends rendering without disabling shade");
     Check(Native.GetForegroundWindow()!=shade.Handle,"task view preparation does not focus the mask");
     controller.Emergency();
     var tray=(NotifyIcon)controllerType.GetField("tray",fields).GetValue(controller);
     Check(tray.Visible && tray.ContextMenuStrip==null,"tray delegates to the shared themed popup rather than a separate menu");
     controllerType.GetMethod("OpenMenu",fields).Invoke(controller,null); Pump(100);
     var shared=(SettingsDialog)controllerType.GetField("settingsWindow",fields).GetValue(controller);
     Check(shared!=null && !shared.IsSettings && shared.Visible,"shared right-click action opens menu before Settings");
     shared.Close();
     controllerType.GetMethod("Toggle",fields).Invoke(controller,null); Check(state.Enabled,"shared menu toggle can enable shade");
     controllerType.GetMethod("Toggle",fields).Invoke(controller,null); Check(!state.Enabled,"shared menu toggle can disable shade");
     var saved=(Preferences)controllerType.GetField("preferences",fields).GetValue(controller);
     var conflicting=saved.Copy(); conflicting.Toggle=new FocusShade.Shortcut(7,0x75);
     bool reserved=Native.RegisterHotKey(fixture.Handle,900,0x4007,0x75);
     Check(reserved,"reserve shortcut conflict fixture");
     try {
      string error=(string)controllerType.GetMethod("SavePreferences",fields).Invoke(controller,new object[] { conflicting,false });
      Check(error!=null && ((Preferences)controllerType.GetField("preferences",fields).GetValue(controller)).Toggle.Equals(saved.Toggle),"failed shortcut save preserves registered settings");
     } finally { Native.UnregisterHotKey(fixture.Handle,900); }
     state.Enabled=true; controllerType.GetMethod("OpenSettings",fields).Invoke(controller,null); Pump(150);
     var dialog=(SettingsDialog)controllerType.GetField("settingsWindow",fields).GetValue(controller);
     var controllerButton=(FloatingButton)controllerType.GetField("button",fields).GetValue(controller);
     var controllerLayers=(WindowLayers)controllerType.GetField("appLayers",fields).GetValue(controller);
     Check(dialog!=null && dialog.Visible && shade.Visible && Top(dialog) && controllerLayers.StackCorrect(shade.Handle,controllerButton.Handle,fixture.Handle,new HashSet<IntPtr>()),"settings window stays above active shade without foreground permission");
     dialog.Hide(); controllerType.GetMethod("OpenSettings",fields).Invoke(controller,null); Pump(150);
     Check(dialog.Visible && Top(dialog),"Settings action restores the existing hidden dialog above shade");
     dialog.WindowState=FormWindowState.Minimized; controllerType.GetMethod("OpenSettings",fields).Invoke(controller,null); Pump(150);
     Check(dialog.Visible && dialog.WindowState==FormWindowState.Normal,"Settings action restores a minimized dialog");
     using(var preview=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size)); preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-preview.png")); }
     var shortcutField=(ShortcutBox)typeof(SettingsDialog).GetField("toggle",fields).GetValue(dialog);
     shortcutField.Focus(); controller.Emergency();
     controllerType.GetMethod("Hotkey",fields).Invoke(controller,new object[] { 3 });
     Check(state.Enabled,"background settings cannot swallow the global toggle shortcut");
     ((Button)dialog.CancelButton).PerformClick(); Pump(100);
     Check(dialog.IsDisposed && controllerType.GetField("settingsWindow",fields).GetValue(controller)==null,"Cancel disposes the modeless settings dialog and clears its reference");
     controllerType.GetMethod("OpenSettings",fields).Invoke(controller,null); Pump(150);
     dialog=(SettingsDialog)controllerType.GetField("settingsWindow",fields).GetValue(controller);
     Check(dialog!=null && dialog.Visible,"Settings can be opened again after Cancel");
     dialog.Close(); controller.Emergency();
     using(var locked=new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HOTKEYS.txt"),FileMode.Open,FileAccess.Read,FileShare.None)) {
      controllerType.GetMethod("UpdateShortcutHints",fields).Invoke(controller,null);
      Check(tray.Visible,"locked diagnostic hint file cannot terminate the running tool");
     }
    }
   }
   using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--transition") { UseShellExecute=false,CreateNoWindow=true })) {
    try {
     IntPtr h=IntPtr.Zero; for(int i=0;i<100 && h==IntPtr.Zero;i++) { Pump(20); h=Native.FindWindow(null,"FocusShade external transition fixture"); }
     Check(h!=IntPtr.Zero,"start external task view transition fixture");
     bool denied=false;
     using(var controller=new Controller(delegate { return Native.FindWindow(null,"FocusShade external transition fixture"); },delegate { return denied; })) {
      var fields=BindingFlags.NonPublic|BindingFlags.Instance; var type=typeof(Controller);
      var state=(ShadeState)type.GetField("state",fields).GetValue(controller);
      var shade=(MaskForm)type.GetField("mask",fields).GetValue(controller);
      state.Enabled=true; type.GetMethod("Update",fields).Invoke(controller,null); Pump(150);
      state.PendingUntil=DateTime.UtcNow.AddSeconds(1); type.GetField("switchIntent",fields).SetValue(controller,ShellSurfaceKind.TaskView);
      type.GetMethod("Update",fields).Invoke(controller,null);
      var initialMonitor=Screen.FromHandle(h); var monitor=initialMonitor;
      foreach(var candidate in Screen.AllScreens) if(candidate.DeviceName!=initialMonitor.DeviceName) { monitor=candidate; break; }
      Native.SetWindowPos(h,IntPtr.Zero,monitor.WorkingArea.Left+300,monitor.WorkingArea.Top+300,300,200,0x4214); Pump(150);
      state.PendingUntil=DateTime.MinValue; type.GetMethod("Update",fields).Invoke(controller,null); Pump(200);
      type.GetMethod("Update",fields).Invoke(controller,null); Pump(100);
      h=Native.FindWindow(null,"FocusShade external transition fixture");
      Check(state.Enabled && shade.Visible && (Native.GetWindowLongPtr(h,-20).ToInt64()&8)!=0,"task view exit restores shading on another monitor");
      var bounds=Native.Bounds(h);
      Check(monitor.Bounds.Contains(bounds.Location) && (Screen.AllScreens.Length==1 || monitor.DeviceName!=initialMonitor.DeviceName),"transition fixture uses a different physical monitor when available");
      Check(Pixel(new Point(bounds.Left+50,bounds.Top+80))==0x00FF00,"resumed external application is visible above shade");
      var button=(FloatingButton)type.GetField("button",fields).GetValue(controller); Rectangle position=button.Bounds;
      denied=true; type.GetField("permissionTarget",fields).SetValue(controller,IntPtr.Zero); type.GetMethod("Update",fields).Invoke(controller,null); Pump(100);
      Check(state.Enabled && !shade.Visible && button.Visible && button.Bounds==position,"higher elevation suspends shade while preserving enabled state and button position");
      type.GetMethod("Toggle",fields).Invoke(controller,null); type.GetMethod("Toggle",fields).Invoke(controller,null);
      Check(state.Enabled && !shade.Visible,"toggle remains usable while higher elevation app is focused");
      denied=false; type.GetField("permissionTarget",fields).SetValue(controller,IntPtr.Zero); type.GetMethod("Update",fields).Invoke(controller,null); Pump(200);
      Check(state.Enabled && shade.Visible,"return to ordinary application restores enabled shade");
      controller.Emergency(); Check(!state.Enabled && !shade.Visible,"emergency off remains available after task view resume");
     }
    } finally { if(!child.HasExited) child.Kill(); child.WaitForExit(); }
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

