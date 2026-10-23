using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kirin_Tool;
using Kirin_Tool.UI;
using Kirin_Tool.Services;
using Kirin_Tool.Models;

AppBuilder.Configure<App>().WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
void Pump() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
string? output = args.FirstOrDefault();
if (output != null) Directory.CreateDirectory(output);
var window = new MainWindow(); window.Show(); Pump();
void Capture(string name, Window target) { Pump(); if(output != null) { using var bitmap = target.CaptureRenderedFrame(); Check(bitmap != null,"Skia frame: " + name); bitmap!.Save(Path.Combine(output,name+".png")); } }
Check(window.ClientSize == new Size(1000,700),"main content keeps the Windows 1000 x 700 dimensions");
Check(window.SystemDecorations == SystemDecorations.BorderOnly,"single app title bar instead of duplicated desktop chrome");
Check(window.GetVisualDescendants().OfType<WindowCaption>().Count() == 1,"accessible custom window caption rendered");
var caption=window.GetVisualDescendants().OfType<WindowCaption>().Single();
var captionButtons=caption.GetVisualDescendants().OfType<Button>().ToArray();
captionButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
Check(window.WindowState==WindowState.Maximized,"caption maximizes");
captionButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
Check(window.WindowState==WindowState.Normal,"caption restores");
Capture("welcome",window);
var nav=window.FindControl<ListBox>("NavigationListBox")!;
foreach(var item in nav.Items.OfType<ListBoxItem>())
{
    nav.SelectedItem=item; Pump();
    var page=window.FindControl<StackPanel>(item.Tag!.ToString()+"Page")!;
    Check(page.IsVisible,"navigation routes to "+item.Tag);
    Check(item.Bounds.Height==40,"navigation row height: "+item.Tag);
    // Exercise expansion through a real pointer click, not just IsExpanded assignment.
    foreach(var expander in page.GetVisualDescendants().OfType<WindowsExpander>().ToArray())
    {
        bool before=expander.IsExpanded;
        var header=expander.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(b=>b.Classes.Contains("card-header"));
        if(header == null || !expander.IsEffectivelyVisible) continue;
        var point=header.TranslatePoint(new Point(header.Bounds.Width/2,header.Bounds.Height/2),window);
        if(point is { } p && p.Y>48 && p.Y<680)
        {
            window.MouseDown(p,MouseButton.Left);window.MouseUp(p,MouseButton.Left);Pump();
            Check(expander.IsExpanded!=before,"pointer toggles card: "+expander.Header);
            expander.IsExpanded=before;Pump();
        }
    }
    if(item.Tag!.ToString()=="DeviceInfo")
    {
        window.FindControl<Expander>("DeviceInfoExpander")!.IsExpanded=true;Pump();
        var cards=page.GetVisualDescendants().OfType<WindowsCard>().ToArray();
        Check(cards.Length==8 && cards.All(c=>c.Bounds.Height>=52),"eight complete inline information cards");
        foreach(var field in new[]{"InfoFbLock","InfoUserLock","InfoDeviceModel","InfoVendorCountry","InfoBuildNumber","InfoBaseVersion","InfoCustomVersion","InfoPreloadVersion"})
          Check(window.FindControl<TextBlock>(field) is {IsVisible:true},"named information field retained: "+field);
    }
    Capture(item.Tag!.ToString()!,window);
}
// Owner/modal semantics and keyboard dismissal remain real Avalonia windows.
var dialogs=new DialogService(window);
var message=dialogs.ShowMessageBox("Notice","Unsupported for kirin 9000",showHeading:true);Pump();
var modal=window.OwnedWindows.Single();
Check(modal.Owner==window && modal.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Notice"),"notice heading and modal owner retained");
Capture("notice",modal);
modal.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
Check(message.IsCompletedSuccessfully,"notice closes via OK");
var ordinary=dialogs.ShowMessageBox("Read failed","Example query failure; no device operations.");Pump();
modal=window.OwnedWindows.Single();
Check(modal.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Read failed"),"ordinary messages retain a visible heading without desktop chrome");
modal.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
Check(ordinary.IsCompletedSuccessfully,"ordinary message closes via OK");
var confirm=dialogs.ShowConfirmDialog("Confirm","Example only; no device operations.");Pump();
modal=window.OwnedWindows.Single();
Check(modal.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Confirm"),"confirmation title is rendered inside the dialog");
modal.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
Check(confirm.IsCompletedSuccessfully && !confirm.Result,"confirmation cancellation retained");
using(var cancel=new CancellationTokenSource())
{
    var progress=new ProcessDialogUapp(new List<PartitionInfo>{new(){Name="ERECOVERY",Size=1024,FormattedSize="1 KB"}},cancel);
    var completion=progress.ShowDialog<bool?>(window);Pump();
    const string failure="FAILED (remote: partition length get error; the complete bootloader response must remain readable in the status row)";
    progress.CompletePartitionByIndex(0,false,failure);Pump();
    var status=progress.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text==failure);
    Check(status.Bounds.Height>20 && status.Bounds.Width<500,"long bootloader error wraps into the status column");
    Capture("progress-failure",progress);
    progress.Close();Pump();
    Check(completion.IsCompleted,"progress modal can close");
}
window.Close();
Console.WriteLine($"{checks} visual/interaction checks passed; no device operations.");
