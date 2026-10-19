"""Static Windows PE import audit; does not substitute for clean-PC execution.
Checks dependencies of actual x64 native binaries, without treating installed VC runtimes
on the developer PC as proof that they will exist on the target PC.
"""
from pathlib import Path
import json,os,pefile
ROOT=Path(__file__).resolve().parents[1]
systems=Path(os.environ['SystemRoot'])/'System32'; result=[]
groups=[('app',ROOT/'artifacts/app-0.8-win-x64'),('heif',ROOT/'artifacts/app-0.8-win-x64/heif-engine'),('media',ROOT/'artifacts/app-0.8-win-x64/media-engine'),('speech',ROOT/'artifacts/optional-components/whisper/Release'),('qpdf',ROOT/'artifacts/optional-components/qpdf/qpdf-12.4.2-mingw64/bin'),('office',ROOT/'artifacts/optional-components/office/program'),('archives',ROOT/'artifacts/optional-components/sevenzip/x64')]
for group,base in groups:
    files=[p for p in base.iterdir() if p.is_file() and p.suffix.lower() in ('.dll','.exe','.com')]
    if group=='app': files += list((base/'x64').glob('*.dll'))
    for path in files:
        pe=pefile.PE(str(path),fast_load=True)
        if pe.FILE_HEADER.Machine!=0x8664: continue
        if pe.OPTIONAL_HEADER.DATA_DIRECTORY[14].VirtualAddress: continue
        pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_IMPORT'],pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT']])
        search={p.name.lower() for directory in {base,path.parent} for p in directory.iterdir() if p.is_file()}
        imports=[entry.dll.decode('ascii').lower() for entry in getattr(pe,'DIRECTORY_ENTRY_IMPORT',[])+getattr(pe,'DIRECTORY_ENTRY_DELAY_IMPORT',[])]
        missing=[]
        for name in imports:
            if name in search or name.startswith(('api-ms-','ext-ms-')): continue
            if name.startswith(('vcruntime','msvcp','concrt')) or not (systems/name).is_file(): missing.append(name)
        optional=[]
        if group=='office' and path.name=='officebean.dll' and 'jawt.dll' in missing:
            missing.remove('jawt.dll'); optional=['jawt.dll: Java AWT embedding bridge, not used by Naqla Writer exports']
        result.append({'Group':group,'File':path.name,'Imports':imports,'Missing':missing,'OptionalMissing':optional})
        pe.close()
(ROOT/'artifacts/native-import-audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
missing=[row for row in result if row['Missing']]
print('Native binaries checked:',len(result),'missing dependency entries:',sum(len(x['Missing']) for x in missing))
if missing: print(json.dumps(missing,indent=2)); raise SystemExit(1)
