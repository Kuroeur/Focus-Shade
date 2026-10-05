using System;
using System.IO;
using FocusShade;
class SettingsTests {
 static int count;
 static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL "+name); count++; Console.WriteLine("PASS "+name); }
 static int Main() {
  try {
   var settings=new Preferences();
   var panelField=typeof(Preferences).GetField("PanelOpacity");
   Check(panelField!=null && (int)panelField.GetValue(settings)==90,"menu and settings default to 90 percent opacity");
   panelField.SetValue(settings,77);
   Check((int)panelField.GetValue(Preferences.Parse(settings.Serialize()))==77,"panel opacity persists independently of button opacity");
   Check((int)panelField.GetValue(Preferences.Parse("normal=70\ndocked=50\n"))==90,"older settings retain default panel opacity");
   panelField.SetValue(settings,9); Check(settings.ValidationError()!=null,"invalid panel opacity is rejected"); panelField.SetValue(settings,90);
   Check(settings.NormalOpacity==90 && settings.DockedOpacity==60,"default button opacities");
   Check(settings.Toggle.ToString()=="Ctrl+Alt+F10" && settings.Emergency.ToString()=="Ctrl+Alt+F9","default shortcuts");
   settings.NormalOpacity=73; settings.DockedOpacity=42; settings.Toggle=new Shortcut(6,0x77);
   var copy=Preferences.Parse(settings.Serialize());
   Check(copy.NormalOpacity==73 && copy.DockedOpacity==42 && copy.Toggle.Equals(settings.Toggle),"settings round trip");
   var invalid=Preferences.Parse("normal=0\ndocked=101\ntoggle=0:65\nemergency=3:68\n");
   Check(invalid.NormalOpacity==90 && invalid.DockedOpacity==60,"invalid opacity preserves usable defaults");
   Check(invalid.Toggle.Equals(new Preferences().Toggle),"unsafe unmodified shortcut rejected");
   settings.Emergency=settings.Toggle;
   Check(settings.ValidationError()!=null,"duplicate shortcuts rejected");
   Check(Shortcut.IsSafe(3,0x44) && !Shortcut.IsSafe(4,0x44) && !Shortcut.IsSafe(3,0x10),"shortcuts require Ctrl or Alt and a real key");
   Check(Preferences.OpacityByte(90)==230 && Preferences.OpacityByte(60)==153,"configured opacity maps to existing visual alpha");
   Check(Preferences.ToggleLabel(false)=="Turn on" && Preferences.ToggleLabel(true)=="Turn off","English tray menu follows mask state");
   string path=Path.Combine(Path.GetTempPath(),"FocusShade-settings-test-"+Guid.NewGuid().ToString("N")+".ini");
   try {
    settings=new Preferences(); settings.NormalOpacity=80;
    Preferences.Save(path,settings); settings.NormalOpacity=70; Preferences.Save(path,settings);
    Check(Preferences.Load(path).NormalOpacity==70,"atomic settings replacement persists updates");
   } finally { if(File.Exists(path)) File.Delete(path); }
   var startup=typeof(Preferences).Assembly.GetType("FocusShade.StartupRegistration");
   var taskXml=startup.GetMethod("TaskXml",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
   Check(taskXml!=null,"administrator startup has a scheduled task definition");
   string executable=@"C:\Focus & Shade\FocusShade.exe",sid="S-1-5-21-123-456-789-1001";
   var document=new System.Xml.XmlDocument(); document.LoadXml((string)taskXml.Invoke(null,new object[]{executable,sid}));
   var ns=new System.Xml.XmlNamespaceManager(document.NameTable); ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
   Check(document.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:RunLevel",ns).InnerText=="HighestAvailable" && document.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:LogonType",ns).InnerText=="InteractiveToken","startup requests highest privilege in the interactive user session");
   Check(document.SelectSingleNode("/t:Task/t:Triggers/t:LogonTrigger/t:UserId",ns).InnerText==sid,"startup trigger is scoped to the current user");
   Check(document.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command",ns).InnerText==executable,"startup preserves and escapes the complete executable path");
   Check(!StartupRegistration.Enabled,"missing or nonmatching startup task can be read without breaking settings");
   Console.WriteLine("TOTAL "+count+" settings checks passed"); return 0;
  } catch(Exception ex) { Console.WriteLine(ex); return 1; }
 }
}
