using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kirin_Tool;
using Kirin_Tool.Services;
using Kirin_Tool.Models;
using System.Reflection;
int count=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
void Reject(Action a,string name){try{a();}catch(InvalidOperationException){Check(true,name);return;}throw new Exception(name);}
ProcessResult Reply(string value,int code=0)=>new(){Output=value,ExitCode=code};
Check(ChipCapabilities.Allows("hisi9000","read-info")&&ChipCapabilities.Allows("hisi9000","read-sn")&&ChipCapabilities.Allows("hisi9000","read-psid"),"read-only operations allowed");
Check(ChipCapabilities.Allows("hisi9000","reboot-usb-update"),"experimental rescue entry allowed");
foreach(var operation in new[]{"unlock-fastboot","write-sn","frp-remove","unlock-bootloader","enable-downgrade","sw-testpoint-enter","sw-testpoint-exit","dump-partitions","flash-xml","full-ota","convert-oem","reboot"})
 Check(!ChipCapabilities.Allows("hisi9000",operation),operation+" blocked");
Check(ChipCapabilities.Allows("hisi980","write-sn")&&ChipCapabilities.Allows("hisi990","unlock-fastboot"),"existing chip policy retained");
Check(RescuePartitionPlan.ForChip("hisi9000").Select(x=>x.Image).SequenceEqual(new[]{"ERECOVERY","ERECOVERY_RAMDISK","ERECOVERY_VENDOR"}),"Mate40 firmware image names");
Check(RescuePartitionPlan.ForChip("hisi9000")[0].Target=="rescue_recovery","explicit experimental destination");
Check(RescuePartitionPlan.ForChip("hisi980")[0]==new RescueImageTarget("ERECOVERY_KERNEL","rescue_recovery_kernel"),"older rescue mapping retained");
var psid=FastbootReadResponses.Psid(Reply("(bootloader) SN:TEST-SN\n(bootloader) IMEI:TEST-IMEI\n(bootloader) MEID:TEST-MEID\nOKAY [0.001s]"));
Check(psid["SN"]=="TEST-SN"&&psid["IMEI"]=="TEST-IMEI","PSID explicit SN parsed separately from IMEI");
Reject(()=>FastbootReadResponses.Psid(Reply("(bootloader) SN:A\nFAILED (remote: Command not allowed)",1)),"rejected PSID cannot become successful partial SN");
Reject(()=>FastbootReadResponses.Psid(Reply("SN:A\nSN:B")),"conflicting SN rejected");
Reject(()=>FastbootReadResponses.Psid(Reply("serialno:USB-IDENTIFIER")),"USB identifier never substituted");
Reject(()=>FastbootReadResponses.Psid(Reply("OKAY\n")),"empty PSID response rejected");
Check(FastbootReadResponses.OemVersion("(bootloader) NAM-LGRP1-CHN 102.0.1.272\nOKAY")=="NAM-LGRP1-CHN 102.0.1.272","colonless NAM OEM reply accepted");
Check(FastbootReadResponses.OemVersion("(bootloader) :NOH-LGRP1-CHN 104.2.0.196\nOKAY")=="NOH-LGRP1-CHN 104.2.0.196","Kirin leading colon retained");
Check(FastbootReadResponses.OemVersion("(bootloader) value\nFAILED")=="Unknown","failed version response rejected");
AppBuilder.Configure<App>().WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
var owner=new MainWindow();
// An accidental missing gate must never invoke a real fastboot client.
foreach(var field in new[]{"_fastbootClient","_xmlFastbootClient"})
 typeof(MainWindow).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,new FastbootClient("/tmp/kirin9000-test-no-device-client"));
owner.Show();Dispatcher.UIThread.RunJobs();
var combo=owner.FindControl<ComboBox>("CpuComboBox")!;
combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().Single(x=>x.Content?.ToString()?.Contains("hisi9000")==true);Dispatcher.UIThread.RunJobs();
Check(owner.FindControl<Border>("Kirin9000Notice")!.IsVisible,"experimental chip notice visible");
Check(owner.FindControl<TextBlock>("Kirin9000RescueHint")!.IsVisible,"experimental rescue mapping labelled");
Check(owner.FindControl<Expander>("UnlockBootloaderPanel")!.IsVisible,"unsupported unlock still accessible for notice");
var execute=typeof(MainWindow).GetMethod("ExecuteCommand",BindingFlags.Instance|BindingFlags.NonPublic)!;
void Notice(Action invoke,string name){invoke();Dispatcher.UIThread.RunJobs();var dialog=owner.OwnedWindows.Single();Check(dialog.Title=="Notice"&&dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Notice")&&dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Unsupported for kirin 9000"),name+" shows heading/body before device access");dialog.Close();Dispatcher.UIThread.RunJobs();}
foreach(var command in new[]{"unlock-fastboot","write-sn","frp-remove","enable-downgrade","sw-testpoint-enter","sw-testpoint-exit","unlock-bootloader"})
 Notice(()=>execute.Invoke(owner,[command]),command);
