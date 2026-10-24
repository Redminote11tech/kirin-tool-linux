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
Check(window.Title.Contains("v2.4.8") && window.Title.Contains("Linux port by Redminote11tech"),"version and Linux-port attribution appear in the window title");
Check(caption.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==window.Title),"caption and desktop title agree");
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
    foreach(var button in page.GetVisualDescendants().OfType<Button>().Where(b=>b.IsEffectivelyVisible && b.Bounds.Width>0 && b.Content is string))
    {
        var label=new TextBlock {Text=(string)button.Content!,FontFamily=button.FontFamily,FontSize=button.FontSize,FontWeight=button.FontWeight};
        label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        Check(button.Bounds.Width >= label.DesiredSize.Width+button.Padding.Left+button.Padding.Right+(button is WindowsButton ? 22 : 0),
            "action label fits: "+button.Content);
    }
    Capture(item.Tag!.ToString()!,window);
}
// Exercise selected-file UI without invoking firmware parsing or device commands.
nav.SelectedItem=window.FindControl<ListBoxItem>("FlashItem"); Pump();
var warning=window.FindControl<WindowsInfoBar>("FullOtaWarning")!;
void SelectFiles(int count)
{
    int index=0;
    foreach(var file in new[]{window.BasePtableFile,window.BaseUpdateFile,window.CustUpdateFile,window.PreloadUpdateFile,window.CustPtableFile,window.PreloadPtableFile})
    {
        typeof(FullOtaFile).GetField("_filePath",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(file,index++<count?file.FileType+".APP":null);
        foreach(var property in new[]{"HasFile","FileName","StatusText"})
            typeof(FullOtaFile).GetMethod("OnPropertyChanged",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(file,new object[]{property});
    }
    foreach(var property in new[]{"FullOtaStatusMessage","ShowFullOtaWarning","FullOtaWarningMessage","CanStartFullOta"})
        typeof(MainWindow).GetMethod("OnPropertyChanged",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly)!.Invoke(window,new object[]{property});
    Pump();
}
SelectFiles(0); Check(!warning.IsVisible,"no incomplete warning before choosing a file");
SelectFiles(1); Check(warning.IsVisible && warning.Message!.Contains("all 4"),"partial fastboot OTA shows the four-file warning");
Check(warning.GetVisualDescendants().OfType<WindowsSymbol>().Any(s=>s.Symbol=="ErrorCircle24" && s.Filled),"warning uses the original filled Fluent icon");
Capture("OTA-1-of-4",window);
warning.GetVisualDescendants().OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
Check(warning.IsDismissed && !warning.IsVisible,"warning close button dismisses the panel");
SelectFiles(2); Check(warning.IsVisible,"file-selection changes reopen the incomplete warning");
SelectFiles(4); Check(!warning.IsVisible,"complete four-file selection hides the warning"); Capture("OTA-4-of-4",window);
SelectFiles(0);window.IsUsbUpdateSelected=true;Pump();SelectFiles(1);
Check(warning.IsVisible && warning.Message!.Contains("all 6"),"partial USB Update OTA shows the six-file warning");Capture("OTA-1-of-6",window);
SelectFiles(6);Check(!warning.IsVisible,"complete six-file selection hides the warning");Capture("OTA-6-of-6",window);
SelectFiles(0);window.IsUsbUpdateSelected=false;Pump();
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
