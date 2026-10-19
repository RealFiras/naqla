"""Extract the pinned official LibreOffice MSI as an optional local component; NEVER executes MSI actions.
Only reads MSI database tables and cabinet payload through 7-Zip. No registry/service/system install.
"""
import ctypes, hashlib, json, pathlib, shutil, subprocess, sys
from ctypes import wintypes as w

ROOT = pathlib.Path(__file__).resolve().parents[1]
MSI = ROOT / 'artifacts' / 'LibreOffice-fast.msi'
OUT = ROOT / 'artifacts' / 'optional-components' / 'office'
RAW = ROOT / 'artifacts' / 'office-msi-files'
EXPECTED = 'f9877032fd908beb9c0ddf06df4af5c2e85f419c42e14876c4cce5aae5fb2660'
msi = ctypes.WinDLL('msi')
msi.MsiOpenDatabaseW.argtypes = [w.LPCWSTR, w.LPCWSTR, ctypes.POINTER(w.UINT)]
msi.MsiDatabaseOpenViewW.argtypes = [w.UINT, w.LPCWSTR, ctypes.POINTER(w.UINT)]
msi.MsiViewExecute.argtypes = [w.UINT, w.UINT]
msi.MsiViewFetch.argtypes = [w.UINT, ctypes.POINTER(w.UINT)]
msi.MsiRecordGetStringW.argtypes = [w.UINT, w.UINT, w.LPWSTR, ctypes.POINTER(w.DWORD)]
msi.MsiCloseHandle.argtypes = [w.UINT]

def check(code):
    if code: raise OSError(f'Read-only MSI database error {code}')

def query(db, sql, columns):
    view = w.UINT(); check(msi.MsiDatabaseOpenViewW(db, sql, ctypes.byref(view)))
    try:
        check(msi.MsiViewExecute(view, 0)); rows = []
        while True:
            record = w.UINT(); code = msi.MsiViewFetch(view, ctypes.byref(record))
            if code == 259: break
            check(code)
            try:
                row = []
                for i in range(1, columns + 1):
                    length = w.DWORD(0); code = msi.MsiRecordGetStringW(record, i, None, ctypes.byref(length))
                    if code not in (0, 234): check(code)
                    buffer = ctypes.create_unicode_buffer(length.value + 1); length.value += 1
                    check(msi.MsiRecordGetStringW(record, i, buffer, ctypes.byref(length))); row.append(buffer.value)
                rows.append(row)
            finally: msi.MsiCloseHandle(record)
        return rows
    finally: msi.MsiCloseHandle(view)

if __name__ == '__main__':
    with MSI.open('rb') as file:
        if hashlib.file_digest(file, 'sha256').hexdigest() != EXPECTED: raise RuntimeError('Official MSI checksum mismatch')
    db = w.UINT(); check(msi.MsiOpenDatabaseW(str(MSI), None, ctypes.byref(db)))
    try:
        dirs = {id: (parent, name) for id, parent, name in query(db, 'SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`', 3)}
        components = dict(query(db, 'SELECT `Component`, `Directory_` FROM `Component`', 2))
        files = query(db, 'SELECT `File`, `Component_`, `FileName`, `FileSize` FROM `File`', 4)
        if '--inspect' in sys.argv:
            print(json.dumps({'roots': {k: v for k, v in dirs.items() if 'INSTALL' in k or not v[0]}, 'files': len(files)}, indent=2)); sys.exit(0)
    finally: msi.MsiCloseHandle(db)
    def relative(id):
        parent, name = dirs[id]
        if id == 'INSTALLLOCATION': return pathlib.Path()
        if not parent: return None
        prefix = relative(parent)
        if prefix is None: return None
        name = name.split(':')[0].split('|')[-1]
        if name in ('', '.'): return prefix
        if '/' in name or '\\' in name or name == '..': raise RuntimeError('Unexpected MSI directory name')
        return prefix / name
    RAW.mkdir(parents=True, exist_ok=True); OUT.mkdir(parents=True, exist_ok=True)
    subprocess.run([r'C:\Program Files\7-Zip\7z.exe', 'x', '-y', str(MSI), '-o' + str(RAW)], check=True, stdout=subprocess.DEVNULL)
    inventory = []
    for key, component, name, size in files:
        prefix = relative(components[component])
        if prefix is None: continue
        name = name.split('|')[-1]
        if '/' in name or '\\' in name or ':' in name: raise RuntimeError('Unexpected MSI file name')
        src = RAW / key; dst = OUT / prefix / name
        if not src.is_file() or src.stat().st_size != int(size): raise RuntimeError('MSI payload file missing/size mismatch: ' + key)
        dst.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(src, dst)
        inventory.append({'File': dst.relative_to(OUT).as_posix(), 'Bytes': dst.stat().st_size, 'Sha256': hashlib.file_digest(dst.open('rb'), 'sha256').hexdigest()})
    # Official redistributable CRT files stay app-local; no system installation.
    for src in RAW.iterdir():
        if src.is_file() and '_amd64.' in src.name and src.name.lower().endswith('dfefc2fe_eee6_424c_841b_d4e66f0c84a3'):
            name = src.name.split('_amd64.')[0]
            if name.lower().endswith('.dll'): shutil.copyfile(src, OUT / 'program' / name)
    (OUT.parent / 'office-source.json').write_text(json.dumps({'Version': '26.2.6', 'Source': 'Official LibreOffice Windows x64 MSI', 'MsiSha256': EXPECTED, 'SystemInstalled': False, 'Files': inventory}, indent=2), encoding='utf-8')
    print(f'Extracted {len(inventory)} files; no MSI actions were run.', flush=True)
