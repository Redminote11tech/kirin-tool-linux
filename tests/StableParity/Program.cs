using Kirin_Tool.Services;
using Kirin_Tool.Utils;
using System.Text.Json;
using System.Security.Cryptography;

string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
var boundary = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tests/StableParity/boundary.json")));
foreach (var file in boundary.RootElement.EnumerateObject())
    Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,file.Name)))).Equals(file.Value.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase), "frozen stable boundary: " + file.Name);
foreach (string absent in new[]{"Utils/FlashInputSet.cs","Utils/FastbootErrorHints.cs","Services/UsbPortModeReader.cs","tests/SafetyTests/Program.cs"})
    Check(!File.Exists(Path.Combine(root,absent)), "beta code absent: " + absent);
string dir=Path.Combine(Path.GetTempPath(),"kirin-stable-parity-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try {
    byte[] Sparse(uint checksum, params (ushort Type,uint Blocks,byte[] Data)[] chunks) {
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
        w.Write(0xed26ff3au);w.Write((ushort)1);w.Write((ushort)0);w.Write((ushort)28);w.Write((ushort)12);
        w.Write(4096u);w.Write(chunks.Sum(c=>(int)c.Blocks));w.Write(chunks.Length);w.Write(checksum);
        foreach(var c in chunks){w.Write(c.Type);w.Write((ushort)0);w.Write(c.Blocks);w.Write((uint)c.Data.Length+12);w.Write(c.Data);}
        return stream.ToArray();
    }
    // Golden Windows positional result. This asserts fidelity, NOT valid merged geometry/CRC.
    foreach(bool crc in new[]{false,true}) {
        var a=Sparse(crc?0x12345678u:0u,(0xcac1,2,Enumerable.Repeat((byte)'A',8192).ToArray()),(0xcac3,4,Array.Empty<byte>()));
        var b=crc?Sparse(0x87654321u,(0xcac3,4,Array.Empty<byte>()),(0xcac1,1,Enumerable.Repeat((byte)'B',4096).ToArray()),(0xcac4,0,BitConverter.GetBytes(0x11223344u))):Sparse(0,(0xcac3,4,Array.Empty<byte>()),(0xcac1,1,Enumerable.Repeat((byte)'B',4096).ToArray()),(0xcac3,1,Array.Empty<byte>()));
        File.WriteAllBytes(dir+"/a",a);File.WriteAllBytes(dir+"/b",b);
        var expected=a[..^12].Concat(b[40..]).ToArray();BitConverter.GetBytes(3u).CopyTo(expected,20);
        foreach(bool reverse in new[]{false,true}){
            await SuperMerger.MergeSuperImages(dir+(reverse?"/b":"/a"),dir+(reverse?"/a":"/b"),dir+"/merged");
            Check(File.ReadAllBytes(dir+"/merged").SequenceEqual(expected),$"Windows merge bytes CRC={crc} reverse={reverse}");
        }
    }
    string fake=dir+"/fake-fastboot";
    File.WriteAllText(fake,"#!/usr/bin/python3\nimport sys,json\nprint(json.dumps(sys.argv[1:]))\nsys.exit(7)\n");
    File.SetUnixFileMode(fake,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    var fb=new FastbootClient(fake);string unusual=dir+"/image with spaces and \\\"quote.img";
    var result=await fb.FlashPartition("system",unusual);
    Check(result.ExitCode==7,"flash process exit preserved");
    var argv=JsonSerializer.Deserialize<string[]>(result.Output.Trim())!;
    Check(argv.SequenceEqual(new[]{"flash","system",unusual}),"flash arguments preserved without beta input validation");
    Directory.CreateDirectory(dir+"/fastbootimage");File.WriteAllBytes(dir+"/fastbootimage/BOOT.img",new byte[]{1});
    File.WriteAllText(dir+"/flash.xml","<configurations><configuration><fastbootimage><image name=\"BOOT DISPLAY\" identifier=\"boot\">fastbootimage\\BOOT.img</image></fastbootimage></configuration></configurations>");
    var svc=new FastbootFlasherService(fb);
    var partitions=svc.ParseFlashingXml(dir+"/flash.xml");
    Check(partitions.Count==1&&partitions[0].Identifier=="boot"&&partitions[0].DumpPath==dir+"/fastbootimage/BOOT.img","Windows XML separator and identifier preserved");
    Console.WriteLine($"{checks} stable parity checks passed; no hardware accessed.");
} finally {Directory.Delete(dir,true);}
