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
  readonly NumericUpDown normal=new NumericUpDown(),docked=new NumericUpDown();
  readonly CheckBox startup=new CheckBox { Text="Start with Windows",AutoSize=true };
  readonly ShortcutBox toggle=new ShortcutBox(),emergency=new ShortcutBox(),exit=new ShortcutBox();
  readonly Label message=new Label { AutoSize=false,ForeColor=Color.Firebrick };
  readonly Func<Preferences,bool,string> save;
  public void ShowForUser() {
   if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;
   if(!Visible) Show();
   // Hidden startup parameters can leave managed Visible true while the HWND is hidden.
   if(!Native.IsWindowVisible(Handle)) Native.ShowWindow(Handle,5);
   BringToFront(); Activate();
  }
  public bool CaptureRegisteredShortcut(Shortcut key) { foreach(var box in new ShortcutBox[] { toggle,emergency,exit }) if(box.Focused) { box.Value=key; return true; } return false; }
  public SettingsDialog(Preferences current,bool startWithWindows,Func<Preferences,bool,string> save) {
   this.save=save; Text="FocusShade Settings"; Font=new Font("Segoe UI",10); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=true; StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(440,410);
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=2,RowCount=10 };
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   for(int i=0;i<10;i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,i==8?58:i==9?42:31));
   AddRow(layout,0,"Button opacity (%)",normal); AddRow(layout,1,"Docked opacity (%)",docked);
   normal.Minimum=docked.Minimum=10; normal.Maximum=docked.Maximum=100; normal.Value=current.NormalOpacity; docked.Value=current.DockedOpacity;
   layout.Controls.Add(startup,0,2); layout.SetColumnSpan(startup,2); startup.Checked=startWithWindows;
   var heading=new Label { Text="Click a field, then press Ctrl/Alt + a key.",AutoSize=true,ForeColor=SystemColors.GrayText }; layout.Controls.Add(heading,0,3); layout.SetColumnSpan(heading,2);
   AddRow(layout,4,"Toggle shade",toggle); AddRow(layout,5,"Emergency off",emergency); AddRow(layout,6,"Exit app",exit);
   toggle.Value=current.Toggle; emergency.Value=current.Emergency; exit.Value=current.Exit;
   var hint=new Label { Text="Changes apply when saved.",AutoSize=true,ForeColor=SystemColors.GrayText }; layout.Controls.Add(hint,0,7); layout.SetColumnSpan(hint,2);
   message.Dock=DockStyle.Fill; layout.Controls.Add(message,0,8); layout.SetColumnSpan(message,2);
   var buttons=new FlowLayoutPanel { FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill };
   var cancel=new Button { Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true }; var apply=new Button { Text="Save",AutoSize=true };
   cancel.Click+=delegate { Close(); };
   apply.Click+=delegate { var next=new Preferences { NormalOpacity=(int)normal.Value,DockedOpacity=(int)docked.Value,Toggle=toggle.Value,Emergency=emergency.Value,Exit=exit.Value }; string error=next.ValidationError(); if(error==null) error=this.save(next,startup.Checked); if(error!=null) { message.Text=error; return; } DialogResult=DialogResult.OK; Close(); };
   buttons.Controls.Add(cancel); buttons.Controls.Add(apply); layout.Controls.Add(buttons,0,9); layout.SetColumnSpan(buttons,2); Controls.Add(layout); AcceptButton=apply; CancelButton=cancel;
  }
  static void AddRow(TableLayoutPanel layout,int row,string name,Control field) { layout.Controls.Add(new Label { Text=name,AutoSize=true,Anchor=AnchorStyles.Left },0,row); field.Anchor=AnchorStyles.Left|AnchorStyles.Right; layout.Controls.Add(field,1,row); }
 }
}
