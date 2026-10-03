using System.Collections.Concurrent;
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
sealed class Fake : IBulkDevice {
 public BlockingCollection<(int status,byte[] bytes)> Input=new();
 public List<byte> Written=new();public int Limit=int.MaxValue,SendError,Sends;public bool Disposed;
 public int Receive(byte[] b,out int n,uint timeout){if(Input.TryTake(out var x,(int)timeout)){x.bytes.CopyTo(b,0);n=x.bytes.Length;return x.status;}n=0;return -7;}
 public int Send(byte[] b,out int n,uint timeout){Sends++;n=Math.Min(b.Length,Limit);Written.AddRange(b.Take(n));return SendError;}
 public void Dispose(){Disposed=true;}
}
