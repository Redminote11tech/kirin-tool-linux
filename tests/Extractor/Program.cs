using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kirin_Tool;
using Kirin_Tool.Services;

string root = Path.Combine(Path.GetTempPath(), "kirin-extractor-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); checks++; }
void Fixture(string path, params (string Name, byte[] Data)[] entries)
{
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write(new byte[12]); // Container prefix.
    foreach (var entry in entries)
    {
        writer.Write(new byte[] {0x55, 0xaa, 0x5a, 0xa5});
        writer.Write(102); writer.Write(0); writer.Write(Encoding.ASCII.GetBytes("HWTEST00")); writer.Write(0);
        writer.Write((uint)entry.Data.Length); writer.Write(new byte[32]);
        byte[] name = new byte[32]; Encoding.ASCII.GetBytes(entry.Name).CopyTo(name, 0); writer.Write(name);
        writer.Write(new byte[10]); writer.Write(entry.Data);
        while (writer.BaseStream.Position % 4 != 0) writer.Write((byte)0);
    }
}
async Task Fails(Func<Task> action, string label)
{
    bool failed = false;
    try { await action(); } catch (Exception) { failed = true; }
    Check(failed, label);
}
try
{
    byte[] boot = Enumerable.Range(0, 4097).Select(i => (byte)i).ToArray();
    byte[] cust = Encoding.ASCII.GetBytes("cust payload");
    string app = Path.Combine(root, "UPDATE.APP");
    Fixture(app, ("BOOT", boot), ("CUST", cust), ("BOOT", new byte[]{7,8}), ("EMPTY", Array.Empty<byte>()));
    var entries = PartitionExtractor.Read(app);
    Check(entries.Count == 4 && entries[0].Partition.Size == boot.Length && entries[2].FileName == "BOOT_2.img", "list all entries, sizes and distinct duplicate filenames");
    entries[1].IsSelected = false; entries[3].IsSelected = false;
    string output = Path.Combine(root, "selected");
    int extracted = await PartitionExtractor.ExtractAsync(app, output, entries);
    Check(extracted == 2 && File.ReadAllBytes(Path.Combine(output, "BOOT.img")).SequenceEqual(boot) &&
        File.ReadAllBytes(Path.Combine(output, "BOOT_2.img")).SequenceEqual(new byte[]{7,8}) && Directory.GetFiles(output).Length == 2, "only ticked images extracted byte-for-byte");
    await Fails(() => PartitionExtractor.ExtractAsync(app, output, entries), "existing images refused");
    Check(File.ReadAllBytes(Path.Combine(output, "BOOT.img")).SequenceEqual(boot), "existing output preserved");
    entries.ForEach(e => e.IsSelected = false);
    await Fails(() => PartitionExtractor.ExtractAsync(app, Path.Combine(root,"none"), entries), "empty selection refused");
    entries[3].IsSelected = true;
    await PartitionExtractor.ExtractAsync(app, Path.Combine(root,"empty"), entries);
    Check(new FileInfo(Path.Combine(root,"empty","EMPTY.img")).Length == 0, "zero-length entry extracted");
    // Base, Cust and Preload all use the same format, without partition-name remapping.
    foreach (string name in new[]{"BASE", "CUST", "PRELOAD"})
    {
        string input = Path.Combine(root, name+".APP"); Fixture(input, (name, cust));
        var list = PartitionExtractor.Read(input);
        await PartitionExtractor.ExtractAsync(input, Path.Combine(root,name), list);
        Check(File.ReadAllBytes(Path.Combine(root,name,name+".img")).SequenceEqual(cust), name+" archive extraction");
    }
    string truncated = Path.Combine(root,"truncated.app"); File.WriteAllBytes(truncated, File.ReadAllBytes(app)[..^12]);
    await Fails(() => Task.Run(() => PartitionExtractor.Read(truncated)), "truncated archive refused");
    string invalid = Path.Combine(root,"invalid.app"); Fixture(invalid,("../escape",cust));
    await Fails(() => Task.Run(() => PartitionExtractor.Read(invalid)), "unsafe partition name refused");
    Fixture(invalid,("BOOT",cust));
    var badHeader = File.ReadAllBytes(invalid); BitConverter.GetBytes(97).CopyTo(badHeader,16); File.WriteAllBytes(invalid,badHeader);
    await Fails(() => Task.Run(() => PartitionExtractor.Read(invalid)), "invalid header size refused");
    File.WriteAllText(invalid,"not an update app");
    await Fails(() => Task.Run(() => PartitionExtractor.Read(invalid)), "non-archive refused");
    string changed = Path.Combine(root,"changed.app"); Fixture(changed,("BOOT",boot));
    var previous = PartitionExtractor.Read(changed); Fixture(changed,("CUST",cust));
    await Fails(() => PartitionExtractor.ExtractAsync(changed,Path.Combine(root,"changed"),previous), "changed source table refused");
    string large = Path.Combine(root,"large.app"); Fixture(large,("SYSTEM",new byte[3*1024*1024]));
    using var cancellation = new CancellationTokenSource();
    await Fails(() => PartitionExtractor.ExtractAsync(large,Path.Combine(root,"cancel"),PartitionExtractor.Read(large),cancellation.Token,
        p => { if (p.Bytes>0) cancellation.Cancel(); }), "mid-image cancellation stops extraction");
    Check(Directory.GetFiles(Path.Combine(root,"cancel")).Length == 0,"cancelled image and temporary file removed");
    using var scanCancellation = new CancellationTokenSource(); scanCancellation.Cancel();
    await Fails(() => Task.Run(() => PartitionExtractor.Read(app,scanCancellation.Token)),"archive scan cancellation");

    // Index a sparse archive whose later entry begins above 4 GiB without reading its payload.
    string huge = Path.Combine(root,"huge.app"); Fixture(huge,("SYSTEM",new byte[1]));
    using (var stream = new FileStream(huge,FileMode.Open,FileAccess.Write))
    {
        stream.Position=36; using var writer=new BinaryWriter(stream,Encoding.UTF8,true); writer.Write(uint.MaxValue);
        long next=(114L+uint.MaxValue+3)&~3L;
        stream.SetLength(next); stream.Position=next;
        string tail=Path.Combine(root,"tail.app"); Fixture(tail,("PRELOAD",cust));
        writer.Write(File.ReadAllBytes(tail)[12..]);
    }
    var hugeEntries=PartitionExtractor.Read(huge); hugeEntries[0].IsSelected=false;
    Check(hugeEntries.Count==2 && hugeEntries[1].Partition.DataOffset>uint.MaxValue,"64-bit offsets in archives above 4 GiB");
    await PartitionExtractor.ExtractAsync(huge,Path.Combine(root,"huge"),hugeEntries);
    Check(File.ReadAllBytes(Path.Combine(root,"huge","PRELOAD.img")).SequenceEqual(cust),"selected partition above 4 GiB extracted");

    // Run the actual UI entry points, render the bound rows, tick choices and start extraction.
    AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var window = new MainWindow(); window.Show();
    window.FindControl<ListBox>("NavigationListBox")!.SelectedItem = window.FindControl<ListBoxItem>("ExtractorItem");
    Dispatcher.UIThread.RunJobs();
    Check(window.FindControl<StackPanel>("ExtractorPage")!.IsVisible && !window.FindControl<StackPanel>("WelcomePage")!.IsVisible,"Extractor Tools navigation");
    var load = (Task)typeof(MainWindow).GetMethod("LoadExtractionSourceAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{app})!;
    void Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while(!task.IsCompleted && DateTime.UtcNow<deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        if(!task.IsCompleted) throw new Exception("UI task timed out");
        task.GetAwaiter().GetResult(); Dispatcher.UIThread.RunJobs();
    }
    Pump(load);
    var items = window.FindControl<ItemsControl>("ExtractorPartitions")!;
    Check(items.ItemCount==4 && items.GetVisualDescendants().OfType<CheckBox>().Count()==4,"all partition checkboxes rendered");
    window.FindControl<TextBox>("ExtractorDestinationPath")!.Text=Path.Combine(root,"ui");
    typeof(MainWindow).GetField("_extractionDestination",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,Path.Combine(root,"ui"));
    window.FindControl<Button>("ExtractorSelectNoneButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Check(!window.FindControl<Button>("ExtractorStartButton")!.IsEnabled && window.FindControl<TextBlock>("ExtractorSelectionText")!.Text=="0/4 partitions selected","Select None disables extraction");
    window.FindControl<Button>("ExtractorSelectAllButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Check(window.FindControl<TextBlock>("ExtractorSelectionText")!.Text=="4/4 partitions selected","Select All updates checkboxes");
    var rows = items.GetVisualDescendants().OfType<CheckBox>().ToList(); rows[1].IsChecked=false; rows[3].IsChecked=false;
    Dispatcher.UIThread.RunJobs();
    var start = window.FindControl<Button>("ExtractorStartButton")!;
    Check(start.IsEnabled && window.FindControl<TextBlock>("ExtractorSelectionText")!.Text=="2/4 partitions selected","checkbox binding updates selection and extract action");
    string? screenshot=Environment.GetEnvironmentVariable("KIRIN_EXTRACTOR_SCREENSHOT");
    if(screenshot != null) { Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); window.CaptureRenderedFrame()?.Save(screenshot); }
    start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var deadline = DateTime.UtcNow.AddSeconds(15);
    while(window.FindControl<Button>("ExtractorCancelButton")!.IsEnabled && DateTime.UtcNow<deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
    Dispatcher.UIThread.RunJobs();
    Check(window.FindControl<TextBlock>("ExtractorStatus")!.Text!.StartsWith("Extracted 2 partitions") &&
        Directory.GetFiles(Path.Combine(root,"ui")).Length==2 && File.ReadAllBytes(Path.Combine(root,"ui","BOOT.img")).SequenceEqual(boot),"UI extracts ticked partitions and reports completion");
    window.Close();
    Console.WriteLine($"{checks} extractor checks passed.");
}
finally { Directory.Delete(root,true); }
