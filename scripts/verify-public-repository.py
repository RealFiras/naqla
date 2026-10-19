"""Stdlib-only checks of Git source files, documentation links and historical evidence.
This is repository hygiene, not native Windows acceptance or a complete secret scanner.
"""
from pathlib import Path,PurePosixPath
import json,re,subprocess,sys,urllib.parse,posixpath
ROOT=Path(__file__).resolve().parents[1]
tracked=subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode('utf-8').split('\0')
files={x for x in tracked if x}; errors=[]
for name in sorted(files):
    path=ROOT/name
    if not path.is_file(): errors.append('Missing tracked file: '+name);continue
    if name.startswith('artifacts/') or path.suffix.lower() in ('.dll','.exe','.msi','.pfx','.p12','.key','.onnx','.traineddata') or PurePosixPath(name).name.startswith('.env'):
        errors.append('Generated/private/native file tracked: '+name)
    if path.suffix.lower() not in ('.md','.cs','.csproj','.py','.ps1','.json','.yml','.yaml','.html','.cjs','.props','.targets'):continue
    text=path.read_text(encoding='utf-8-sig')
    for pattern in (r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'\bgh[pousr]_[A-Za-z0-9]{30,}\b',r'\bgithub_pat_[A-Za-z0-9_]{30,}\b',r'\bAKIA[A-Z0-9]{16}\b'):
        if re.search(pattern,text): errors.append('Possible credential in '+name)
    # Vendored license text keeps upstream-relative links verbatim.
    if path.suffix.lower()!='.md' or name.startswith('assets/licenses/'):continue
    for target in re.findall(r'\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)',text):
        target=target.strip('<>')
        if target.startswith(('#','https://','http://','mailto:')):continue
        decoded=urllib.parse.unquote(target.split('#',1)[0])
        if not decoded:continue
        relative=posixpath.normpath(posixpath.join(posixpath.dirname(name),decoded))
        if relative not in files and not any(x.startswith(relative.rstrip('/')+'/') for x in files):
            errors.append('Broken/case-mismatched public link: '+name+' -> '+target)
snapshot=json.loads((ROOT/'docs/test-results/0.8-development.json').read_text(encoding='utf-8'))
assert snapshot['AcceptedDistinctGroups']==len(snapshot['Groups'])==123
assert all(x['Passed'] for x in snapshot['Groups'])
assert len(snapshot['ConversionPairs'])==snapshot['ConversionPairsPassed']==136
assert all(x['Status']=='PASSED' and x['OriginalHashPreserved'] for x in snapshot['ConversionPairs'])
if errors:
    for error in errors:print(error)
    sys.exit(1)
print(f'PASS public source hygiene and local Markdown links ({len(files)} tracked files).')
print('PASS historical snapshot consistency (123 groups / 136 conversion pairs; physical checks pending).')
