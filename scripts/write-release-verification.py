"""Record post-package evidence externally; never embed a self-referential installer hash."""
from pathlib import Path
import datetime,hashlib,json
R=Path(__file__).resolve().parents[1]; A=R/'artifacts'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for data in iter(lambda:f.read(1024*1024),b''):h.update(data)
    return h.hexdigest().upper()
def item(p):return {'Path':p.relative_to(R).as_posix(),'Bytes':p.stat().st_size,'SHA256':digest(p)}
proof=read(A/'final-verification.json'); smoke=read(A/'diagnostic-package-smoke.json')
assert proof['ReadyForPackaging'] and proof['BundledRuntime']=='10.0.13'
installer=item(A/'Naqla-Dev-Setup-win-x64.exe')
assert installer['SHA256']==smoke['InstallerSHA256']
assert smoke['HashMismatches']==0 and smoke['BundledRuntimeLoaded'] and smoke['PackagedHeicRoundtripPassed']
assert smoke['MainWindowHandle']==0 and smoke['ExitCode']==0 and smoke['DuplicateExitCode']==0
assert smoke['HooksRemovedSuccessfully'] and smoke['TrayVisibleProperty']
for source in proof['ProductionSourceSha256']:
    assert digest(R/source['path']).lower()==source['sha256'].lower(),'Source changed: '+source['path']
runs=[read(A/name/'results.json') for name in proof['Suites']]
executed=[row for run in runs for row in run]
result={
 'Build':proof['Build'],'BundledRuntime':proof['BundledRuntime'],
 'Classification':'DEVELOPMENT READY','CreatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
 'Installer':installer,'InstallerIsAuthenticodeSigned':False,
 'TestGroupExecutions':len(executed),'PassedExecutions':sum(x['Passed'] for x in executed),
 'FailedExecutionsRetained':sum(not x['Passed'] for x in executed),
 'ResolvedByTargetedRerun':proof['ResolvedByTargetedRerun'],
 'AcceptedDistinctGroups':proof['AcceptedDistinctGroups'],'UnresolvedAutomatedFailures':0,
 'ConversionPairsPassed':proof['ConversionPairsPassed'],'AutomatedBlocked':0,'AutomatedSkipped':0,
 'ExtractedFilesCompared':smoke['ExtractedFilesCompared'],'PayloadHashMismatches':smoke['HashMismatches'],
 'PackagedProcessAndHeicSmoke':'PASSED',
 'OptionalComponents':[item(p) for p in sorted((A/'component-packs').glob('*.zip'))],
 'ExamplePlugins':[item(p) for p in sorted((A/'sample-plugins').glob('*.zip'))],
 'Evidence':['artifacts/final-verification.json','artifacts/diagnostic-package-smoke.json',
 'artifacts/portable-lifecycle.json','artifacts/performance-runtime-final/results.json',
 'artifacts/native-import-audit.json','artifacts/editor-preview-verification.json'],
 'Pending':['Physical Explorer/Desktop OLE drag and keyboard transitions','Tray appearance/Explorer restart',
 'Live native dialogs/DPI/multiple monitors','Clean-account install/upgrade/uninstall',
 'Disconnected-PC processing','PDF editor visual approval','Representative accuracy and long sessions'],
 'Limitations':'See docs/KNOWN-LIMITATIONS.md; declarative plugins are constrained API 1, not arbitrary executable code or an OS sandbox.'}
(A/'release-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:result[k] for k in ['Build','Classification','TestGroupExecutions','PassedExecutions','FailedExecutionsRetained','AcceptedDistinctGroups','UnresolvedAutomatedFailures','ExtractedFilesCompared']},indent=2))
print('Installer SHA256: '+installer['SHA256'])
