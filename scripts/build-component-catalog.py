"""Build separate, explicit offline packs from verified development dependencies.
Does not install components or enable plugins. Run after the pinned fetch/extraction scripts.
"""
from pathlib import Path
import hashlib,json,zipfile,shutil
ROOT=Path(__file__).resolve().parents[1]; SOURCE=ROOT/'artifacts/optional-components'; CATALOG={}; PACKS=ROOT/'artifacts/component-packs'
PACKS.mkdir(exist_ok=True)
def digest(path):
    with path.open('rb') as file: return hashlib.file_digest(file,'sha256').hexdigest()
def add(feature,path,name):
    CATALOG[name]={'Feature':feature,'Bytes':path.stat().st_size,'Sha256':digest(path)}
    return path,name
groups={
 'ocr':[add('ocr', SOURCE/'tessdata'/f'{lang}.traineddata',f'tessdata/{lang}.traineddata') for lang in ('eng','ara')],
 'background':[add('background',SOURCE/'u2netp.onnx','u2netp.onnx')],
 'speech':[add('speech',SOURCE/'ggml-tiny.bin','ggml-tiny.bin')],
 'archives':[add('archives',SOURCE/'sevenzip/x64/7za.exe','sevenzip/x64/7za.exe')],
 'pdf-metadata':[], 'office':[]}
groups['speech'].append(add('speech',SOURCE/'ggml-base.bin','ggml-base.bin'))
for path in (SOURCE/'whisper/Release').iterdir():
    if path.suffix=='.dll' and (path.name.startswith('ggml') or path.name=='whisper.dll') or path.name=='whisper-cli.exe':
        groups['speech'].append(add('speech',path,'whisper/Release/'+path.name))
qpdf=SOURCE/'qpdf/qpdf-12.4.2-mingw64'
for path in (qpdf/'bin').iterdir():
    if path.suffix=='.dll' or path.name=='qpdf.exe': groups['pdf-metadata'].append(add('pdf-metadata',path,'qpdf-engine/'+path.name))
for path in (SOURCE/'office').rglob('*'):
    if path.is_file() and '__pycache__' not in path.parts and path.suffix != '.pyc': groups['office'].append(add('office',path,'office/'+path.relative_to(SOURCE/'office').as_posix()))
licenses={'ocr':SOURCE/'licenses/tessdata-Apache-2.0.txt','background':SOURCE/'licenses/u2net-Apache-2.0.txt','speech':SOURCE/'licenses/whisper-MIT.txt','archives':SOURCE/'sevenzip/License.txt'}
for feature,path in licenses.items(): groups[feature].append(add(feature,path,f'component-licenses/{feature}.txt'))
groups['speech'].append(add('speech',SOURCE/'licenses/whisper-model-MIT.txt','component-licenses/whisper-model-MIT.txt'))
for path in (ROOT/'assets/native-crt').glob('*.dll'):
    groups['speech'].append(add('speech',path,'whisper/Release/'+path.name))
groups['speech'].append(add('speech',ROOT/'assets/licenses/Microsoft-VC-Runtime-LICENSE.txt','component-licenses/speech-CRT-LICENSE.txt'))
for name in ('qpdf-Apache-2.0.txt','qpdf-NOTICE.txt','gcc-GPL-3.txt','gcc-runtime-exception.txt','winpthreads-MIT.txt'):
    groups['pdf-metadata'].append(add('pdf-metadata',SOURCE/'licenses'/name,'component-licenses/'+name))
for name in ('LICENSE.txt','NOTICE.md','LICENSE','NOTICE'):
    path=qpdf/name
    if path.exists(): groups['pdf-metadata'].append(add('pdf-metadata',path,'qpdf-engine/'+name))
(ROOT/'assets/component-catalog.json').write_text(json.dumps(CATALOG,indent=2),encoding='utf-8')
for feature,files in groups.items():
    if '--catalog-only' in __import__('sys').argv: continue
    output=PACKS/f'Naqla-component-{feature}.zip'
    with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as zip:
        zip.writestr('component.json',json.dumps({'SchemaVersion':1,'Feature':feature,'CatalogVersion':'2026-10-19'}))
        for path,name in files: zip.write(path,name)
    print(f'{feature}: {len(files)} files, {output.stat().st_size} bytes, SHA256 {digest(output)}',flush=True)
print(f'Catalog: {len(CATALOG)} pinned files.',flush=True)
