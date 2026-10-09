using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kirin_Tool;
using Kirin_Tool.Models;
using Kirin_Tool.UI;
AppBuilder.Configure<App>().WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var names = new[]{"ERECOVERY_KERNEL","ERECOVERY_RAMDISK","ERECOVERY_VENDOR"};
var parts=names.Select(n=>new PartitionInfo{Name=n,Size=1024,FormattedSize="1 KB"}).ToList();
foreach(bool sources in new[]{false,true}) {
 using var cancel=new CancellationTokenSource();
 var dialog=sources?new ProcessDialogUapp(parts.Select(p=>(p,"base")).ToList(),cancel):new ProcessDialogUapp(parts,cancel);
 dialog.Show();Dispatcher.UIThread.RunJobs();
 var items=dialog.GetVisualDescendants().OfType<ItemsControl>().Single();
 if(items.ItemCount!=3)throw new Exception($"Partition rows missing (source constructor={sources}): {items.ItemCount}");
 foreach(var name in names)
  if(!dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==name))throw new Exception("Unrendered row: "+name);
 for(int i=0;i<3;i++)dialog.CompletePartitionByIndex(i,true);
 dialog.SetOverallComplete(true,"Rebooted to USB Update mode successfully!");Dispatcher.UIThread.RunJobs();
 if(dialog.CurrentOperationText!="Rebooted to USB Update mode successfully!")throw new Exception("Reboot result hidden by generic flash message");
 if(!dialog.FindControl<Button>("CloseButton")!.IsVisible || dialog.CanCancel)throw new Exception("Completion controls incorrect");
 dialog.SetOverallComplete(false,"Flash successful, but failed to enter USB Update mode.");Dispatcher.UIThread.RunJobs();
 if(!dialog.CurrentOperationText.Contains("failed to enter"))throw new Exception("Failure message lost");
 dialog.Close();Console.WriteLine("PASS rendered rows, reboot success/failure and completion controls; sources="+sources);
}
