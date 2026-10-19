"""Stage the visual preview for Pages; source links must work outside the repository."""
from pathlib import Path
import re,shutil
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/pages'
OUT.mkdir(parents=True,exist_ok=True)
if OUT.is_symlink(): raise RuntimeError('Pages output must not be a symbolic link')
shutil.copytree(ROOT/'preview',OUT,dirs_exist_ok=True)
REPO='https://github.com/RealFiras/naqla/blob/main/'
for file in OUT.rglob('*.html'):
    text=file.read_text(encoding='utf-8')
    text=re.sub(r'href="\.\./(docs/[^"#]+\.md)(#[^"]*)?"',lambda m:'href="'+REPO+m[1]+(m[2] or '')+'"',text)
    text=re.sub(r'href="\.\./(README(?:\.ar)?\.md)"',lambda m:'href="'+REPO+m[1]+'"',text)
    file.write_text(text,encoding='utf-8')
(OUT/'.nojekyll').write_text('',encoding='utf-8')
print('Staged browser-only preview and repository documentation links in artifacts/pages.')
