using System.IO.Ports;
namespace Kirin_Tool.Services.USBUpdate.Transport;
public interface IUpdateTransport : IDisposable
{
    int ReadTimeout { get; set; }
    int BytesToRead { get; }
    int ReadByte();
    int Read(byte[] buffer, int offset, int count);
    void Write(byte[] buffer, int offset, int count);
    void DiscardInBuffer();
    void Close();
}
internal sealed class SerialUpdateTransport(SerialPort port) : IUpdateTransport
{
    public int ReadTimeout { get=>port.ReadTimeout; set=>port.ReadTimeout=value; }
    public int BytesToRead=>port.BytesToRead;
    public int ReadByte()=>port.ReadByte();
    public int Read(byte[] b,int o,int n)=>port.Read(b,o,n);
    public void Write(byte[] b,int o,int n)=>port.Write(b,o,n);
    public void DiscardInBuffer()=>port.DiscardInBuffer();
    public void Close()=>port.Close();
    public void Dispose()=>port.Dispose();
}
