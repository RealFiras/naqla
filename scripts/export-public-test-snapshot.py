"""Export allowlisted historical test facts; never publish paths, fixtures or raw logs."""
from pathlib import Path
import json,hashlib
ROOT=Path(__file__).resolve().parents[1]; A=ROOT/'artifacts'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
proof=read(A/'final-verification.json')
assert proof['AcceptedDistinctGroups']==123 and all(x['Passed'] for x in proof['AcceptedGroups'])
full=next(name for name in proof['Suites'] if name.startswith('release-acceptance'))
matrix=read(A/full/'conversion-matrix.json')+read(A/full/'document-matrix.json')
assert len(matrix)==136 and all(x['Status']=='PASSED' and x['OriginalHashPreserved'] for x in matrix)
for source in proof['ProductionSourceSha256']:
    if source['path'].startswith('src/') or source['path'] in ('Directory.Build.props','Directory.Build.targets','Naqla.sln'):
        assert hashlib.file_digest((ROOT/source['path']).open('rb'),'sha256').hexdigest()==source['sha256'],'Application source changed; rerun acceptance before claiming these results'
result={
 'Schema':1,'Build':proof['Build'],'AcceptanceRecordedUtc':proof['CreatedUtc'],
 'Type':'Historical local acceptance snapshot; not a new GitHub CI run or independent certification',
 'BundledRuntime':proof['BundledRuntime'],'Classification':proof['Classification'],
 'GroupExecutions':124,'PassedExecutions':123,'RetainedFailedExecutions':1,
 'FailureExplanation':'The full run reused a directory containing a deliberately modified model; the product correctly refused overwrite. The affected pack test passed after test fixture roots were separated.',
 'AcceptedDistinctGroups':123,'UnresolvedAutomatedFailures':0,'ConversionPairsPassed':136,
 'PhysicalWindows':'MANUAL PENDING','DisconnectedPc':'MANUAL PENDING','CleanPcInstaller':'MANUAL PENDING',
 'Groups':[{k:x[k] for k in ('Test','Passed','Milliseconds')} for x in proof['AcceptedGroups']],
 'ConversionPairs':[{k:x[k] for k in ('Source','Destination','Status','OriginalHashPreserved','Milliseconds')} for x in matrix],
 'Excluded':'No source filenames, user directories, diagnostic/recovery logs, file contents or installer binaries are included.'}
out=ROOT/'docs/test-results/0.8-development.json';out.parent.mkdir(exist_ok=True)
out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Exported public historical snapshot: 123 distinct groups, 136 conversion pairs.')
