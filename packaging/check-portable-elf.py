#!/usr/bin/env python3
"""Fail a portable build on newer glibc, mandatory higher ISA, or system libudev."""
from pathlib import Path
import re,subprocess,sys
root=Path(sys.argv[1]);count=0
for path in root.rglob('*'):
    if not path.is_file():continue
    with path.open('rb') as stream:
        if stream.read(4)!=b'\x7fELF':continue
    count+=1
    versions=subprocess.check_output(['readelf','--version-info',str(path)],text=True)
    required=[tuple(map(int,m.split('.'))) for m in re.findall(r'Name: GLIBC_(\d+\.\d+(?:\.\d+)?)',versions)]
    assert all(v<=(2,31) for v in required),f'{path}: glibc > 2.31'
    notes=subprocess.check_output(['readelf','--notes',str(path)],text=True)
    assert not re.search(r'x86 ISA (?:needed|used):[^\n]*x86-64-v[234]',notes),f'{path}: higher x86 ISA note'
    dynamic=subprocess.check_output(['readelf','--dynamic',str(path)],text=True)
    if path.name=='libusb-1.0.so.0':
        assert 'libudev' not in dynamic,f'{path}: host libudev dependency'
assert count>0,'No ELF files checked'
print(f'PASS {count} ELF files: glibc <= 2.31, no higher x86 ISA notes; libusb needs no libudev')
