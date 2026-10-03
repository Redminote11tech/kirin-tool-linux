using Kirin_Tool.Services;
using Kirin_Tool.Utils;
var port=LinuxSerialPortFinder.FindPort(0x12d1,0x3609);
Console.WriteLine("COM 1.0 port: "+(port??"not found"));
if(port==null)return 2;
Console.WriteLine("Connect/Dispose only; no handshake or loader calls.");
try { using(var flasher=new VcomFlasher())flasher.Connect(); Console.WriteLine("OPEN/CLOSE succeeded"); return 0; }
catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
