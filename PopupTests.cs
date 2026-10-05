using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms; using FocusShade;
class PopupTests {
 static List<string> lines=new List<string>(); static int count;
 static void Check(bool ok,string name) { lines.Add((ok?"PASS ":"FAIL ")+name); if(!ok) throw new Exception(name); count++; }
 static IEnumerable<Control> All(Control root) { foreach(Control c in root.Controls) { yield return c; foreach(var child in All(c)) yield return child; } }
 static Button Find(Form f,string text) { foreach(var c in All(f)) if(c is Button && c.Text==text) return (Button)c; throw new Exception("Missing "+text); }
 [STAThread] static void Main() {
  try {
   Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); Application.EnableVisualStyles();
   var showMenu=typeof(SettingsDialog).GetMethod("ShowMenu");
   Check(showMenu!=null,"menu and settings share a popup form");
   var settings=new Preferences(); typeof(Preferences).GetField("PanelOpacity").SetValue(settings,77);
   Preferences saved=null; bool toggled=false;
   using(var popup=new SettingsDialog(settings,false,delegate(Preferences next,bool startup) { saved=next; return null; })) {
    var area=Screen.PrimaryScreen.WorkingArea; Point anchor=new Point(area.Right-12,area.Bottom-12);
    showMenu.Invoke(popup,new object[] { anchor,false,new Action(delegate { toggled=true; }),new Action(delegate {}) }); Application.DoEvents();
    IntPtr handle=popup.Handle; Rectangle menu=popup.Bounds; Color theme=popup.BackColor;
    Check(Native.IsWindowVisible(handle) && Math.Abs(popup.Opacity-.77)<.001,"menu uses persisted shared opacity");
    Check((Native.GetWindowLongPtr(handle,-20).ToInt64()&8)!=0,"popup stays topmost even while shade is disabled");
    Check(Find(popup,"Turn on")!=null && Find(popup,"Exit")!=null,"popup menu has English toggle and Exit");
    Find(popup,"Settings").PerformClick(); Application.DoEvents();
    lines.Add("Settings bounds="+popup.Bounds+" dpi="+Native.GetDpiForWindow(handle));
    using(var preview=new Bitmap(popup.Width,popup.Height)) { popup.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size)); preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"popup-settings-preview.png")); }
    Check(popup.Handle==handle && popup.Right==menu.Right && popup.Bottom==menu.Bottom && area.Contains(popup.Bounds),"Settings expands in the same HWND at the menu anchor");
    Check(popup.Width<=400 && popup.Height<=390,"settings remains compact at 100 percent DPI");
    Check(Math.Abs(popup.Opacity-.77)<.001,"settings and menu share opacity");
    Check(popup.BackColor==theme,"menu and Settings share the same accent palette");
    Check(!popup.ShowInTaskbar && popup.FormBorderStyle==FormBorderStyle.None,"popup settings has no independent taskbar or titlebar");
    bool fits=true;
    foreach(var c in All(popup)) if(c.Visible && c is Label && c.MaximumSize.Width==0 && c.Text.Length!=0) fits&=c.Width>=c.GetPreferredSize(Size.Empty).Width;
    Check(fits,"single-line labels are not clipped in the compact layout");
    Find(popup,"Back").PerformClick(); Application.DoEvents();
    Check(popup.Bounds==menu && Find(popup,"Settings").Visible,"Back restores the same menu surface");
    Find(popup,"Settings").PerformClick();
    int originalWidth=popup.Width;
    popup.Font=new Font("Segoe UI",14.25f); typeof(SettingsDialog).GetMethod("ResizePage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(popup,null);
    Check(popup.Width>originalWidth && area.Contains(popup.Bounds),"larger text expands settings while retaining the screen anchor");
    popup.Font=new Font("Segoe UI",9.5f); typeof(SettingsDialog).GetMethod("ResizePage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(popup,null);
    foreach(var c in All(popup)) if(c is NumericUpDown && c.Name=="PanelOpacity") ((NumericUpDown)c).Value=62;
    Find(popup,"Save").PerformClick();
    Check(saved!=null && (int)typeof(Preferences).GetField("PanelOpacity").GetValue(saved)==62 && popup.IsDisposed,"Save persists panel opacity and closes popup");
   }
   using(var popup=new SettingsDialog(new Preferences(),false,delegate { return null; })) {
    showMenu.Invoke(popup,new object[] { new Point(400,250),true,new Action(delegate { toggled=true; }),new Action(delegate {}) });
    Find(popup,"Turn off").PerformClick(); Check(toggled && popup.IsDisposed,"menu toggle acts once and closes popup");
   }
   using(var popup=new SettingsDialog(new Preferences(),false,delegate { return "Changes could not be saved. Choose another shortcut, then try saving again. Your previous settings are unchanged."; })) {
    popup.ShowForUser(); int height=popup.Height; Find(popup,"Save").PerformClick(); Application.DoEvents();
    Check(!popup.IsDisposed && popup.Height>height && Screen.FromPoint(popup.Location).WorkingArea.Contains(popup.Bounds),"save error expands the popup without hiding the controls");
   }
   using(var traySurface=new Form { Text="FocusShade popup topmost fixture",TopMost=true,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(350,200,350,250) })
   using(var popup=new SettingsDialog(new Preferences(),false,delegate { return null; })) {
    traySurface.Show(); popup.ShowMenu(new Point(400,250),false,delegate {},delegate {}); Application.DoEvents();
    IntPtr hit=Native.WindowFromPoint(new Native.XY(popup.Left+20,popup.Top+20));
    Check(Native.GetAncestor(hit,2)==popup.Handle,"menu is hit-testable above a preexisting topmost tray-like surface");
   }
   lines.Add("TOTAL "+count+" popup checks passed");
  } catch(Exception ex) { lines.Add("ERROR "+ex); Environment.ExitCode=1; }
  File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"popup-test-results.txt"),lines.ToArray());
 }
}
