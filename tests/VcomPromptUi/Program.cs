using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kirin_Tool;
using Kirin_Tool.UI;
using System.Reflection;
AppBuilder.Configure<App>().WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var owner=new MainWindow();owner.Show();
var progress=new ProgressDialog(new());var progressTask=progress.ShowDialog(owner);
Dispatcher.UIThread.RunJobs();
// Native modality is not simulated by Headless; assert dialog ownership and
// continuation behavior rather than the platform window-enabled flag.
var method=typeof(MainWindow).GetMethod("ShowGlobalInteractionPromptAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
foreach(bool done in new[]{true,false}) {
 var task=(Task<bool>)method.Invoke(owner,new object[]{"Cable replug test",progress})!;
 Dispatcher.UIThread.RunJobs();
 var prompt=progress.OwnedWindows.Single();
 var button=prompt.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,done?"Done!":"Cancel"));
 if(!button.IsEffectivelyEnabled)throw new Exception("Prompt action disabled");
 button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
 if(!task.IsCompletedSuccessfully || task.Result!=done)throw new Exception("Prompt did not return expected result");
 if(!progress.IsEnabled)throw new Exception("Progress did not regain input");
 Console.WriteLine("PASS modal progress-owned prompt "+(done?"Done":"Cancel"));
}
progress.Close();owner.Close();
