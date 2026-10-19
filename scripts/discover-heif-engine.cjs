// Developer bootstrap only: resolve official MSYS2 packages, verify their published hashes,
// and record an immutable manifest for fetch-heif-engine.ps1. Never used by the installed app.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const {execFileSync} = require('node:child_process');
const root = path.resolve(__dirname, '..');
const cache = path.join(root, 'artifacts/downloads/heif');
const staging = path.join(root, 'artifacts/heif-staging');
fs.mkdirSync(cache, {recursive:true}); fs.mkdirSync(staging, {recursive:true});
const prefix = 'mingw-w64-ucrt-x86_64-';
async function get(url) {
 const response = await fetch(url, {signal:AbortSignal.timeout(60000)});
 if (!response.ok) throw new Error(`${response.status}: ${url}`);
 return response;
}
(async()=>{
 const queue = [prefix+'libheif']; const seen = new Set(); const packages = [];
 while(queue.length){
  const name = queue.shift(); if(seen.has(name)) continue; seen.add(name);
  // SDL is required by the unrelated viewer, not heif-enc/dec/info. PE closure below verifies this.
  if(name === prefix+'SDL2') continue;
  if(seen.size > 70) throw new Error('Unexpected dependency graph size');
  const html = await (await get('https://packages.msys2.org/packages/'+name)).text();
  const url = html.match(/https:\/\/mirror\.msys2\.org\/mingw\/ucrt64\/[^"<>]+\.pkg\.tar\.zst/)?.[0];
  const sha256 = html.match(/SHA256:[\s\S]{0,250}?([a-f0-9]{64})/)?.[1];
  if(!url || !sha256) {
   const providers=html.split('Provided By:')[1]?.split('Required By:')[0];
   const provider=providers?.match(/https:\/\/packages\.msys2\.org\/packages\/(mingw-w64-ucrt-x86_64-[^"<>]+)/)?.[1];
   if(provider){queue.push(provider);continue;}
   throw new Error('Package metadata not found: '+name);
  }
  const archive = path.join(cache, path.basename(url));
  if(!fs.existsSync(archive) || crypto.createHash('sha256').update(fs.readFileSync(archive)).digest('hex') !== sha256){
   const bytes = Buffer.from(await (await get(url)).arrayBuffer());
   if(crypto.createHash('sha256').update(bytes).digest('hex')!==sha256)throw new Error('Checksum mismatch: '+name);
   fs.writeFileSync(archive,bytes);
  }
  // Some Windows tar builds narrow argv paths. Keep argv ASCII while the Unicode cwd is set by Win32.
  const archiveArg=path.relative(root,archive);
  const info = execFileSync('tar.exe',['-xOf',archiveArg,'.PKGINFO'],{encoding:'utf8',cwd:root});
  const deps = [...info.matchAll(/^depend = ([^\s>=<]+)/gm)].map(x=>x[1]); queue.push(...deps.filter(x=>x.startsWith(prefix)));
  execFileSync('tar.exe',['-xf',archiveArg,'-C',path.relative(root,staging),'--exclude=ucrt64/include','--exclude=ucrt64/lib','--exclude=ucrt64/share/man','--exclude=ucrt64/share/doc'],{stdio:'pipe',cwd:root});
  const version=info.match(/^pkgver = (.+)$/m)?.[1], base=info.match(/^pkgbase = (.+)$/m)?.[1];
  packages.push({name,url,sha256,version,source:`https://mirror.msys2.org/mingw/sources/${base}-${version}.src.tar.zst`});
  console.log('Verified '+name);
 }
 const bin = path.join(staging,'ucrt64/bin');
 const files = fs.readdirSync(bin); const byName = new Map(files.map(x=>[x.toLowerCase(),x]));
 function imports(file){
  const b=fs.readFileSync(file), pe=b.readUInt32LE(0x3c), optional=pe+24;
  const sections=[]; const count=b.readUInt16LE(pe+6), start=optional+b.readUInt16LE(pe+20);
  for(let i=0;i<count;i++){const p=start+i*40;sections.push({rva:b.readUInt32LE(p+12),size:Math.max(b.readUInt32LE(p+8),b.readUInt32LE(p+16)),raw:b.readUInt32LE(p+20)});}
  function offset(rva){const s=sections.find(s=>rva>=s.rva&&rva<s.rva+s.size);if(!s)throw new Error('Invalid PE RVA');return s.raw+rva-s.rva;}
  const dir=optional+(b.readUInt16LE(optional)===0x20b?112:96);
  const rva=b.readUInt32LE(dir+8); if(!rva)return [];
  let p=offset(rva); const result=[];
  while(b.readUInt32LE(p+12)){const n=offset(b.readUInt32LE(p+12));result.push(b.toString('ascii',n,b.indexOf(0,n)));p+=20;}
  return result;
 }
 const pending=['heif-enc.exe','heif-dec.exe','heif-info.exe']; const selected=new Set();
 while(pending.length){
  const name=pending.shift();if(selected.has(name.toLowerCase()))continue;selected.add(name.toLowerCase());
  for(const dependency of imports(path.join(bin,name))){
   const own=byName.get(dependency.toLowerCase());
   if(own)pending.push(own);
   else if(!dependency.toLowerCase().startsWith('api-ms-win-')&&!fs.existsSync(path.join(process.env.SystemRoot,'System32',dependency)))throw new Error('Missing native dependency: '+dependency);
  }
 }
 const manifest={provider:'MSYS2 UCRT64 official packages',packages,files:[...selected].map(x=>byName.get(x)).sort().map(name=>({name,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(bin,name))).digest('hex')}))};
 fs.writeFileSync(path.join(root,'scripts/heif-engine.lock.json'),JSON.stringify(manifest,null,2)+'\n');
 console.log(JSON.stringify({packages:packages.length,nativeFiles:manifest.files.length}));
})().catch(error=>{console.error(error.message);process.exitCode=1});
