using System.Reflection;
using Kirin_Tool.Services.USBUpdate;
using Kirin_Tool.Services.USBUpdate.Transport;
using Kirin_Tool.Utils;

int checks=0;
void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
string dir=Path.Combine(Path.GetTempPath(),"kirin-usb-diagnostics-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try {
    File.WriteAllText(Path.Combine(dir,"list.txt"),"XLOADER 1\n");
    File.WriteAllBytes(Path.Combine(dir,"XLOADER.img"),new byte[64]);
    File.WriteAllBytes(Path.Combine(dir,"XLOADER.img.header"),new byte[100]);
    var flash=typeof(UsbDownloadService).GetMethod("FlashPartitions",BindingFlags.Instance|BindingFlags.NonPublic)!;
    foreach(var scenario in new (int fail,string mode,string stage)[]{(1,"nack","header"),(2,"nack","data transfer"),(3,"nack","finalization"),(2,"timeout","data transfer"),(2,"partial","data transfer"),(2,"io","data transfer"),(0,"success","")}) {
        var lines=new List<string>();string? callback=null;
        var service=new UsbDownloadService(lines.Add,dir);
        service.OnPartitionCompleted=(_,_,ok,details)=>{if(!ok)callback=details;};
        using var port=new Fake(scenario.fail,scenario.mode);
        bool success=false;Exception? error=null;
        try {success=(bool)flash.Invoke(service,new object[]{port})!;}
        catch(TargetInvocationException ex){error=ex.InnerException;}
        if(scenario.fail==0) {
            Check(success && error==null && service.LastError==null,"ACK sequence still succeeds");
            Check(port.Writes==3,"successful header/data/tail sequence unchanged");
            Check(lines.Any(s=>s.Contains("completed successfully")),"completion logged");
            continue;
        }
        Check(!success && port.Writes==scenario.fail,"failure stops without retry or next-stage write: "+scenario);
        string details=service.LastError??"";
        Check(details.Contains("XLOADER") && details.Contains(scenario.stage),"partition and stage retained: "+scenario);
        string reason=scenario.mode switch {"timeout"=>"No response within 1000ms","partial"=>"7E 03","io"=>"USB receive failed (-4)",_=>"7E 03 11 22 7E"};
        Check(details.Contains(reason),"original response/transport reason retained: "+scenario);
        Check(lines.Any(s=>s.Contains(details)),"full reason reaches diagnostic callback: "+scenario);
        if(scenario.fail==1)Check(error?.Message.Contains(reason)==true,"header exception preserves device response");
        else Check(callback==details,"progress failure receives complete reason: "+scenario);
        if(scenario.fail==2)Check(details.Contains("offset 0x0"),"data failure includes block offset: "+scenario);
        Check(port.ReadTimeout==5000,"read timeout restored: "+scenario);
    }
    File.WriteAllBytes(Path.Combine(dir,"XLOADER.img"),new byte[0x20000+64]);
    var secondBlock=new UsbDownloadService(_=>{},dir);
    using(var port=new Fake(3,"nack")) {
        Check(!(bool)flash.Invoke(secondBlock,new object[]{port})!,"later data block failure propagates");
        Check(secondBlock.LastError!.Contains("offset 0x20000") && secondBlock.LastError.Contains("data transfer"),"later block reports its actual offset and stage");
        Check(port.Writes==3,"later block failure does not send the tail or replay data");
    }
    File.Delete(Path.Combine(dir,"list.txt"));
    var missingList=new UsbDownloadService(_=>{},dir);
    using(var port=new Fake(0,"success")) {
        Check(!(bool)flash.Invoke(missingList,new object[]{port})! && missingList.LastError!.Contains("list.txt"),"missing transfer list has a concrete failure reason");
        Check(port.Writes==0,"missing list sends no partition commands");
    }
    if(args.Length>0) {
        var baselineContext=new System.Runtime.Loader.AssemblyLoadContext("previous-release",isCollectible:true);
        var baselineAssembly=baselineContext.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
        var baselineType=baselineAssembly.GetType(typeof(UsbDownloadService).FullName!)!;
        object previous=Activator.CreateInstance(baselineType,new object[]{(Action<string>)(_=>{}),dir})!;
        var current=new UsbDownloadService(_=>{},dir);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(var (method,parameters) in new (string,object[])[]{
            ("CreateHandshakeCommand",Array.Empty<object>()),
            ("CreateHeadCommand",new object[]{new byte[101]}),
            ("CreateTailCommand",new object[]{new byte[101]}),
            ("CreateDataCommand",new object[]{new byte[]{1,2,0x7e,0x7d},64,new byte[]{0,0,0,1},(uint)0x20000})}) {
            byte[] before=(byte[])baselineType.GetMethod(method,flags)!.Invoke(previous,parameters)!;
            byte[] after=(byte[])typeof(UsbDownloadService).GetMethod(method,flags)!.Invoke(current,parameters)!;
            Check(before.SequenceEqual(after),"wire bytes unchanged from packaged 2.4.7: "+method);
        }
        baselineContext.Unload();
    }
    string marker="USB-DIAGNOSTIC-TEST-"+Guid.NewGuid().ToString("N");
    ProcessRunner.WriteDiagnostic("Software Testpoint",marker+" Partition XLOADER, finalization: Device error (0x03). Response: 7E 03 11 22 7E");
    string logDir=Path.Combine(AppContext.BaseDirectory,"log");
    string log=File.ReadAllText(Path.Combine(logDir,$"kirintool_log_{DateTime.Now:yyyy_MM_dd}.log"));
    Check(log.Contains(marker)&&log.Contains("7E 03 11 22 7E"),"non-fastboot diagnostics persist to application log");
    Console.WriteLine($"{checks} USB diagnostic checks passed; simulated transport only.");
}finally{Directory.Delete(dir,true);}

sealed class Fake(int failure,string mode):IUpdateTransport {
    private Queue<byte> reply=new();
    public int Writes {get;private set;}
    public int ReadTimeout {get;set;}=5000;
    public int BytesToRead=>reply.Count;
    public void Write(byte[] data,int offset,int count){
        Writes++;
        if(Writes==failure && mode=="io")throw new IOException("USB Update transport failed",new IOException("USB receive failed (-4)"));
        byte[] bytes=Writes!=failure ? new byte[]{0x7e,0x02,0x6a,0xd3,0x7e} : mode switch {
            "timeout"=>Array.Empty<byte>(),"partial"=>new byte[]{0x7e,0x03},_=>new byte[]{0x7e,0x03,0x11,0x22,0x7e}};
        reply=new Queue<byte>(bytes);
    }
    public int ReadByte()=>reply.Count>0?reply.Dequeue():throw new TimeoutException();
    public int Read(byte[] data,int offset,int count){int n=Math.Min(count,reply.Count);for(int i=0;i<n;i++)data[offset+i]=reply.Dequeue();return n;}
    public void DiscardInBuffer()=>reply.Clear();
    public void Close(){}
    public void Dispose(){}
}
