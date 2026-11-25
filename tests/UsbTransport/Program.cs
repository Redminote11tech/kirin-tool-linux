using System.Collections.Concurrent;
using System.Text;
using Kirin_Tool.Services.USBUpdate.Transport;
void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
void Wait(Func<bool> ready){if(!SpinWait.SpinUntil(ready,2000))throw new Exception("Test wait expired");}
using(var fake=new Fake())using(var t=new BufferedUsbTransport(fake)) {
 fake.Input.Add((0,new byte[]{0x7e,1}));fake.Input.Add((-7,new byte[]{2,0x7e}));
 Wait(()=>t.BytesToRead==4);
 Check(Enumerable.Range(0,4).Select(_=>t.ReadByte()).SequenceEqual(new[]{126,1,2,126}),"fragmented reads and bytes returned with timeout preserved");
 fake.Input.Add((0,new byte[]{9,8}));Wait(()=>t.BytesToRead==2);t.DiscardInBuffer();Check(t.BytesToRead==0,"discard buffered input");
 t.ReadTimeout=20;try{t.ReadByte();throw new Exception("Expected timeout");}catch(TimeoutException){Console.WriteLine("PASS read deadline");}
 fake.Limit=3;t.Write(new byte[]{0,1,2,3,4,5,6,7},1,6);Check(fake.Written.SequenceEqual(new byte[]{1,2,3,4,5,6}),"short successful writes preserve order without duplication");
}
using(var f=new Fake()){using(var t=new BufferedUsbTransport(f)){f.SendError=-7;f.Limit=1;try{t.Write(new byte[]{1,2,3},0,3);throw new Exception("Expected error");}catch(IOException){Check(f.Sends==1,"partial timed-out write is never replayed");}}Check(f.Disposed,"device closed after read pump stopped");}
using(var f=new Fake())using(var t=new BufferedUsbTransport(f)){
 f.Input.Add((-4,Array.Empty<byte>()));Wait(()=>{try{_ = t.BytesToRead;return false;}catch(IOException){return true;}});
 try{t.ReadByte();throw new Exception("Expected disconnect");}catch(IOException){Console.WriteLine("PASS disconnect surfaces as error");}
}
using(var f=new Fake()){
 var t=new BufferedUsbTransport(f);var read=Task.Run(()=>{try{t.ReadByte();return false;}catch(ObjectDisposedException){return true;}});
 t.Dispose();Check(read.Wait(2000)&&read.Result,"close releases pending read");
}
// ---------------------------------------------------------------------------------------------
// S1 (audit 2026-11-15 round3 §1.1): the UPDATE.APP partition name is 32 attacker-controlled
// bytes that used to be concatenated into an output path with no check at all. The extractor now
// runs every name through Utils/PartitionNames and re-checks the resolved path, so a package
// entry called "../../evil" must fail instead of writing outside the transfer directory.
// ---------------------------------------------------------------------------------------------
string s1root = Path.Combine(Path.GetTempPath(), "kirin-usbupdate-name-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(s1root);
void App(string path, params (string Name, byte[] Data)[] entries)
{
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write(new byte[12]);
    foreach (var entry in entries)
    {
        writer.Write(new byte[] { 0x55, 0xaa, 0x5a, 0xa5 });
        writer.Write(102); writer.Write(0); writer.Write(Encoding.ASCII.GetBytes("HWTEST00")); writer.Write(0);
        writer.Write((uint)entry.Data.Length); writer.Write(new byte[32]);
        byte[] name = new byte[32]; Encoding.ASCII.GetBytes(entry.Name).CopyTo(name, 0); writer.Write(name);
        writer.Write(new byte[10]); writer.Write(entry.Data);
        while (writer.BaseStream.Position % 4 != 0) writer.Write((byte)0);
    }
}
void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); } catch (T) { Console.WriteLine("PASS " + message); return; }
    catch (Exception ex) { throw new Exception("FAIL " + message + " (got " + ex.GetType().Name + ": " + ex.Message + ")"); }
    throw new Exception("FAIL " + message + " (nothing was thrown)");
}
try
{
    Check(Kirin_Tool.Utils.PartitionNames.IsSafe("XLOADER") && Kirin_Tool.Utils.PartitionNames.IsSafe("super-2_a.img"),
        "partition names: ordinary names stay legal");
    foreach (string bad in new[] { "..", ".", "../../evil", "a/b", "a\\b", "with space", new string('a', 65), "" })
        Check(!Kirin_Tool.Utils.PartitionNames.IsSafe(bad), "partition names: '" + bad + "' is refused");
    Throws<InvalidDataException>(() => Kirin_Tool.Utils.PartitionNames.ChildPathWithin(s1root, "../../evil", ".img"),
        "partition names: a traversal name cannot resolve to a child path");

    string good = Path.Combine(s1root, "good.app");
    App(good, ("XLOADER", Encoding.ASCII.GetBytes("xloader payload")), ("PTABLE", new byte[] { 1, 2, 3 }));
    string goodOut = Path.Combine(s1root, "good");
    var extractor = new Kirin_Tool.Services.USBUpdate.USBUpdateApp(_ => { }, goodOut);
    var extracted = extractor.ExtractAllPartitions(good, extractUnlockCode: true);
    Check(extracted.Any(line => line.StartsWith("XLOADER", StringComparison.Ordinal)) &&
        File.ReadAllBytes(Path.Combine(goodOut, "XLOADER.img")).SequenceEqual(Encoding.ASCII.GetBytes("xloader payload")),
        "extractor: a well-formed package still extracts byte-for-byte");
    Check(File.Exists(Path.Combine(goodOut, "XLOADER.img.header")), "extractor: the header sidecar is still written");
    if (!OperatingSystem.IsWindows())
    {
        Check(File.GetUnixFileMode(Path.Combine(goodOut, "XLOADER.img")) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
            "extractor: extracted images are created 0600");
        Check(File.GetUnixFileMode(Path.Combine(goodOut, "unlockcode")) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
            "extractor: the unlock code lands 0600 (it is a package secret)");
    }

    string hostile = Path.Combine(s1root, "hostile.app");
    App(hostile, ("../../evil", Encoding.ASCII.GetBytes("pwn")));
    string hostileOut = Path.Combine(s1root, "outer", "dload");
    Directory.CreateDirectory(hostileOut);
    var hostileExtractor = new Kirin_Tool.Services.USBUpdate.USBUpdateApp(_ => { }, hostileOut);
    Throws<InvalidDataException>(() => hostileExtractor.ExtractAllPartitions(hostile),
        "extractor: a '../../evil' partition name aborts instead of writing outside the directory");
    Check(!File.Exists(Path.Combine(s1root, "evil.img")) && !File.Exists(Path.Combine(s1root, "outer", "evil.img"))
        && !File.Exists(Path.Combine(s1root, "evil.img.header")),
        "extractor: no file was created outside the transfer directory");
    Check(Directory.GetFiles(hostileOut, "*.img", SearchOption.AllDirectories).Length == 0,
        "extractor: the refused entry left nothing behind inside either");
}
finally
{
    try { Directory.Delete(s1root, true); } catch { }
}

sealed class Fake : IBulkDevice {
 public BlockingCollection<(int status,byte[] bytes)> Input=new();
 public List<byte> Written=new();public int Limit=int.MaxValue,SendError,Sends;public bool Disposed;
 public int Receive(byte[] b,out int n,uint timeout){if(Input.TryTake(out var x,(int)timeout)){x.bytes.CopyTo(b,0);n=x.bytes.Length;return x.status;}n=0;return -7;}
 public int Send(byte[] b,out int n,uint timeout){Sends++;n=Math.Min(b.Length,Limit);Written.AddRange(b.Take(n));return SendError;}
 public void Dispose(){Disposed=true;}
}
