using System.IO.Ports;
using Kirin_Tool.Utils;

// Deliberately does not reference or invoke UsbDownloadService. No Write API.
string? port = LinuxSerialPortFinder.FindPort(0x12d1,
    interfaceMatches: LinuxSerialPortFinder.IsHuaweiUsbUpdateInterface);
if (port == null)
{
    Console.Error.WriteLine("No Huawei DBAdapter tty found. A USB identity alone is not a serial port.");
    return 2;
}
Console.WriteLine($"DBAdapter tty: {port}");
if (args.Length == 0) return 0;
if (args.Length != 1 || args[0] != "--open-close")
{
    Console.Error.WriteLine("Usage: UsbUpdateProbe [--open-close]");
    return 2;
}
// Only open our audited bulk-only adapter. Other drivers may issue control
// requests on open, so this hardware probe will not open them.
string ttyName = Path.GetFileName(port);
var serial = new DirectoryInfo($"/sys/class/tty/{ttyName}/device").ResolveLinkTarget(true) as DirectoryInfo;
bool owned = false;
for (var d = serial; d != null; d = d.Parent)
{
    var driver = new DirectoryInfo(Path.Combine(d.FullName, "driver"));
    if (driver.Exists && driver.ResolveLinkTarget(true)?.Name == "kirin_usb_update") owned = true;
    if (File.Exists(Path.Combine(d.FullName, "idVendor"))) break;
}
if (!owned) throw new InvalidOperationException("Open-close check requires kirin_usb_update driver.");
using var connection = new SerialPort(port, 9600)
{
    ReadTimeout = 5000, WriteTimeout = 5000,
    WriteBufferSize = 8 * 1024 * 1024, ReadBufferSize = 1024 * 1024
};
connection.Open();
Console.WriteLine($"Opened with application settings; buffered input: {connection.BytesToRead} bytes. No writes performed.");
connection.Close();
Console.WriteLine("Closed successfully. This does not validate flashing or device responses.");
return 0;
