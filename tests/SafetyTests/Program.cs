using Kirin_Tool.Utils;
using Kirin_Tool.Services;
using Kirin_Tool.Services.USBUpdate;
using System.Reflection;
using System.Text;

string dir = Path.Combine(Path.GetTempPath(), "kirin-safety-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
async Task Reject(Func<Task> action, string message) { bool failed = false; try { await action(); } catch { failed = true; } Check(failed, message); }
void RejectSync(Action action, string message) => Reject(() => { action(); return Task.CompletedTask; }, message).GetAwaiter().GetResult();
void Sparse(string path, uint blockSize, params (ushort type, uint blocks, byte value)[] chunks)
{
    using var w = new BinaryWriter(File.Create(path));
    w.Write(0xed26ff3au); w.Write((ushort)1); w.Write((ushort)0); w.Write((ushort)28); w.Write((ushort)12);
    w.Write(blockSize); w.Write(chunks.Aggregate(0u, (n,c)=>n+c.blocks)); w.Write((uint)chunks.Length); w.Write(0u);
    foreach (var c in chunks) {
        w.Write(c.type); w.Write((ushort)0); w.Write(c.blocks);
        w.Write(12u + (c.type == 0xcac1 ? c.blocks * blockSize : c.type == 0xcac2 ? 4u : 0u));
        if (c.type == 0xcac1) w.Write(Enumerable.Repeat(c.value,checked((int)(c.blocks*blockSize))).ToArray());
        if (c.type == 0xcac2) w.Write(new byte[]{c.value,c.value,c.value,c.value});
    }
}
// Independent small-fixture decoder: validates totals and locates every byte.
byte[] Decode(string path)
{
    using var r = new BinaryReader(File.OpenRead(path));
    Check(r.ReadUInt32()==0xed26ff3a,"sparse magic"); r.BaseStream.Position=12;
    uint block=r.ReadUInt32(), total=r.ReadUInt32(), count=r.ReadUInt32();r.ReadUInt32();
    var output=new byte[checked((int)(block*total))]; int offset=0;
    for(uint i=0;i<count;i++) {
        ushort type=r.ReadUInt16(); r.ReadUInt16(); int bytes=checked((int)(r.ReadUInt32()*block)); uint size=r.ReadUInt32();
        if(type==0xcac1){Check(size==bytes+12,"RAW length");r.BaseStream.ReadExactly(output.AsSpan(offset,bytes));}
        else if(type==0xcac2){var fill=r.ReadBytes(4);for(int n=0;n<bytes;n++)output[offset+n]=fill[n%4];}
        else Check(type==0xcac3 && size==12,"DONT_CARE length");
        offset+=bytes;
    }
    Check(offset==output.Length && r.BaseStream.Position==r.BaseStream.Length,"sparse declared geometry"); return output;
}
void App(string path,string name,byte[] payload,uint? declared=null)
{
    var h=new byte[98]; BitConverter.GetBytes(0xa55aaa55u).CopyTo(h,0);BitConverter.GetBytes(98).CopyTo(h,4);
    Encoding.ASCII.GetBytes("HWTEST00").CopyTo(h,12);BitConverter.GetBytes(declared??(uint)payload.Length).CopyTo(h,24);
    Encoding.ASCII.GetBytes(name).CopyTo(h,60); using var w=new BinaryWriter(File.Create(path));w.Write(h);w.Write(payload);
    while(w.BaseStream.Position%4!=0)w.Write((byte)0);
}
object Invoke(object target,string method,params object?[] args) => target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(target,args)!;
try {
    string a=dir+"/a",b=dir+"/b",merged=dir+"/merged";
    Sparse(a,4096,(0xcac1,2,(byte)'A'),(0xcac3,4,0));
    Sparse(b,4096,(0xcac3,4,0),(0xcac1,1,(byte)'B'),(0xcac3,1,0));
    await SuperMerger.MergeSuperImages(a,b,merged);
    var bytes=Decode(merged);Check(bytes.Length==6*4096&&bytes[4*4096]=='B'&&bytes[2*4096]==0,"merge preserves partition offsets and holes");
    string reverse=dir+"/reverse";await SuperMerger.MergeSuperImages(b,a,reverse);Check(Decode(reverse).SequenceEqual(bytes),"merge independent of input/file-size order");
    await SuperMerger.MergeSuperImages(a,a,reverse);Check(Decode(reverse).SequenceEqual(Decode(a)),"identical overlaps accepted");
    Sparse(b,4096,(0xcac1,2,(byte)'Z'),(0xcac3,4,0));File.WriteAllText(merged,"KEEP");
    await Reject(()=>SuperMerger.MergeSuperImages(a,b,merged),"conflicting overlaps rejected");Check(File.ReadAllText(merged)=="KEEP","failed merge preserves prior output");
    Sparse(b,512,(0xcac1,2,1),(0xcac3,4,0));await Reject(()=>SuperMerger.MergeSuperImages(a,b,merged),"mismatched block size rejected");
    Sparse(b,4096,(0xcac3,7,0));await Reject(()=>SuperMerger.MergeSuperImages(a,b,merged),"mismatched partition size rejected");
    File.WriteAllBytes(b,new byte[]{1,2,3});await Reject(()=>SuperMerger.MergeSuperImages(a,b,merged),"truncated header rejected");
    Sparse(b,4096,(0xcac1,2,1),(0xcac3,4,0));using(var f=File.OpenWrite(b))f.SetLength(45);
    await Reject(()=>SuperMerger.MergeSuperImages(a,b,merged),"truncated RAW chunk rejected");
    Sparse(a,512,(0xcac2,2,0xA5),(0xcac3,2,0));Sparse(b,512,(0xcac3,2,0),(0xcac1,1,0x5A),(0xcac3,1,0));
    await SuperMerger.MergeSuperImages(a,b,merged);bytes=Decode(merged);Check(bytes.Length==2048&&bytes[0]==0xA5&&bytes[1024]==0x5A,"FILL and non-4096 blocks preserved");
    await Reject(()=>SuperMerger.MergeSuperImages(a,b,a),"input cannot be output");
    Check(!Directory.GetFiles(dir,"*.partial").Any(),"failed merges leave no partial output");

    string app=dir+"/bad.app";App(app,"XLOADER",new byte[]{1,2,3},4096);
    RejectSync(()=>new USBUpdateApp(_=>{},dir+"/truncated").ExtractAllPartitions(app),"truncated USB image rejected");
    App(app,"../XLOADER",new byte[]{1,2,3});RejectSync(()=>new USBUpdateApp(_=>{},dir+"/escape").ExtractAllPartitions(app),"partition path traversal rejected");
    App(app,"XLOADER",new byte[]{1,2,3});RejectSync(()=>new USBUpdateApp(_=>{},dir+"/missing-selection").ExtractAllPartitions(app,includePartitions:new(){"BOOT"}),"missing selected partition rejected");
    var validDir=dir+"/valid";new USBUpdateApp(_=>{},validDir).ExtractAllPartitions(app,true);
    File.WriteAllText(validDir+"/list.txt","XLOADER 1\n");
    var usb=new UsbDownloadService(_=>{},validDir);
    using(var plan=(IDisposable)Invoke(usb,"PrepareFlashPlan"))Check(true,"valid USB plan preflights without hardware");
    File.WriteAllText(validDir+"/list.txt","XLOADER 1\nBOOT 1\n");RejectSync(()=>Invoke(usb,"PrepareFlashPlan"),"missing later image aborts whole plan");
    File.WriteAllText(validDir+"/list.txt","XLOADER 1\n");File.WriteAllBytes(validDir+"/XLOADER.img",new byte[]{1});
    RejectSync(()=>Invoke(usb,"PrepareFlashPlan"),"header/image length mismatch rejected");
    string empty=dir+"/empty.img";File.WriteAllBytes(empty,Array.Empty<byte>());RejectSync(()=>new FlashInputSet(new[]{a,empty}),"empty later fastboot input rejects preflight");

    // USB uppercase regression. Cancel before device discovery/connection.
    Sparse(a,4096,(0xcac1,2,1),(0xcac3,4,0));Sparse(b,4096,(0xcac3,4,0),(0xcac1,1,2),(0xcac3,1,0));
    App(dir+"/a.app","SUPER",File.ReadAllBytes(a));App(dir+"/b.app","SUPER",File.ReadAllBytes(b));
    using var cancel=new CancellationTokenSource();bool discovered=false;
    var full=new USBUpdateFlasherService(_=>{},dir+"/uppercase",(_,_)=>throw new Exception("Hardware forbidden"));
    full.OnPartitionsDiscovered=parts=>{Check(parts.Count==1&&parts[0].PartitionName=="super","uppercase SUPER fragments actually merge");discovered=true;cancel.Cancel();};
    await Reject(()=>full.FlashPartitions(new[]{(dir+"/a.app","Base",new List<string>{"SUPER"}),(dir+"/b.app","Cust",new List<string>{"SUPER"})},cancel.Token),"USB test stops before hardware");
    Check(discovered,"USB merge preflight completed");
    // Mapping/header consistency for the newly merged partition.
    using(var plan=(IDisposable)Invoke(new UsbDownloadService(_=>{},dir+"/uppercase"),"PrepareFlashPlan"))Check(true,"merged USB header length and mapping valid");

    // Real ProcessRunner/FastbootClient against a fake executable; never opens USB.
    string fake=dir+"/fake-fastboot", log=dir+"/calls",mode=dir+"/mode";
    File.WriteAllText(fake,"#!/usr/bin/python3\n"+"import sys,pathlib\np=pathlib.Path(__file__).parent\na=sys.argv[1:]\nwith (p/'calls').open('a') as f:f.write(repr(a)+'\\n')\nm=(p/'mode').read_text()\nif a==['--version']:\n print('stock' if m=='stock' else 'vendor-storage-upload-v1');sys.exit(0)\nif a[0]=='flash':sys.exit(1)\nif m=='query' and a[1]=='dump-emmc':\n print('KIRIN_DUMP_QUERY_FAILED');sys.exit(1)\nif m=='transfer':\n print('FAILED transfer');sys.exit(1)\npathlib.Path(a[-1]).write_bytes(b'ABCD')\nprint('KIRIN_DUMP_OK bytes='+('8' if m=='short' else '4'))\n");
    File.SetUnixFileMode(fake,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    var fb=new FastbootClient(fake);string backup=dir+"/backup with spaces.img";
    File.WriteAllText(mode,"good");var result=await fb.DumpPartition("oeminfo",backup);Check(result.IsSuccess&&File.ReadAllText(backup)=="ABCD","validated backup published and spaced filename preserved");
    File.WriteAllText(backup,"KEEP");File.WriteAllText(mode,"short");result=await fb.DumpPartition("oeminfo",backup);Check(!result.IsSuccess&&File.ReadAllText(backup)=="KEEP","short backup rejected without overwriting previous file");
    File.WriteAllText(mode,"transfer");File.WriteAllText(log,"");result=await fb.DumpPartition("oeminfo",backup);Check(!result.IsSuccess&&!File.ReadAllText(log).Contains("dump-storage"),"no retry after upload failure");
    File.WriteAllText(mode,"query");result=await fb.DumpPartition("oeminfo",backup);Check(result.IsSuccess&&File.ReadAllText(log).Contains("dump-storage"),"metadata-only failure permits storage fallback");
    File.WriteAllText(mode,"stock");File.WriteAllText(log,"");await Reject(()=>fb.DumpPartition("oeminfo",backup),"old/system fastboot cannot certify backups");Check(!File.ReadAllText(log).Contains("dump-emmc"),"unsupported backup stopped before device command");
    await Reject(()=>fb.FlashPartition("xloader --erase",a),"invalid partition rejected before execution");
    File.WriteAllText(mode,"good");result=await fb.FlashPartition("xloader",a);Check(!result.IsSuccess,"flash exit status preserved");
    Console.WriteLine($"{checks} safety checks passed; no hardware accessed.");
}
finally { Directory.Delete(dir,true); }
