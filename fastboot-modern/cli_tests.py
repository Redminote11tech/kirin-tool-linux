#!/usr/bin/env python3
# Runs only kirin_cli_tests, whose transport cannot open USB devices.
import os, pathlib, subprocess, sys, tempfile
binary = str(pathlib.Path(sys.argv[1]).resolve())
checks = 0
with tempfile.TemporaryDirectory() as folder:
    root=pathlib.Path(folder);trace=root/'trace';out=root/'dump.img';image=root/'boot.img';image.write_bytes(b'ABCD')
    def run(args,events,code=0,env=None):
        global checks
        lines=[]
        for kind,value in events:
            if kind.endswith('@'):lines.append(f'{kind}:{value[0]:02x}:{value[1]}\n')
            else:lines.append(kind+':'+(value.encode() if isinstance(value,str) else value).hex()+'\n')
        trace.write_text(''.join(lines))
        result=subprocess.run([binary,*map(str,args)],env={**os.environ,'MOCK_TRACE':str(trace),'HUAWEI_ULTRAFLASH':'1',**(env or {})},capture_output=True,timeout=10)
        assert result.returncode==code,(args,result.returncode,result.stderr)
        assert result.stderr.count(b'TRACE_MATCH')==sum(k!='E' for k,_ in events),(args,result.stderr)
        checks+=1;return result
    def upload(kind,address=0x12340000,size=4):
        return [('W',f'upload_{kind}:{address:016x}:{size:016x}'),('R','OKAY'),('R',b'AB'),('R',b'CD'),('R','OKAY')]
    for op,kind in [('memory','memory'),('dump-emmc','emmc'),('dump-storage','storage')]:
        run(['oem',op,'oeminfo',out],[('W',f'getvar:{kind}:oeminfo'),('R','OKAY0000000012340000:0000000000000004')]+upload(kind))
        assert out.read_bytes()==b'ABCD'
        result=run(['oem',op,'oeminfo',out],[('W',f'getvar:{kind}:oeminfo'),('R','FAILCommand not allowed')],1)
        assert b'Command not allowed' in result.stderr
    run(['oem','memupload','12340000','4',out],upload('memory'))
    run(['oem','dump-ufseye','1','4','1',out],upload('ufseye',0x101))
    result=run(['oem','memupload','12340000','4','stdout'],upload('memory'))
    assert result.stdout==b'ABCD'
    out.write_bytes(b'KEEP')
    run(['oem','memupload','12340000','4',out],upload('memory')[:-1]+[('R','FAILtail')],1)
    assert out.read_bytes()==b'KEEP' and not list(root.glob('*.partial.*'))
    for op in [['memory','x'*16,out],['memupload','-1','4',out],['memupload','ffffffffffffffff','2',out],['memupload','bad!','4',out],['memupload','0',' -1',out],['memupload','0x','4',out],['dump-ufseye','1','4','2',out],['dump-ufseye','0','4','0',out]]:
        run(['oem',*op],[],1)
    for args,wire in [(['getvar','nve:SN'],'getvar:nve:SN'),(['oem','get-psid'],'oem get-psid'),(['create-logical-partition','system_a','4096'],'create-logical-partition:system_a:4096'),(['snapshot-update','cancel'],'snapshot-update:cancel'),(['snapshot-update','merge'],'snapshot-update:merge'),(['gsi','disable'],'gsi:disable'),(['gsi','wipe'],'gsi:wipe'),(['reboot','recovery'],'reboot-recovery'),(['reboot-recovery'],'reboot-recovery')]:
        run(args,[('W',wire),('R','OKAY')])
        result=run(args,[('W',wire),('R','FAILCommand not allowed')],1)
        assert b'Command not allowed' in result.stderr
    run(['reboot','fastboot'],[('W','getvar:is-userspace'),('R','OKAYno'),('W','reboot-fastboot'),('R','OKAY'),('W','getvar:is-userspace'),('R','OKAYyes')])
    run(['reboot-fastboot'],[('W','getvar:is-userspace'),('R','OKAYyes')])
    run(['reboot','fastboot'],[('W','getvar:is-userspace'),('R','OKAYno'),('W','reboot-fastboot'),('R','FAILCommand not allowed')],1)
    run(['reboot','fastboot'],[('W','getvar:is-userspace'),('R','OKAYno'),('W','reboot-fastboot'),('R','OKAY'),('W','getvar:is-userspace'),('R','OKAYno')],1)
    run(['set_active','a'],[('W','getvar:slot-count'),('R','OKAY2'),('W','set_active:a'),('R','OKAY')])
    stage=[('W','download:00000004'),('R','DATA00000004'),('W',b'ABCD'),('R','OKAY')]
    receive=[('W','upload'),('R','DATA00000004'),('R',b'ABCD'),('R','OKAY')]
    run(['-S','1M','stage',image,'get_staged',out],stage+receive)
    assert out.read_bytes()==b'ABCD'
    for op in ['get_unlock_bootloader_nonce','lock_bootloader']:
        run(['flashing',op],[('W','flashing '+op),('R','OKAY')])
        run(['flashing',op],[('W','flashing '+op),('R','FAILCommand not allowed')],1)
    run(['-S','1M','flashing','unlock_bootloader',image],stage+[('W','flashing unlock_bootloader'),('R','OKAY')])
    run(['delete-logical-partition','system_a'],[('W','delete-logical-partition:system_a'),('R','OKAY')])
    run(['resize-logical-partition','system','4096'],[('W','getvar:has-slot:system'),('R','OKAYno'),('W','getvar:is-logical:system'),('R','OKAYyes'),('W','resize-logical-partition:system:4096'),('R','OKAY')])
    query=[('W','getvar:has-slot:boot'),('R','OKAYno'),('W','getvar:max-fetch-size'),('R','OKAY4'),('W','getvar:partition-size:boot'),('R','OKAY8')]
    chunks=[('W','fetch:boot:0x00000000:0x00000004'),('R','DATA00000004'),('R',b'ABCD'),('R','OKAY'),('W','fetch:boot:0x00000004:0x00000004'),('R','DATA00000004'),('R',b'EFGH'),('R','OKAY')]
    run(['fetch','boot',out],query+chunks)
    assert out.read_bytes()==b'ABCDEFGH'
    for response,body in [('FAILCommand not allowed',[]),('DATA00000002',[('R',b'AB'),('R','OKAY')]),('DATA00000005',[('R',b'ABCDE')])]:
        out.write_bytes(b'KEEP')
        run(['fetch','boot',out],query+[('W','fetch:boot:0x00000000:0x00000004'),('R',response)]+body,1)
        assert out.read_bytes()==b'KEEP' and not list(root.glob('*.partial.*'))
    for events in [receive[:-1]+[('R','FAILtail refused')],[('W','upload'),('R','FAILCommand not allowed')]]:
        out.write_bytes(b'KEEP')
        run(['get_staged',out],events,1)
        assert out.read_bytes()==b'KEEP' and not list(root.glob('*.partial.*'))
    run(['--slot','b','-S','1M','flash','boot',image],[('W','getvar:slot-count'),('R','OKAY2'),('W','getvar:has-slot:boot'),('R','OKAYyes'),('W','getvar:is-logical:boot_b'),('R','OKAYno'),('W','ultraflash:boot_b'),('R','OKAY')]+stage+[('W','flash:boot_b'),('R','OKAY'),('W','ultraflash:'),('R','OKAY')])
    # Native modern flash query sequence, followed by recovered Huawei wire behavior.
    preflight=[('W','getvar:has-slot:boot'),('R','OKAYno'),('W','getvar:is-logical:boot'),('R','OKAYno')]
    standard=stage+[('W','flash:boot'),('R','OKAY')]
    ultra=[('W','ultraflash:boot'),('R','OKAY')]+standard+[('W','ultraflash:'),('R','OKAY')]
    run(['-S','1M','flash','boot',image],preflight+ultra)
    result=run(['-S','1M','flash','boot',image],preflight+ultra[:-1]+[('R','FAILend refused')],1)
    assert b'end refused' in result.stderr
    result=run(['-S','1M','flash','boot',image],preflight+[('W','ultraflash:boot'),('R','FAILCommand not allowed')]+standard)
    assert b'UltraFlash refused' in result.stderr and b'Command not allowed' in result.stderr
    run(['-S','1M','flash','boot',image],preflight+standard,env={'HUAWEI_ULTRAFLASH':'off'})
    run(['-S','1M','flash','boot',image],[('W','getvar:has-slot:boot'),('R','FAILunknown variable'),('W','getvar:is-logical:boot'),('R','FAILunknown variable'),('W','ultraflash:boot'),('R','FAILCommand not allowed')]+standard)
    run(['-S','1M','flash','boot',image],preflight+[('W','ultraflash:boot'),('E',b'')],1)
print(f'PASS {checks} modern full CLI checks, fake transport only')
