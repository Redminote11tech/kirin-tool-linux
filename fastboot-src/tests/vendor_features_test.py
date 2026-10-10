# Real CLI and engine; only the USB backend is replaced. No hardware access.
import os, subprocess, tempfile, pathlib
checks=0
with tempfile.TemporaryDirectory() as folder:
    root=pathlib.Path(folder); image=root/'boot.img';image.write_bytes(b'ABCD')
    out=root/'dump.img';trace=root/'trace'
    def run(args,events,code=0,env=None):
        global checks
        trace.write_text(''.join(k+':'+(v.encode() if isinstance(v,str) else v).hex()+'\n' for k,v in events))
        p=subprocess.run(['tests/cli-fastboot',*args],env={**os.environ,'MOCK_TRACE':str(trace),'HUAWEI_ULTRAFLASH':'1',**(env or {})},capture_output=True,timeout=5)
        assert p.returncode==code,(args,p.returncode,p.stderr)
        assert p.stderr.count(b'TRACE_MATCH')==len([e for e in events if e[0]!='E']),(args,p.stderr)
        checks+=1;return p
    def standard(part='boot'):
        return [('W','download:00000004'),('R','DATA00000004'),('W',b'ABCD'),('R','OKAY'),('W','flash:'+part),('R','OKAY')]
    def ultra(part='boot'):
        return [('W','ultraflash:'+part),('R','OKAY')]+standard(part)+[('W','ultraflash:'),('R','OKAY')]
    args=['-S','0','flash','boot',str(image)]
    run(args,ultra())
    run(args,[('W','ultraflash:boot'),('R','FAILunknown command')]+standard())
    for mode in ['0','off']:run(args,standard(),env={'HUAWEI_ULTRAFLASH':mode})
    # No automatic write retry after lost/malformed handshake or failed data/end.
    run(args,[('W','ultraflash:boot'),('E',b'')],1)
    run(args,[('W','ultraflash:boot'),('R','oops')],1)
    run(args,[('W','ultraflash:boot'),('R','OKAY'),('W','download:00000004'),('R','FAILdownload refused')],1)
    run(args,ultra()[:-1]+[('R','FAILend refused')],1)
    run(args+['flash','cust',str(image)],ultra()+ultra('cust'))
    run(args+['flash','cust',str(image)],[('W','ultraflash:boot'),('R','FAILunknown')]+standard()+standard('cust'))
    def upload(kind,address=0x12340000,size=4):
        return [('W',f'upload_{kind}:{address:016x}:{size:016x}'),('R','OKAY'),('R',b'AB'),('R',b'CD'),('R','OKAY')]
    run(['oem','memory','oeminfo',str(out)],[('W','getvar:memory:oeminfo'),('R','OKAY0000000012340000:0000000000000004')]+upload('memory'))
    assert out.read_bytes()==b'ABCD'
    run(['oem','memupload','12340000','4',str(out)],upload('memory'))
    assert out.read_bytes()==b'ABCD'
    run(['oem','dump-ufseye','1','4','1',str(out)],upload('ufseye',0x101))
    assert out.read_bytes()==b'ABCD'
    out.write_bytes(b'KEEP')
    run(['oem','memupload','12340000','4',str(out)],upload('memory')[:-1]+[('R','FAILtail')],1)
    assert out.read_bytes()==b'KEEP' and not list(root.glob('*.partial.*'))
    for vals in [('0','4','0'),('1','0','0'),('1','1000001','0'),('1','4','2')]:
        run(['oem','dump-ufseye',*vals,str(out)],[],1)
    for addr,size in [('-1','4'),('garbage','4'),('ffffffffffffffff','2'),('0','0')]:
        run(['oem','memupload',addr,size,str(out)],[],1)
    run(['oem','memory','a'*16,str(out)],[],1)
    print(f'PASS {checks} native vendor feature CLI/engine checks (fake USB only)')
