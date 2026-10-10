using System.Runtime.InteropServices;
using System.Text;
namespace Kirin_Tool.Services.USBUpdate.Transport;
public interface IBulkDevice : IDisposable
{
    int Receive(byte[] data, out int count, uint timeout);
    int Send(byte[] data, out int count, uint timeout);
}
internal sealed class NativeBulkDevice : IBulkDevice
{
    private IntPtr handle;
    private NativeBulkDevice(IntPtr handle) { this.handle=handle; }
    public static NativeBulkDevice Open()
    {
        var error=new StringBuilder(512);
        int status=OpenNative(out var handle,error,error.Capacity);
        if(status!=0)throw new IOException(error.ToString());
        return new NativeBulkDevice(handle);
    }
    public int Receive(byte[] data,out int count,uint timeout)=>ReadNative(handle,data,data.Length,out count,timeout);
    public int Send(byte[] data,out int count,uint timeout)=>WriteNative(handle,data,data.Length,out count,timeout);
    public void Dispose() { if(handle!=IntPtr.Zero){CloseNative(handle);handle=IntPtr.Zero;} }
    [DllImport("kirin_usb_update",EntryPoint="ku_open")] private static extern int OpenNative(out IntPtr h,StringBuilder error,int size);
    [DllImport("kirin_usb_update",EntryPoint="ku_read")] private static extern int ReadNative(IntPtr h,[Out] byte[] b,int size,out int count,uint timeout);
    [DllImport("kirin_usb_update",EntryPoint="ku_write")] private static extern int WriteNative(IntPtr h,byte[] b,int size,out int count,uint timeout);
    [DllImport("kirin_usb_update",EntryPoint="ku_close")] private static extern void CloseNative(IntPtr h);
}
