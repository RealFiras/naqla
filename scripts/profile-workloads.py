"""Developer-only real-process sampling; psutil is a test tool, never an app dependency.
100 ms samples are lower-bound observations, not instantaneous peak guarantees.
"""
from pathlib import Path
import json,subprocess,time,psutil,sys
ROOT=Path(__file__).resolve().parents[1]
EXE=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else ROOT/'artifacts/qa-runtime-final/Naqla.Tests.exe'
OUT=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else ROOT/'artifacts/performance-final'; OUT.mkdir(exist_ok=True)
reports=[]
for name,args in [('ocr',['--ocr-only']),('background',['--background-only']),('speech',['--speech-only']),('media',['--advanced-only']),('plugins',['--plugins-only'])]:
    directory=OUT/name; log=(OUT/(name+'.log')).open('w',encoding='utf-8')
    started=time.monotonic(); process=subprocess.Popen([str(EXE),str(directory),*args],stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
    root=psutil.Process(process.pid); peaks={}; peak_total=0; samples=0
    while process.poll() is None:
        total=0
        try: observed=[root]+root.children(recursive=True)
        except psutil.NoSuchProcess: observed=[]
        for item in observed:
            try:
                memory=item.memory_info().rss; total+=memory; key=(item.pid,item.name())
                peaks[key]=max(peaks.get(key,0),memory)
            except (psutil.NoSuchProcess,psutil.AccessDenied): pass
        peak_total=max(peak_total,total); samples+=1; time.sleep(.1)
    log.close()
    reports.append({'Workload':name,'ExitCode':process.returncode,'WallSeconds':round(time.monotonic()-started,3),'Samples':samples,'SampleIntervalMs':100,'SampledTreePeakBytes':peak_total,'Processes':[{'Name':key[1],'SampledPeakBytes':value} for key,value in peaks.items()],'Scope':'Actual test process and its descendants only; sampled lower bounds, not production idle or Shift latency.'})
    print(name,process.returncode,round(peak_total/1048576,1),'MB',flush=True)
    (OUT/'results.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
if any(row['ExitCode'] for row in reports): raise SystemExit(1)
