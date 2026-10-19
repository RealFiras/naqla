"""Local development app-local CRT closure, verified against the pinned MSI-derived catalog.
Never installs a redistributable or writes system folders. Retains Microsoft terms.
External redistribution requires the applicable licensed Visual Studio rights/review.
"""
from pathlib import Path
import hashlib,json,shutil
ROOT=Path(__file__).resolve().parents[1]; SOURCE=ROOT/'artifacts/optional-components/office'
CATALOG=json.loads((ROOT/'assets/component-catalog.json').read_text(encoding='utf-8'))
OUT=ROOT/'assets/native-crt'; OUT.mkdir(exist_ok=True)
names=['concrt140.dll','msvcp140.dll','msvcp140_1.dll','msvcp140_2.dll','msvcp140_atomic_wait.dll','msvcp140_codecvt_ids.dll','vccorlib140.dll','vcruntime140.dll','vcruntime140_1.dll','vcruntime140_threads.dll']
rows=[]
for name in names:
    source=SOURCE/'program'/name; digest=hashlib.file_digest(source.open('rb'),'sha256').hexdigest()
    if digest.lower()!=CATALOG['office/program/'+name]['Sha256'].lower(): raise RuntimeError('Pinned app-local CRT file mismatch: '+name)
    shutil.copyfile(source,OUT/name); rows.append({'File':name,'Sha256':digest,'Bytes':source.stat().st_size})
    # whisper-cli is a separate executable, so its own application directory needs the CRT too.
    shutil.copyfile(source,ROOT/'artifacts/optional-components/whisper/Release'/name)
(OUT/'sources.json').write_text(json.dumps({'Source':'Unmodified app-local Microsoft AMD64 runtime files from pinned official LibreOffice MSI; no system installation','MsiSha256':'f9877032fd908beb9c0ddf06df4af5c2e85f419c42e14876c4cce5aae5fb2660','Files':rows},indent=2),encoding='utf-8')
text=(SOURCE/'license.txt').read_text(encoding='utf-8'); section=text.split('## Microsoft Visual C++ 2015 Runtime Libraries',1)[1].split('\n## ',1)[0]
(ROOT/'assets/licenses/Microsoft-VC-Runtime-LICENSE.txt').write_text('# Microsoft Visual C++ Runtime Libraries\n'+section,encoding='utf-8')
print('Prepared',len(rows),'app-local CRT DLLs;',sum(x['Bytes'] for x in rows),'bytes')
