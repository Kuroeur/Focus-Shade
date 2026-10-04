using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace FocusShade {
 internal static class ButtonRenderer {
  public static Bitmap Draw(Size size,Color accent,Color ink,bool collapsed,bool active) {
   int w=Math.Max(1,size.Width),h=Math.Max(1,size.Height),scale=4;
   using(var large=new Bitmap(w*scale,h*scale,PixelFormat.Format32bppPArgb)) {
    using(var g=Graphics.FromImage(large)) {
     g.ScaleTransform(scale,scale); g.SmoothingMode=SmoothingMode.AntiAlias;
     using(var path=new GraphicsPath()) using(var fill=new SolidBrush(accent)) {
      float x=.5f,y=.5f,width=w-1f,height=h-1f,d=Math.Min(width,height);
      if(w==h) path.AddEllipse(x,y,width,height);
      else { path.AddArc(x,y,d,d,180,90); path.AddArc(x+width-d,y,d,d,270,90); path.AddArc(x+width-d,y+height-d,d,d,0,90); path.AddArc(x,y+height-d,d,d,90,90); path.CloseFigure(); }
      g.FillPath(fill,path);
     }
     if(!collapsed) using(var pen=new Pen(ink,Math.Max(1.25f,w/28f))) {
      float center=w/2f,r=w*.23f,small=w*.085f;
      g.DrawEllipse(pen,center-r,center-r,r*2,r*2);
      if(active) using(var fill=new SolidBrush(ink)) g.FillEllipse(fill,center-small,center-small,small*2,small*2);
      else g.DrawEllipse(pen,center-small,center-small,small*2,small*2);
     }
    }
    var image=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
    using(var g=Graphics.FromImage(image)) { g.CompositingMode=CompositingMode.SourceCopy; g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.DrawImage(large,new Rectangle(0,0,w,h)); }
    return image;
   }
  }
  public static void Present(IntPtr window,Point location,Bitmap image,byte opacity) {
   IntPtr screen=Native.GetDC(IntPtr.Zero),dc=Native.CreateCompatibleDC(screen),bitmap=IntPtr.Zero,old=IntPtr.Zero;
   try {
    bitmap=image.GetHbitmap(Color.FromArgb(0)); old=Native.SelectObject(dc,bitmap);
    var position=new Native.XY(location.X,location.Y); var size=new Native.XY(image.Width,image.Height); var origin=new Native.XY(0,0);
    var blend=new Native.Blend { Op=0,Flags=0,Alpha=opacity,Format=1 };
    if(!Native.UpdateLayeredWindow(window,screen,ref position,ref size,dc,ref origin,0,ref blend,2)) throw new System.ComponentModel.Win32Exception();
   } finally { if(old!=IntPtr.Zero) Native.SelectObject(dc,old); if(bitmap!=IntPtr.Zero) Native.DeleteObject(bitmap); Native.DeleteDC(dc); Native.ReleaseDC(IntPtr.Zero,screen); }
  }
 }
}
