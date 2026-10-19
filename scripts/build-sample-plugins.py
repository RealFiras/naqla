from pathlib import Path
import json,zipfile,hashlib
ROOT=Path(__file__).resolve().parents[1]; OUT=ROOT/'artifacts/sample-plugins'; OUT.mkdir(exist_ok=True)
records=[]
for id,name,label,operation,extensions in [('sample.hash','Local SHA-256','SHA-256','sha256',['PNG','JPG','PDF','TXT','MP4','MP3','DOCX']),('sample.text','Local text statistics','إحصاءات النص','text-statistics',['TXT','HTML'])]:
    manifest={'SchemaVersion':1,'Id':id,'Name':name,'Version':'1.0.0','ApiVersion':1,'Capabilities':['read-selection','write-result'],'Actions':[{'Id':'action','Label':label,'Operation':operation,'Extensions':extensions}]}
    path=OUT/(id+'.naqla-plugin.zip')
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as zip:
        zip.writestr('manifest.json',json.dumps(manifest,ensure_ascii=False,indent=2))
        zip.writestr('README.md',f'# {name}\n\nNaqla declarative API 1 sample. MIT. Uses trusted fixed host operation `{operation}` only. No package code or networking. Install disabled; enable after reviewing read-selection/write-result permissions.\n')
    records.append({'File':path.name,'Sha256':hashlib.file_digest(path.open('rb'),'sha256').hexdigest()})
(OUT/'sources.json').write_text(json.dumps(records,indent=2),encoding='utf-8'); print(json.dumps(records,indent=2))
