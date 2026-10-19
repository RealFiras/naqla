"""Publish only the verified beta assets using the existing Git credential helper.
Tokens remain in memory; this script does not save or print credentials. Explicit phases
keep draft/upload separate from publication. Never replaces a published release.
"""
from pathlib import Path
import argparse,hashlib,json,posixpath,re,subprocess,urllib.request,urllib.error,urllib.parse,time
ROOT=Path(__file__).resolve().parents[1]; REPO='RealFiras/naqla'; TAG='v0.8.0-beta.1'

def main():
    args=argparse.ArgumentParser();args.add_argument('--phase',choices=('draft','upload','publish'),required=True);args.add_argument('--release-id',type=int);options=args.parse_args();phase=options.phase
    credential=subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,timeout=30,cwd=ROOT)
    if credential.returncode:raise RuntimeError('GitHub credential helper failed')
    fields=dict(x.split('=',1) for x in credential.stdout.splitlines() if '=' in x)
    token=fields.get('password');assert token,'No GitHub credential available'
    headers={'Authorization':'Bearer '+token,'User-Agent':'Naqla-beta-release','Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28'}
    def api(endpoint,method='GET',body=None):
        request=urllib.request.Request('https://api.github.com'+endpoint,headers=headers,method=method,data=json.dumps(body).encode() if body is not None else None)
        with urllib.request.urlopen(request,timeout=90) as response:
            raw=response.read();return json.loads(raw) if raw else None
    repo=api('/repos/'+REPO);assert repo['full_name']==REPO and repo['permissions']['admin'] and not repo['private']
    sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    notes=(ROOT/'docs/releases'/f'{TAG}.md').read_text(encoding='utf-8')
    base=f'https://github.com/{REPO}/blob/{TAG}/docs/releases/'
    notes=re.sub(r'\]\((\.\./[^)]+)\)',lambda m:']('+urllib.parse.urljoin(base,m[1])+')',notes)
    state_file=ROOT/'artifacts/public-release-draft.json'
    state=json.loads(state_file.read_text(encoding='utf-8')) if state_file.exists() else {}
    saved=state.get('Id') if state.get('Repository')==REPO and state.get('Tag')==TAG else None
    release_id=options.release_id or saved
    if release_id:
        release=api('/repos/'+REPO+'/releases/'+str(release_id))
        assert release['name']=='Naqla 0.8.0-beta.1 — Windows x64','Unexpected release identity'
    else:
        releases=api('/repos/'+REPO+'/releases?per_page=100&fresh='+str(time.time_ns()))
        release=next((x for x in releases if x['tag_name']==TAG),None)
    if release and not release['draft']:
        print('Release already published: '+release['html_url']);return
    if release is None:
        release=api('/repos/'+REPO+'/releases','POST',{'tag_name':TAG,'target_commitish':'main','name':'Naqla 0.8.0-beta.1 — Windows x64','body':notes,'draft':True,'prerelease':True})
    else:
        # A draft without an existing Git tag can become "untagged-*" when edited.
        # Keep its explicit identity on every update and persist the server ID.
        release=api('/repos/'+REPO+'/releases/'+str(release['id']),'PATCH',{'tag_name':TAG,'target_commitish':'main','body':notes,'prerelease':True})
    state_file.write_text(json.dumps({'Repository':REPO,'Tag':TAG,'Id':release['id']}),encoding='utf-8')
    print('Draft ready: '+str(release['id']),flush=True)
    if phase=='draft':return
    output=Path((ROOT/'artifacts/public-release-path.txt').read_text(encoding='utf-8-sig').strip()).resolve()
    assert output.is_relative_to((ROOT/'artifacts/public-release').resolve()),'Invalid release output'
    build=json.loads((output/'build.json').read_text(encoding='utf-8-sig'));assert build['Tag']==TAG
    verification=json.loads((output/'public-beta-verification.json').read_text(encoding='utf-8'))
    assert verification['InstallerSHA256'].lower()==build['SHA256'].lower() and verification['InstallSucceeded'] and verification['StartupConfigured'] and verification['InstalledAppReady']
    assets=[output/build['Installer'],output/'public-beta-verification.json']
    component_dir=ROOT/'artifacts/public-components'
    assets += [component_dir/f'Naqla-component-{name}.zip' for name in ('ocr','background','speech','archives','pdf-metadata')]
    assets += [component_dir/'7z2604-src.7z']
    def digest(p):
        with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
    assert digest(assets[0]).lower()==build['SHA256'].lower(),'Installer changed after verification'
    checksums=output/'SHA256SUMS.txt';checksums.write_text(''.join(digest(p)+'  '+p.name+'\n' for p in assets),encoding='utf-8');assets.append(checksums)
    existing={x['name']:x for x in api('/repos/'+REPO+'/releases/'+str(release['id'])+'/assets')}
    for asset in assets:
        if asset.name in existing:
            remote=existing[asset.name]
            assert remote['size']==asset.stat().st_size and remote.get('digest')=='sha256:'+digest(asset),'Existing asset differs; refusing replacement: '+asset.name
            print('Already verified: '+asset.name,flush=True);continue
        if phase=='publish':raise RuntimeError('Upload all verified assets before publication: '+asset.name)
        url='https://uploads.github.com/repos/'+REPO+'/releases/'+str(release['id'])+'/assets?name='+urllib.parse.quote(asset.name)
        request_headers=dict(headers,**{'Content-Type':'application/octet-stream','Content-Length':str(asset.stat().st_size)})
        with asset.open('rb') as data:
            request=urllib.request.Request(url,data=data,headers=request_headers,method='POST')
            with urllib.request.urlopen(request,timeout=600) as response:result=json.load(response)
        assert result['size']==asset.stat().st_size and result.get('digest')=='sha256:'+digest(asset),'Server asset digest mismatch'
        print('Uploaded and SHA256 verified: '+asset.name,flush=True)
    if phase=='publish':
        remote_sha=subprocess.check_output(['git','ls-remote','origin','refs/heads/main'],cwd=ROOT,text=True).split()[0]
        assert remote_sha==sha,'Push the verified source commit before publishing'
        published=api('/repos/'+REPO+'/releases/'+str(release['id']),'PATCH',{'tag_name':TAG,'target_commitish':sha,'draft':False,'prerelease':True,'body':notes,'make_latest':'false'})
        print('Published official beta: '+published['html_url'],flush=True)

if __name__=='__main__':
    try:main()
    except urllib.error.HTTPError as error:
        print('GitHub request failed with HTTP '+str(error.code));raise SystemExit(1)