foreach(var handler in new[]{"DumpPartitions_Click","FlashFromXml_Click","StartFullOtaButton_Click","BtnConvertOem_Click","RebootDevice_Click"})
 Notice(()=>typeof(MainWindow).GetMethod(handler,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,[new Button(),new RoutedEventArgs(Button.ClickEvent)]),handler);
// Exercise the real rescue handler with synthetic archives and a local process stub.
// No physical USB or firmware payload is used.
string fixtureDir=Path.Combine(Path.GetTempPath(),"kirin9000-rescue-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureDir);
try {
 string fake=Path.Combine(fixtureDir,"fake-fastboot"), calls=Path.Combine(fixtureDir,"calls");
 File.WriteAllText(fake,"#!/usr/bin/python3\nimport sys,json\nwith open("+System.Text.Json.JsonSerializer.Serialize(calls)+",'a') as f: f.write(json.dumps(sys.argv[1:])+'\\n')\nif sys.argv[1:]==['devices']: print('TEST fastboot')\nelif sys.argv[1:]==['oem','lock-state','info']: print('(bootloader) FB LockState: LOCKED')\nelif 'flash' in sys.argv: print('FAILED (remote: partition length get error)'); sys.exit(1)\nelse: print('OKAY')\n");
 File.SetUnixFileMode(fake,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
 typeof(MainWindow).GetField("_fastbootClient",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,new FastbootClient(fake));
 void Fixture(string path,params string[] names) {
  using var w=new BinaryWriter(File.Create(path)); w.Write(new byte[12]);
  foreach(string name in names) { w.Write(new byte[]{0x55,0xaa,0x5a,0xa5});w.Write(102);w.Write(0);w.Write(System.Text.Encoding.ASCII.GetBytes("HWTEST00"));w.Write(0);w.Write(4u);w.Write(new byte[32]);var bytes=new byte[32];System.Text.Encoding.ASCII.GetBytes(name).CopyTo(bytes,0);w.Write(bytes);w.Write(new byte[10]);w.Write(new byte[4]);while(w.BaseStream.Position%4!=0)w.Write((byte)0); }
 }
 void PumpUntil(Func<bool> ready) {var until=DateTime.UtcNow.AddSeconds(15);while(!ready()){Dispatcher.UIThread.RunJobs();if(DateTime.UtcNow>until)throw new Exception("Stub rescue timed out");Thread.Sleep(10);}Dispatcher.UIThread.RunJobs();}
 foreach(bool complete in new[]{false,true}) {
  File.WriteAllText(calls,"");string app=Path.Combine(fixtureDir,"UPDATE.APP");
  Fixture(app,complete?new[]{"ERECOVERY","ERECOVERY_RAMDISK","ERECOVERY_VENDOR"}:new[]{"ERECOVERY_RAMDISK","ERECOVERY_VENDOR"});
  owner.UsbUpdateFile.FilePath=app;
  var task=(Task)typeof(MainWindow).GetMethod("HandleRebootToUsbUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,[])!;
  PumpUntil(()=>owner.OwnedWindows.Any());var progress=(Kirin_Tool.UI.ProcessDialogUapp)owner.OwnedWindows.Single();
  PumpUntil(()=>progress.FindControl<Button>("CloseButton")!.IsVisible);
  var argv=File.ReadAllLines(calls).Select(line=>System.Text.Json.JsonSerializer.Deserialize<string[]>(line)!).ToArray();
  if(!complete) {Check(!argv.Any(a=>a.Contains("flash")),"missing combined ERECOVERY prevents all writes");Check(progress.CurrentOperationText.Contains("no images flashed"),"missing-image result clearly reported");}
  else {Check(argv.Where(a=>a.Contains("flash")).Select(a=>a[1]).SequenceEqual(new[]{"rescue_recovery","rescue_recovery_ramdisk","rescue_recovery_vendor"}),"real handler dispatches experimental rescue aliases");Check(progress.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("partition length get error")==true),"bootloader failure shown in progress rows");Check(!argv.Any(a=>a.Contains("rescue_enter_recovery")),"failed writes prevent USB Update entry command");}
  progress.Close();PumpUntil(()=>task.IsCompleted);task.GetAwaiter().GetResult();
 }
} finally {Directory.Delete(fixtureDir,true);}
combo.SelectedItem=combo.Items.OfType<ComboBoxItem>().Single(x=>x.Content?.ToString()?.Contains("hisi980")==true);Dispatcher.UIThread.RunJobs();
Check(!owner.FindControl<Border>("Kirin9000Notice")!.IsVisible,"switching back clears experimental notice");
owner.Close();Console.WriteLine($"{count} Kirin9000/PSID checks passed; no device operations.");
