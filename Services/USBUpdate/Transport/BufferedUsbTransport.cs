using System.Diagnostics;
namespace Kirin_Tool.Services.USBUpdate.Transport;
// The protocol consumes a serial-style byte stream, not USB packet boundaries.
public sealed class BufferedUsbTransport : IUpdateTransport
{
    private readonly IBulkDevice device;
    private readonly Queue<byte> input=new();
    private readonly object gate=new(), writeGate=new();
    private readonly Thread reader;
    private volatile bool closed;
    private Exception? failure;
    private int timeout=5000;
    public int ReadTimeout { get=>timeout; set { if(value<=0)throw new ArgumentOutOfRangeException(nameof(value));timeout=value; } }
    public static BufferedUsbTransport Open()=>new(NativeBulkDevice.Open());
    public BufferedUsbTransport(IBulkDevice device)
    {
        this.device=device;
        reader=new Thread(Pump){IsBackground=true,Name="Kirin USB Update input"};
        try { reader.Start(); } catch { device.Dispose(); throw; }
    }
    private void Check() { if(closed)throw new ObjectDisposedException(nameof(BufferedUsbTransport));if(failure!=null)throw new IOException("USB Update transport failed",failure); }
    private void Pump()
    {
        var buffer=new byte[16384];
        try {
            while(!closed) {
                int status=device.Receive(buffer,out int count,100);
                lock(gate) {
                    if(closed)return;
                    if(count<0||count>buffer.Length)throw new IOException("Invalid USB receive count");
                    if(status!=0&&status!=-7)throw new IOException($"USB receive failed ({status})");
                    if(input.Count+count>8*1024*1024)throw new IOException("USB input buffer overflow");
                    // A timeout can still deliver bytes; never throw them away.
                    for(int i=0;i<count;i++)input.Enqueue(buffer[i]);
                    Monitor.PulseAll(gate);
                }
            }
        } catch(Exception ex) { lock(gate){failure=ex;Monitor.PulseAll(gate);} }
    }
    public int BytesToRead { get {lock(gate){Check();return input.Count;}} }
    public void DiscardInBuffer() {lock(gate){Check();input.Clear();}}
    public int ReadByte() {var b=new byte[1];Read(b,0,1);return b[0];}
    public int Read(byte[] buffer,int offset,int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if(offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException();
        lock(gate) {
            Check();if(count==0)return 0;
            var watch=Stopwatch.StartNew();int limit=ReadTimeout;
            while(input.Count==0) {
                Check();int remaining=limit-(int)watch.ElapsedMilliseconds;
                if(remaining<=0)throw new TimeoutException("USB Update read timed out");
                Monitor.Wait(gate,remaining);
            }
            Check();int n=Math.Min(count,input.Count);for(int i=0;i<n;i++)buffer[offset+i]=input.Dequeue();return n;
        }
    }
    public void Write(byte[] buffer,int offset,int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if(offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException();
        lock(writeGate) {
            var watch=Stopwatch.StartNew();
            while(count>0) {
                lock(gate)Check();
                int remaining=5000-(int)watch.ElapsedMilliseconds;
                if(remaining<=0)throw new TimeoutException("USB Update write timed out; no automatic replay");
                var chunk=buffer.AsSpan(offset,Math.Min(count,65536)).ToArray();
                int status=device.Send(chunk,out int sent,(uint)remaining);
                if(sent<0||sent>chunk.Length)throw new IOException("Invalid USB write count");
                // Any error aborts, even with partial progress: never replay a packet.
                if(status!=0)throw new IOException($"USB write failed ({status}), {sent} bytes transferred; no automatic replay");
                if(sent==0)throw new IOException("USB write made no progress");
                offset+=sent;count-=sent;
            }
        }
    }
    public void Close()=>Dispose();
    public void Dispose()
    {
        lock(writeGate) {
            lock(gate){if(closed)return;closed=true;Monitor.PulseAll(gate);}
            reader.Join();device.Dispose();
        }
    }
}
