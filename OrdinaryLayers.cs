using System;using System.Collections.Generic;using FocusShade;
namespace FocusShade {
 internal sealed class OrdinaryLayers:IDisposable {
  struct Identity {public uint Process,Thread;}
  readonly Dictionary<IntPtr,Identity> originalTop=new Dictionary<IntPtr,Identity>();
  readonly Dictionary<IntPtr,DateTime> requests=new Dictionary<IntPtr,DateTime>();
  readonly Dictionary<IntPtr,DateTime> positions=new Dictionary<IntPtr,DateTime>();
  HashSet<IntPtr> applications=new HashSet<IntPtr>(),allowed=new HashSet<IntPtr>();
  public string Problem="";
  static bool Top(IntPtr h){return (Native.GetWindowLongPtr(h,-20).ToInt64()&8)!=0;}
  static bool Above(IntPtr h,IntPtr mask){for(IntPtr current=Native.GetWindow(mask,3);current!=IntPtr.Zero;current=Native.GetWindow(current,3))if(current==h)return true;return false;}
  public void ForgetDestroyed(IntPtr h){originalTop.Remove(h);requests.Remove(h);positions.Remove(h);applications.Remove(h);allowed.Remove(h);}
  bool RequestDue(IntPtr h,bool demotion){var pending=demotion?requests:positions;DateTime previous;if(pending.TryGetValue(h,out previous)&&DateTime.UtcNow<previous.AddMilliseconds(500))return false;pending[h]=DateTime.UtcNow;return true;}
  public void Prepare(HashSet<IntPtr> group,HashSet<IntPtr> surfaces,Func<IntPtr,bool> applicationWindow){
   applications=new HashSet<IntPtr>(group);allowed=new HashSet<IntPtr>(surfaces);
   Native.EnumWindows(delegate(IntPtr h,IntPtr p){if(!allowed.Contains(h)&&applicationWindow(h)&&Top(h)){if(!originalTop.ContainsKey(h)){uint pid;uint thread=Native.GetWindowThreadProcessId(h,out pid);originalTop.Add(h,new Identity{Process=pid,Thread=thread});}if(RequestDue(h,true))Native.SetWindowPos(h,Native.NOTOPMOST,0,0,0,0,0x4213);}return true;},IntPtr.Zero);
  }
  public bool Ready(){foreach(var h in applications)if(Native.IsWindow(h)&&Top(h)){Problem="waiting for application demotion";return false;}return true;}
  public IntPtr Lowest(){IntPtr result=IntPtr.Zero;Native.EnumWindows(delegate(IntPtr h,IntPtr p){if(applications.Contains(h)&&Native.IsWindowVisible(h)&&!Native.IsIconic(h))result=h;return true;},IntPtr.Zero);return result;}
  public void PutBackgroundBehind(IntPtr mask,Func<IntPtr,bool> applicationWindow){
   Native.EnumWindows(delegate(IntPtr h,IntPtr p){if(!applications.Contains(h)&&!allowed.Contains(h)&&applicationWindow(h)&&(Top(h)||Above(h,mask))&&RequestDue(h,false))Native.SetWindowPos(h,mask,0,0,0,0,0x4213);return true;},IntPtr.Zero);
  }
  public bool StackCorrect(IntPtr mask,IntPtr button,IntPtr target){return StackCorrect(mask,button,target,new HashSet<IntPtr>(),IntPtr.Zero);}
  public bool StackCorrect(IntPtr mask,IntPtr button,IntPtr target,HashSet<IntPtr> shell){return StackCorrect(mask,button,target,shell,IntPtr.Zero);}
  public bool StackCorrect(IntPtr mask,IntPtr button,IntPtr target,HashSet<IntPtr> shell,IntPtr popup){
   Problem="";if(Top(mask)){Problem="ordinary mask became topmost";return false;}
   var ranks=new Dictionary<IntPtr,int>();uint ourPid;Native.GetWindowThreadProcessId(mask,out ourPid);int limit=1024;
   for(IntPtr h=Native.GetWindow(mask,3);h!=IntPtr.Zero&&limit-->0;h=Native.GetWindow(h,3)){
    ranks[h]=ranks.Count;if(!Native.IsWindowVisible(h)||Native.IsIconic(h)||Native.Cloaked(h))continue;uint pid;Native.GetWindowThreadProcessId(h,out pid);
    string cls=Native.Class(h);bool taskbar=(cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd")&&ShellWindows.IsTraySurface(h);
    bool lateSnap=cls=="XamlExplorerHostIslandWindow" && ShellWindows.IsSnapBar(h,Native.DragActive(target,false));
    if(pid!=ourPid&&!applications.Contains(h)&&!allowed.Contains(h)&&!shell.Contains(h)&&!taskbar&&!lateSnap){var rect=Native.Bounds(h);if(rect.Width>1&&rect.Height>1){Problem="unexpected-above-mask="+h+" class="+cls;return false;}}
   }
   foreach(var h in applications)if(Native.IsWindowVisible(h)&&!Native.IsIconic(h)&&!ranks.ContainsKey(h)){Problem="application-below-mask="+h;return false;}
   if(!ranks.ContainsKey(button)){Problem="button-below-mask";return false;}
   foreach(var h in applications)if(ranks.ContainsKey(h)&&ranks[h]>ranks[button]){Problem="application-above-button="+h;return false;}
   if(popup!=IntPtr.Zero&&Native.IsWindowVisible(popup)){if(!ranks.ContainsKey(popup)){Problem="popup-below-mask";return false;}foreach(var h in applications)if(ranks.ContainsKey(h)&&ranks[h]>ranks[popup]){Problem="application-above-popup="+h;return false;}}
   return true;
  }
  public void Dispose(){foreach(var pair in originalTop){uint pid;uint thread=Native.GetWindowThreadProcessId(pair.Key,out pid);if(Native.IsWindow(pair.Key)&&pid==pair.Value.Process&&thread==pair.Value.Thread)Native.SetWindowPos(pair.Key,Native.TOPMOST,0,0,0,0,0x4213);}originalTop.Clear();requests.Clear();positions.Clear();applications.Clear();allowed.Clear();}
 }
}
