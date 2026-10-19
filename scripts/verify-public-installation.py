"""Explicit current-user installation and verification. MUTATES the current user's app,
startup preference and, when requested, component store. Never restarts Windows.
Requires --install; not a CI/clean-account or physical Explorer test.
"""
from pathlib import Path
import argparse,hashlib,json,os,subprocess,time,base64

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install',action='store_true',required=True)
    parser.add_argument('--enable-startup',action='store_true')
    parser.add_argument('--import-public-packs',action='store_true')
    parser.add_argument('--engine-cache',type=Path)
    parser.add_argument('--offline-cache',action='store_true')
    args=parser.parse_args()
    if os.name!='nt':raise RuntimeError('Windows required')
    import winreg,psutil
    root=Path(__file__).resolve().parents[1]
    out=Path((root/'artifacts/public-release-path.txt').read_text(encoding='utf-8-sig').strip()).resolve()
    assert out.is_relative_to((root/'artifacts/public-release').resolve())
    read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
    def digest(p):
        with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
    build=read(out/'build.json'); installer=out/build['Installer']
    assert digest(installer).lower()==build['SHA256'].lower()
    command=[str(installer),'--install','--download-engines']
    if args.enable_startup:command.append('--start-with-windows')
    if args.engine_cache:command+=['--engine-cache',str(args.engine_cache.resolve())]
    if args.offline_cache:command.append('--offline-cache')
    result=subprocess.run(command,timeout=600,creationflags=subprocess.CREATE_NO_WINDOW)
    assert result.returncode==0,'Installer failed with exit code '+str(result.returncode)
    print('PASS actual current-user installation transaction.',flush=True)
    installed=Path(os.environ['LOCALAPPDATA'])/'Programs/Naqla'; exe=installed/'Naqla.App.exe'
    assert exe.is_file()
    source_files=[p for p in (out/'app').rglob('*') if p.is_file()]
    for source in source_files:
        target=installed/source.relative_to(out/'app')
        assert target.is_file() and digest(source)==digest(target),'Installed payload mismatch'
    manifest=read(installed/'.naqla-installed-files.json');assert manifest['Schema']==2
    for file in manifest['Files']:assert digest(installed/file['Path']).lower()==file['Sha256'].lower(),'Installed ownership hash differs'
    heif=read(root/'scripts/heif-engine.lock.json')
    for file in heif['files']:assert digest(installed/'heif-engine'/file['name']).lower()==file['sha256'].lower()
    crt=read(root/'assets/native-crt/sources.json')
    assert not any((installed/file['File']).exists() for file in crt['Files']),'Proprietary CRT unexpectedly bundled'
    frameworks=read(installed/'Naqla.App.runtimeconfig.json')['runtimeOptions']['includedFrameworks']
    assert all(x['version']=='10.0.13' for x in frameworks)
    imported=[]
    if args.import_public_packs:
        for feature in ('ocr','background','speech','archives','pdf-metadata'):
            package=root/'artifacts/public-components'/f'Naqla-component-{feature}.zip'
            result=subprocess.run([str(exe),'--import-component',str(package)],timeout=300,creationflags=subprocess.CREATE_NO_WINDOW)
            assert result.returncode==0,'Public component import failed: '+feature
            imported.append(feature);print('PASS installed public pack: '+feature,flush=True)
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\Microsoft\Windows\CurrentVersion\Run') as key:
        startup=winreg.QueryValueEx(key,'Naqla')[0] if args.enable_startup else None
    assert not args.enable_startup or startup=='"'+str(exe)+'"','Startup points to the wrong executable'
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run') as key:
            approved=winreg.QueryValueEx(key,'Naqla')[0]
        assert not approved or approved[0]!=3,'Windows has independently disabled this startup entry'
    except FileNotFoundError:pass
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\Microsoft\Windows\CurrentVersion\Uninstall\Naqla.Development') as key:
        assert winreg.QueryValueEx(key,'InstallLocation')[0]==str(installed)
        assert winreg.QueryValueEx(key,'DisplayVersion')[0]=='0.8.0-beta.1'
    logs=Path(os.environ['LOCALAPPDATA'])/'Naqla/Diagnostics';before=set(logs.glob('session-*.jsonl'))
    process=subprocess.Popen([str(exe)],creationflags=subprocess.CREATE_NO_WINDOW)
    session=None;entries=[]
    for _ in range(200):
        assert process.poll() is None,'Installed tray app exited before readiness'
        candidates=set(logs.glob('session-*.jsonl'))-before
        if candidates:
            session=max(candidates,key=lambda p:p.stat().st_mtime)
            try:entries=[json.loads(x) for x in session.read_text(encoding='utf-8-sig').splitlines()]
            except json.JSONDecodeError:time.sleep(.1);continue
            if any(x.get('Event')=='AppReady' for x in entries):break
        time.sleep(.1)
    assert session and any(x.get('Event')=='AppReady' for x in entries),'No AppReady event'
    assert entries[0]['Build']=='0.8.0-beta.1'
    assert next(x for x in entries if x.get('Event')=='TrayVisibleRequested')['Data']['Visible']
    raw=session.read_text(encoding='utf-8-sig');assert os.environ['USERPROFILE'] not in raw and str(root) not in raw
    duplicate=subprocess.run([str(exe)],timeout=10,creationflags=subprocess.CREATE_NO_WINDOW);assert duplicate.returncode==0
    runtime=[m.path for m in psutil.Process(process.pid).memory_maps() if Path(m.path).name.lower()=='coreclr.dll']
    assert len(runtime)==1 and Path(runtime[0]).parent==installed
    powershell="$p=[Environment]::GetFolderPath('Programs')+'\\Naqla.lnk';if(!(Test-Path -LiteralPath $p)){Write-Output 'False';exit};$s=New-Object -ComObject WScript.Shell;$l=$s.CreateShortcut($p);Write-Output ($l.TargetPath -eq ([Environment]::GetFolderPath('LocalApplicationData')+'\\Programs\\Naqla\\Naqla.App.exe'))"
    encoded=base64.b64encode(powershell.encode('utf-16-le')).decode('ascii')
    shortcut=subprocess.check_output(['powershell.exe','-NoProfile','-NonInteractive','-WindowStyle','Hidden','-EncodedCommand',encoded],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()=='True'
    assert shortcut,'Start-menu shortcut missing or points elsewhere'
    report={'Build':'0.8.0-beta.1','Installer':build['Installer'],'InstallerSHA256':build['SHA256'],
      'InstallSucceeded':True,'InstalledPayloadFilesCompared':len(source_files),'OwnedFilesHashVerified':len(manifest['Files']),
      'HeifNativePinsVerified':len(heif['files']),'BundledRuntime':'10.0.13','BundledRuntimeLoaded':True,
      'StandaloneMicrosoftCrtBundled':False,'StartupConfigured':args.enable_startup,'StartMenuShortcutVerified':shortcut,
      'InstalledAppReady':True,'TrayVisibleProperty':True,'SingletonDuplicateExitCode':duplicate.returncode,
      'LocalDiagnosticsExcludePrivatePaths':True,'PublicPacksImported':imported,
      'OfflineInstallerCacheModeUsed':args.offline_cache,'ActualLoginRestartTested':False,
      'PhysicalExplorerDesktopGestureVerified':False,'CleanAccountOrUninstallVerified':False,
      'Classification':'OFFICIAL PUBLIC BETA; manual acceptance gates remain',
      'HistoricalProcessingBaseline':'123 distinct checks / 136 pairs, including private Office, recorded separately; not a new full run'}
    (out/'public-beta-verification.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))
    print('Installed beta is left running. Windows was not restarted.',flush=True)

if __name__=='__main__':main()
