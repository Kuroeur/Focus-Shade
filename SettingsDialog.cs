using System;
using System.Drawing;
using System.Windows.Forms;
namespace FocusShade {
 internal sealed class ShortcutBox : TextBox {
  Shortcut value;
  public Shortcut Value { get { return value; } set { this.value=value; Text=value.ToString(); } }
  public ShortcutBox() { ReadOnly=true; BackColor=SystemColors.Window; }
  protected override bool IsInputKey(Keys keyData) { return true; }
  protected override void OnKeyDown(KeyEventArgs e) {
   e.SuppressKeyPress=true; e.Handled=true;
   uint mod=(e.Control?2u:0)|(e.Alt?1u:0)|(e.Shift?4u:0),key=(uint)e.KeyCode;
   if(Shortcut.IsSafe(mod,key)) Value=new Shortcut(mod,key);
   base.OnKeyDown(e);
  }
 }
 internal sealed class SettingsDialog : Form {
  sealed class MenuButton : Button {
   public MenuButton() { SetStyle(ControlStyles.Selectable,false); TabStop=false; }
   protected override void Select(bool directed,bool forward) { }
  }
  readonly NumericUpDown normal=new NumericUpDown(),docked=new NumericUpDown(),panel=new NumericUpDown { Name="PanelOpacity" };
  readonly CheckBox startup=new CheckBox { Text="Start with Windows",AutoSize=true };
  readonly ShortcutBox toggle=new ShortcutBox(),emergency=new ShortcutBox(),exit=new ShortcutBox();
  readonly Label message=new Label { AutoSize=true,Visible=false };
  readonly Panel host=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(12) };
  readonly TableLayoutPanel settingsPage=new TableLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,ColumnCount=2 };
  readonly FlowLayoutPanel menuPage=new FlowLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,FlowDirection=FlowDirection.TopDown,WrapContents=false };
  readonly Button menuToggle=new MenuButton(),menuSettings=new MenuButton { Text="Settings" },menuExit=new MenuButton { Text="Exit" };
  public Action ModeChanged;
  readonly Func<Preferences,bool,string> save;
  Action toggleAction,exitAction; Point anchor; bool positioning;
  public bool IsSettings { get; private set; }
  public bool CaptureRegisteredShortcut(Shortcut key) { if(!IsSettings) return false; foreach(var box in new ShortcutBox[] { toggle,emergency,exit }) if(box.Focused) { box.Value=key; return true; } return false; }
  public SettingsDialog(Preferences current,bool startWithWindows,Func<Preferences,bool,string> save) {
   this.save=save; anchor=Cursor.Position; Text="FocusShade Settings"; Font=new Font("Segoe UI",9.5f); FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true; StartPosition=FormStartPosition.Manual; AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi; Padding=new Padding(1); Opacity=current.PanelOpacity/100.0;
   settingsPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); settingsPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   var header=new TableLayoutPanel { ColumnCount=3,AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8) };
   header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   var back=new Button { Text="Back",AutoSize=true }; back.Click+=delegate { SelectPage(false); };
   var close=new Button { Text="Close",AutoSize=true }; close.Click+=delegate { Close(); };
   header.Controls.Add(back,0,0); header.Controls.Add(new Label { Text="Settings",AutoSize=true,Anchor=AnchorStyles.None,Font=new Font(Font,FontStyle.Bold) },1,0); header.Controls.Add(close,2,0);
   Span(settingsPage,0,header);
   AddRow(settingsPage,1,"Button opacity (%)",normal); AddRow(settingsPage,2,"Docked opacity (%)",docked); AddRow(settingsPage,3,"Panel opacity (%)",panel);
   normal.Minimum=docked.Minimum=panel.Minimum=10; normal.Maximum=docked.Maximum=panel.Maximum=100; normal.Value=current.NormalOpacity; docked.Value=current.DockedOpacity; panel.Value=current.PanelOpacity;
   Span(settingsPage,4,startup); startup.Checked=startWithWindows;
   Span(settingsPage,5,new Label { Text="Select a field, then press Ctrl/Alt + a key.",AutoSize=true,MaximumSize=new Size(310,0),Margin=new Padding(3,5,3,5) });
   AddRow(settingsPage,6,"Toggle shade",toggle); AddRow(settingsPage,7,"Emergency off",emergency); AddRow(settingsPage,8,"Exit app",exit);
   toggle.Value=current.Toggle; emergency.Value=current.Emergency; exit.Value=current.Exit;
   Span(settingsPage,9,message);
   var buttons=new FlowLayoutPanel { FlowDirection=FlowDirection.RightToLeft,AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,8,0,0) };
   var cancel=new Button { Text="Cancel",AutoSize=true }; var apply=new Button { Text="Save",AutoSize=true };
   cancel.Click+=delegate { Close(); };
   apply.Click+=delegate {
    var next=new Preferences { NormalOpacity=(int)normal.Value,DockedOpacity=(int)docked.Value,PanelOpacity=(int)panel.Value,Toggle=toggle.Value,Emergency=emergency.Value,Exit=exit.Value };
    string error=next.ValidationError(); if(error==null) error=this.save(next,startup.Checked);
    if(error!=null) { message.Text=error; message.Visible=true; ResizePage(); return; }
    Close();
   };
   buttons.Controls.Add(cancel); buttons.Controls.Add(apply); Span(settingsPage,10,buttons);
   foreach(var button in new Button[] { menuToggle,menuSettings,menuExit }) { button.AutoSize=false; button.Size=new Size(160,32); button.Margin=new Padding(0,2,0,2); button.TextAlign=ContentAlignment.MiddleLeft; menuPage.Controls.Add(button); }
   menuToggle.Click+=delegate { if(toggleAction!=null) toggleAction(); };
   menuSettings.Click+=delegate { ShowForUser(); };
   menuExit.Click+=delegate { var action=exitAction; Close(); if(action!=null) action(); };
   host.Controls.Add(settingsPage); host.Controls.Add(menuPage); Controls.Add(host); AcceptButton=apply; CancelButton=cancel;
   settingsPage.Visible=false; menuPage.Visible=false; ApplyTheme();
  }
  static void Span(TableLayoutPanel table,int row,Control control) { table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(control,0,row); table.SetColumnSpan(control,2); }
  static void AddRow(TableLayoutPanel table,int row,string text,Control field) { table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(new Label { Text=text,AutoSize=true,Anchor=AnchorStyles.Left },0,row); field.Dock=DockStyle.Fill; table.Controls.Add(field,1,row); }
  int Dips(int value) { return Math.Max(1,(int)Math.Round(value*Native.GetDpiForWindow(Handle)/96.0)); }
  void SelectPage(bool settings) { IsSettings=settings; settingsPage.Visible=settings; menuPage.Visible=!settings; ResizePage(); if(ModeChanged!=null) ModeChanged(); }
  void ResizePage() {
   if(positioning || IsDisposed) return; positioning=true;
   try {
    var page=IsSettings?(Control)settingsPage:menuPage; int width=Dips(IsSettings?348:188);
    host.Padding=new Padding(Dips(12)); page.Width=width-host.Padding.Horizontal-2;
    if(IsSettings) {
     int column=0;
     foreach(Control child in settingsPage.Controls) if(settingsPage.GetColumnSpan(child)==1) {
      int required=child is TextBoxBase?TextRenderer.MeasureText(child.Text,child.Font).Width+Dips(16):child.GetPreferredSize(Size.Empty).Width;
      column=Math.Max(column,required+child.Margin.Horizontal);
     }
     width=Math.Max(width,column*2+host.Padding.Horizontal+2); page.Width=width-host.Padding.Horizontal-2;
    }
    if(IsSettings) { message.MaximumSize=new Size(page.Width-6,0); }
    else foreach(Control item in menuPage.Controls) item.Width=page.Width;
    page.PerformLayout(); Size preferred=page.GetPreferredSize(new Size(page.Width,0));
    Bounds=Geometry.Popup(anchor,new Size(width,preferred.Height+host.Padding.Vertical+2),Screen.FromPoint(anchor).WorkingArea);
    page.PerformLayout();
    int content=page.Padding.Top;
    foreach(Control child in page.Controls) if(child!=message || message.Text.Length!=0) content=Math.Max(content,child.Bottom+child.Margin.Bottom);
    Bounds=Geometry.Popup(anchor,new Size(width,content+page.Padding.Bottom+host.Padding.Vertical+2),Screen.FromPoint(anchor).WorkingArea);
   } finally { positioning=false; }
  }
  void Reveal() {
   if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;
   if(!Visible) Show();
   // Hidden startup can disagree with managed Visible; explicitly reveal a requested popup.
   if(!Native.IsWindowVisible(Handle)) Native.ShowWindow(Handle,IsSettings?5:4);
   ResizePage();
   if(IsSettings) { BringToFront(); Activate(); }
   else Native.SetWindowPos(Handle,Native.TOPMOST,0,0,0,0,0x13);
  }
  public void ShowMenu(Point point,bool enabled,Action toggleAction,Action exitAction) {
   SetMenuActions(toggleAction,exitAction); anchor=point; Location=point;
   SetShadeEnabled(enabled); SelectPage(false); Reveal();
  }
  public void SetMenuActions(Action toggleAction,Action exitAction) { this.toggleAction=toggleAction; this.exitAction=exitAction; }
  public void SetShadeEnabled(bool enabled) { menuToggle.Text=Preferences.ToggleLabel(enabled); }
  public void ShowForUser() { SelectPage(true); Reveal(); normal.Focus(); }
  protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=Native.TOOLWINDOW; return p; } }
  protected override bool ShowWithoutActivation { get { return !IsSettings; } }
  public void SetPanelOpacity(int value) { Opacity=value/100.0; }
  static Color Mix(Color a,Color b,double amount) { return Color.FromArgb((int)(a.R*(1-amount)+b.R*amount),(int)(a.G*(1-amount)+b.G*amount),(int)(a.B*(1-amount)+b.B*amount)); }
  void Theme(Control control,Color accent,Color ink) {
   control.ForeColor=ink; control.BackColor=accent;
   if(control is TextBoxBase || control is NumericUpDown) control.BackColor=Mix(accent,ink,.12);
   var button=control as Button;
   if(button!=null) { button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderColor=Mix(accent,ink,.3); button.BackColor=Mix(accent,ink,.08); button.Padding=new Padding(4,2,4,2); }
   foreach(Control child in control.Controls) Theme(child,accent,ink);
  }
  void ApplyTheme() { uint color; bool opaque; Color accent=Native.DwmGetColorizationColor(out color,out opaque)==0?ButtonVisuals.Accent(color):SystemColors.Highlight; Theme(this,accent,ButtonVisuals.Ink(accent)); message.ForeColor=ButtonVisuals.Ink(accent)==Color.White?Color.LightYellow:Color.Maroon; Invalidate(); }
  protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using(var pen=new Pen(Mix(BackColor,ForeColor,.3))) e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1); }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData) { if(keyData==Keys.Escape) { Close(); return true; } return base.ProcessCmdKey(ref msg,keyData); }
  protected override void WndProc(ref Message m) { if(m.Msg==0x21 && !IsSettings) { m.Result=new IntPtr(3); return; } base.WndProc(ref m); if(m.Msg==0x320 || m.Msg==0x1A) ApplyTheme(); if(m.Msg==0x2E0) ResizePage(); }
 }
}
