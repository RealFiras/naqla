"""Actual local tray-process smoke and sampled idle use; no simulated keyboard/dragging."""
from pathlib import Path
import json,os,subprocess,time,psutil
ROOT=Path(__file__).resolve().parents[1]; EXE=ROOT/'artifacts/app-0.8-win-x64/Naqla.App.exe'
logs=Path(os.environ['LOCALAPPDATA'])/'Naqla/Diagnostics'; records=[]
runtime=json.loads((EXE.parent/'Naqla.App.runtimeconfig.json').read_text(encoding='utf-8'))['runtimeOptions']['includedFrameworks']
assert all(item['version']=='10.0.13' for item in runtime),'The packaged runtime security pin differs'
for cycle in range(3):
    before=set(logs.glob('session-*.jsonl')) if logs.exists() else set()
    process=subprocess.Popen([str(EXE)],creationflags=subprocess.CREATE_NO_WINDOW); current=psutil.Process(process.pid)
    try:
        started=time.monotonic(); session=None; ready=None
        while time.monotonic()-started<15:
            if process.poll() is not None: raise RuntimeError('Portable app exited before ready')
            candidates=set(logs.glob('session-*.jsonl'))-before
            if candidates:
                session=max(candidates,key=lambda x:x.stat().st_mtime)
                lines=[json.loads(line) for line in session.read_text(encoding='utf-8-sig').splitlines()]
                if any(line.get('Event')=='AppReady' for line in lines): ready=time.monotonic()-started; break
            time.sleep(.05)
        if ready is None: raise RuntimeError('AppReady not observed')
        handle=int(subprocess.check_output(['powershell.exe','-NoProfile','-NonInteractive','-WindowStyle','Hidden','-Command',f'(Get-Process -Id {process.pid}).MainWindowHandle.ToInt64()'],creationflags=subprocess.CREATE_NO_WINDOW,text=True).strip())
        assert handle==0,'Unexpected main window handle'
        duplicate=subprocess.run([str(EXE)],creationflags=subprocess.CREATE_NO_WINDOW,timeout=8)
        if duplicate.returncode: raise RuntimeError('Duplicate failed')
        time.sleep(1); cpu=current.cpu_times(); start_cpu=cpu.user+cpu.system; samples=[]
        for _ in range(100 if cycle==0 else 20):
            if process.poll() is not None: raise RuntimeError('App did not remain resident')
            samples.append(current.memory_info().rss); time.sleep(.1)
        cpu=current.cpu_times(); delta=cpu.user+cpu.system-start_cpu
        modules=[row.path for row in current.memory_maps()]
        runtime=[path for path in modules if Path(path).name.lower()=='coreclr.dll']
        assert runtime and Path(runtime[0]).parent==EXE.parent,'Runtime was not app-local'
        assert not any('magick.native' in path.lower() or 'onnxruntime.dll' in path.lower() for path in modules),'Heavy image/model DLL loaded at idle'
        subprocess.run([str(EXE),'--quit'],creationflags=subprocess.CREATE_NO_WINDOW,timeout=8); process.wait(timeout=15)
        lines=[json.loads(line) for line in session.read_text(encoding='utf-8-sig').splitlines()]; events=[line.get('Event') for line in lines]
        for event in ['AppReady','MouseHookInstalled','KeyboardHookInstalled','TrayVisibleRequested','HooksReleased','TrayRemoved','AppStopped']: assert event in events,event
        tray=next(line for line in lines if line.get('Event')=='TrayVisibleRequested'); assert tray['Data']['Visible']
        assert next(line for line in lines if line.get('Event')=='HooksReleased')['Data']['Succeeded']
        header=lines[0]; assert header['Build']=='0.8.0-development.1'
        raw=session.read_text(encoding='utf-8-sig'); assert str(ROOT) not in raw and os.environ['USERPROFILE'] not in raw
        records.append({'Cycle':cycle+1,'Build':header['Build'],'Runtime':'10.0.13','MainWindowHandle':handle,'StartupToAppReadySeconds':round(ready,3),'IdleSampleSeconds':len(samples)/10,'IdleCpuSeconds':round(delta,6),'IdleRssMinBytes':min(samples),'IdleRssMaxBytes':max(samples),'BundledRuntimeLoaded':True,'HeavyNativeImageOrModelLoadedAtIdle':False,'SingletonDuplicateExitCode':duplicate.returncode,'ExitCode':process.returncode,'TrayVisibleProperty':True,'HooksRemoved':True,'DiagnosticLogHasNoWorkspaceOrUserPath':True,'ActualTrayScreenshot':False,'ActualExplorerOrDpiAcceptance':'MANUAL PENDING'})
        print('Lifecycle cycle',cycle+1,'passed',flush=True)
    finally:
        if process.poll() is None:
            subprocess.run([str(EXE),'--quit'],creationflags=subprocess.CREATE_NO_WINDOW,timeout=8); process.wait(timeout=15)
(ROOT/'artifacts/portable-lifecycle.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
