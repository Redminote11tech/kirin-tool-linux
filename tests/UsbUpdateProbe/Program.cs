using Kirin_Tool.Services.USBUpdate.Transport;
if(!args.SequenceEqual(new[]{"--open-close"})) {
    Console.WriteLine("Usage: kirin-usb-update-probe --open-close\nClaims/releases DBAdapter through libusb; no handshake or firmware writes.");return 0;
}
try {
    using var port=BufferedUsbTransport.Open();
    Console.WriteLine("Userspace DBAdapter opened; no kernel serial adapter required. No protocol commands sent.");
    Console.WriteLine("Buffered input: "+port.BytesToRead);
    port.Close();Console.WriteLine("Closed successfully. Firmware transfers remain unvalidated.");return 0;
} catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
