"""Prepare public packs from exact verified inputs, excluding standalone Microsoft CRT.
Office redistribution is not part of this beta. Never edits local/private component trees.
"""
from pathlib import Path
import hashlib,json,zipfile,urllib.request
ROOT=Path(__file__).resolve().parents[1]; A=ROOT/'artifacts'
catalog=json.loads((ROOT/'assets/component-catalog.json').read_text(encoding='utf-8'))
crt={x['File'].lower() for x in json.loads((ROOT/'assets/native-crt/sources.json').read_text(encoding='utf-8'))['Files']}
def excluded(name):return Path(name).name.lower() in crt or name.endswith('speech-CRT-LICENSE.txt')
public={name:value for name,value in catalog.items() if not excluded(name)}
output=A/'public-components'; output.mkdir(exist_ok=True)
(output/'component-catalog.json').write_text(json.dumps(public,indent=2),encoding='utf-8')
rows=[]
for feature in ('ocr','background','speech','archives','pdf-metadata'):
    source=A/'component-packs'/f'Naqla-component-{feature}.zip'
    target=output/source.name
    expected={name:value for name,value in public.items() if value['Feature']==feature}
    with zipfile.ZipFile(source) as old,zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as new:
        manifest=json.loads(old.read('component.json')); assert manifest['Feature']==feature
        new.writestr('component.json',json.dumps(manifest))
        for name,value in expected.items():
            data=old.read(name)
            assert len(data)==value['Bytes'] and hashlib.sha256(data).hexdigest().lower()==value['Sha256'].lower(),'Unverified component file: '+name
            new.writestr(name,data)
    with target.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    rows.append({'File':target.name,'Feature':feature,'Bytes':target.stat().st_size,'Sha256':digest,'MicrosoftCrtIncluded':False})
    print('Verified public pack: '+feature,flush=True)
source=output/'7z2604-src.7z'; source_hash='6bcfd2679d178174df06d09d50147dd7b5980d7beac896136689dfc9d51e0fd9'; source_url='https://github.com/ip7z/7zip/releases/download/26.04/7z2604-src.7z'
if not source.exists():
    with urllib.request.urlopen(source_url,timeout=120) as response,source.open('wb') as target:target.write(response.read(8_388_608))
with source.open('rb') as file:assert hashlib.file_digest(file,'sha256').hexdigest()==source_hash,'7-Zip corresponding source mismatch'
(output/'sources.json').write_text(json.dumps({'CatalogVersion':'2026-10-19','RequiredRuntime':'Official Microsoft Visual C++ x64 runtime on target PC','OfficePackDistributed':False,'SevenZipSource':{'File':source.name,'URL':source_url,'Sha256':source_hash},'Packs':rows},indent=2),encoding='utf-8')
