using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kirin_Tool;

AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
Exception? unhandled = null;
Dispatcher.UIThread.UnhandledException += (_, e) => { unhandled=e.Exception; e.Handled=true; };
var owner=new MainWindow();owner.Show();
var model=owner.FindControl<TextBox>("TxtDeviceModel")!;
var vendor=owner.FindControl<TextBox>("TxtVendor")!;
var file=owner.FindControl<TextBox>("TxtOemInfoFile")!;
var convert=owner.FindControl<Button>("BtnConvertOem")!;
void Test(string name,string? m,string? v,string? f,string expected) {
    model.Text=m;vendor.Text=v;file.Text=f;unhandled=null;
    convert.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    if(unhandled!=null)throw new Exception(name+": UI exception",unhandled);
    var dialog=owner.OwnedWindows.SingleOrDefault();
    if(dialog?.Title!="Input Error" || !dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==expected))
        throw new Exception(name+": expected input validation dialog");
    dialog.Close();Dispatcher.UIThread.RunJobs();
    Console.WriteLine("PASS "+name);
}
// Untouched Avalonia TextBoxes have null text. All paths below end before device I/O.
Test("untouched empty form",null,null,null,"Device Model and Vendor/Country cannot be empty.");
Test("missing vendor","TEST-MODEL",null,null,"Device Model and Vendor/Country cannot be empty.");
Test("whitespace model","   ","test/test",null,"Device Model and Vendor/Country cannot be empty.");
// Force the file-based CPU route by selecting the first supported combo item (hisi65x_a).
owner.FindControl<ComboBox>("CpuComboBox")!.SelectedIndex=1;
Test("missing file with valid fields","TEST-MODEL","test/test",null,"Please select a valid OEMInfo file.");
Test("empty file text","TEST-MODEL","test/test","","Please select a valid OEMInfo file.");
owner.Close();
Console.WriteLine("5 OEMInfo UI checks passed; headless, no device access.");
