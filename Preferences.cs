using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
namespace FocusShade {
 public struct Shortcut {
  public uint Modifiers,Key;
  public Shortcut(uint modifiers,uint key) { Modifiers=modifiers; Key=key; }
  public override string ToString() {
   string name=Key>=0x70 && Key<=0x87?"F"+(Key-0x6F):Key>=0x30 && Key<=0x5A?((char)Key).ToString():Key==0x13?"Pause":Key==0x20?"Space":Key==0x0D?"Enter":Key==0x09?"Tab":"Key "+Key.ToString("X2");
   return ((Modifiers&2)!=0?"Ctrl+":"")+((Modifiers&1)!=0?"Alt+":"")+((Modifiers&4)!=0?"Shift+":"")+name;
  }
  public static bool IsSafe(uint modifiers,uint key) {
   bool real=key>=0x30 && key<=0x39 || key>=0x41 && key<=0x5A || key>=0x70 && key<=0x87 || key==0x13 || key==0x20 || key==0x0D || key==0x08 || key>=0x21 && key<=0x2E || key==0x09;
   return (modifiers&3)!=0 && (modifiers&~7u)==0 && real && !(key==0x09 && (modifiers&1)!=0) && !(key==0x2E && (modifiers&3)==3);
  }
 }
 public sealed class Preferences {
  public int NormalOpacity=90,DockedOpacity=60;
  public Shortcut Toggle=new Shortcut(3,0x79),Emergency=new Shortcut(3,0x78),Exit=new Shortcut(7,0x7B);
  public Preferences Copy() { return (Preferences)MemberwiseClone(); }
  public string Serialize() { return "normal="+NormalOpacity+"\ndocked="+DockedOpacity+"\ntoggle="+Encode(Toggle)+"\nemergency="+Encode(Emergency)+"\nexit="+Encode(Exit)+"\n"; }
  static string Encode(Shortcut key) { return key.Modifiers+":"+key.Key; }
  static Shortcut Decode(string text,Shortcut fallback) { var pieces=text.Split(':'); uint mod,key; return pieces.Length==2 && uint.TryParse(pieces[0],out mod) && uint.TryParse(pieces[1],out key) && Shortcut.IsSafe(mod,key)?new Shortcut(mod,key):fallback; }
  public static Preferences Parse(string text) {
   var result=new Preferences();
   foreach(string line in text.Split('\n')) {
    int split=line.IndexOf('='); if(split<0) continue;
    string key=line.Substring(0,split).Trim(),value=line.Substring(split+1).Trim(); int number;
    if(key=="normal" && int.TryParse(value,out number) && number>=10 && number<=100) result.NormalOpacity=number;
    if(key=="docked" && int.TryParse(value,out number) && number>=10 && number<=100) result.DockedOpacity=number;
    if(key=="toggle") result.Toggle=Decode(value,result.Toggle);
    if(key=="emergency") result.Emergency=Decode(value,result.Emergency);
    if(key=="exit") result.Exit=Decode(value,result.Exit);
   }
   if(result.ValidationError()!=null) { var defaults=new Preferences(); result.Toggle=defaults.Toggle; result.Emergency=defaults.Emergency; result.Exit=defaults.Exit; }
   return result;
  }
  public string ValidationError() {
   if(NormalOpacity<10 || NormalOpacity>100 || DockedOpacity<10 || DockedOpacity>100) return "Opacity must be between 10% and 100%.";
   if(!Shortcut.IsSafe(Toggle.Modifiers,Toggle.Key) || !Shortcut.IsSafe(Emergency.Modifiers,Emergency.Key) || !Shortcut.IsSafe(Exit.Modifiers,Exit.Key)) return "Use Ctrl or Alt with a letter, number, function key or navigation key. System switching shortcuts are reserved.";
   if(Toggle.Equals(Emergency) || Toggle.Equals(Exit) || Emergency.Equals(Exit)) return "Each shortcut must be different.";
   return null;
  }
  public static byte OpacityByte(int value) { return (byte)Math.Round(value*255.0/100); }
  public static string ToggleLabel(bool enabled) { return enabled?"Turn off":"Turn on"; }
  public static void Save(string path,Preferences value) {
   string error=value.ValidationError(); if(error!=null) throw new ArgumentException(error);
   string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try { File.WriteAllText(temporary,value.Serialize(),new UTF8Encoding(false)); if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); }
   finally { if(File.Exists(temporary)) File.Delete(temporary); }
  }
  public static Preferences Load(string path) { try { return File.Exists(path)?Parse(File.ReadAllText(path)):new Preferences(); } catch(IOException) { return new Preferences(); } catch(UnauthorizedAccessException) { return new Preferences(); } }
 }
 internal static class StartupRegistration {
  const string Run="Software\\Microsoft\\Windows\\CurrentVersion\\Run",Name="FocusShade";
  static string Command { get { return "\""+System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName+"\""; } }
  public static bool Enabled { get { using(var key=Registry.CurrentUser.OpenSubKey(Run)) return key!=null && string.Equals(key.GetValue(Name) as string,Command,StringComparison.OrdinalIgnoreCase); } }
  public static void Set(bool enabled) { using(var key=Registry.CurrentUser.CreateSubKey(Run)) { if(enabled) key.SetValue(Name,Command,RegistryValueKind.String); else key.DeleteValue(Name,false); } }
 }
}
