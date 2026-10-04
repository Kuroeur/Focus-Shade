using System;
using System.IO;
using FocusShade;
class SettingsTests {
 static int count;
 static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL "+name); count++; Console.WriteLine("PASS "+name); }
 static int Main() {
  try {
   var settings=new Preferences();
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
   Console.WriteLine("TOTAL "+count+" settings checks passed"); return 0;
  } catch(Exception ex) { Console.WriteLine(ex); return 1; }
 }
}
