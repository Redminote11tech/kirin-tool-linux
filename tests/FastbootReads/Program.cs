using Kirin_Tool.Services;
using Kirin_Tool.Models;
ProcessResult Reply(string text, int status=0) => new(){Output=text,ExitCode=status};
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
void Reject(Action action,string name){try{action();}catch(InvalidOperationException){Console.WriteLine("PASS "+name);return;}throw new Exception(name);}
Check(FastbootReadResponses.Variable(Reply("nve:SN: TEST123\nfinished. total time: 0.003s"),"nve:SN")=="TEST123","bare serial");
Check(FastbootReadResponses.Variable(Reply(" (bootloader) nve:SN: TEST123\r\n"),"nve:SN")=="TEST123","INFO serial");
Reject(()=>FastbootReadResponses.Variable(Reply("nve:SN: A\nnve:SN: B"),"nve:SN"),"conflicting serial");
Reject(()=>FastbootReadResponses.Variable(Reply("serialno: TEST123"),"nve:SN"),"no USB serial substitution");
var fail=Reply("getvar:ptable FAILED (remote: invalid command)\nfinished. total time: 0.006s",1);
Reject(()=>FastbootReadResponses.Partitions(fail),"remote invalid command is not empty table");
Reject(()=>FastbootReadResponses.Variable(fail,"nve:SN"),"failed serial query");
Check(FastbootReadResponses.Partitions(Reply("(bootloader) :xloader\r\n(bootloader) :system_a\nptable: \nfinished. total time: 0.01s")).SequenceEqual(new[]{"xloader","system_a"}),"INFO partition entries");
Reject(()=>FastbootReadResponses.Partitions(Reply("ptable: \nfinished. total time: 0.01s")),"unknown table format");
Reject(()=>FastbootReadResponses.Partitions(Reply("(bootloader) :../../file")),"malformed entry");
Reject(()=>FastbootReadResponses.Partitions(Reply("(bootloader) :system\nFAILED (remote: read failed)",1)),"partial failed table");
