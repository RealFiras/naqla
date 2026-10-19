"""Developer-only fetch. The installed application never imports this script or downloads engines/models.
Run explicitly with Python 3; emits a SHA256 inventory for the separate offline component pack.
"""
import concurrent.futures, hashlib, json, pathlib, urllib.request, zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEST = ROOT / 'artifacts' / 'optional-components'
PIN = '87416418657359cb625c412a48b6e1d6d41c29bd'
ASSETS = [
    ('whisper-bin-x64.zip', 'https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip', '6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'),
    ('ggml-tiny.bin', 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin', 'be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21'),
    ('ggml-base.bin', 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin', '60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe'),
    ('u2netp.onnx', 'https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx', None),
    ('tessdata/eng.traineddata', f'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/{PIN}/eng.traineddata', None),
    ('tessdata/ara.traineddata', f'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/{PIN}/ara.traineddata', None),
    ('7z2604-extra.7z', 'https://github.com/ip7z/7zip/releases/download/26.04/7z2604-extra.7z', 'dc4b11d3399db18b063630137145f5585d8f7ac847bf3639bd1185d7d1f7cee0'),
    ('licenses/whisper-MIT.txt', 'https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.9.5/LICENSE', None),
    ('licenses/whisper-model-MIT.txt', 'https://raw.githubusercontent.com/openai/whisper/main/LICENSE', None),
    ('licenses/qpdf-Apache-2.0.txt', 'https://raw.githubusercontent.com/qpdf/qpdf/v12.4.2/LICENSE.txt', None),
    ('licenses/qpdf-NOTICE.txt', 'https://raw.githubusercontent.com/qpdf/qpdf/v12.4.2/NOTICE.md', None),
    ('licenses/gcc-GPL-3.txt', 'https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-15.2.0/COPYING3', None),
    ('licenses/gcc-runtime-exception.txt', 'https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-15.2.0/COPYING.RUNTIME', None),
    ('licenses/winpthreads-MIT.txt', 'https://raw.githubusercontent.com/mingw-w64/mingw-w64/v13.0.0/mingw-w64-libraries/winpthreads/COPYING', None),
    ('licenses/u2net-Apache-2.0.txt', 'https://raw.githubusercontent.com/xuebinqin/U-2-Net/master/LICENSE', None),
    ('licenses/tessdata-Apache-2.0.txt', f'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/{PIN}/LICENSE', None),
    ('fixtures/jfk.wav', 'https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.9.5/samples/jfk.wav', None),
]

def fetch(asset):
    relative, url, expected = asset
    path = DEST / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        tmp = path.with_name(path.name + '.download')
        try:
            request = urllib.request.Request(url, headers={'User-Agent': 'Naqla-development-dependency-fetch'})
            with urllib.request.urlopen(request, timeout=120) as source, tmp.open('wb') as target:
                while chunk := source.read(1024 * 1024):
                    target.write(chunk)
            tmp.replace(path)
        finally:
            tmp.unlink(missing_ok=True)
    digest = hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()
    if expected and digest != expected:
        raise RuntimeError(f'Pinned checksum mismatch: {relative}')
    print(f'{relative}: {path.stat().st_size} bytes', flush=True)
    return {'File': relative, 'Url': url, 'Sha256': digest, 'Bytes': path.stat().st_size, 'UpstreamPinnedDigest': expected is not None}

if __name__ == '__main__':
    DEST.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        inventory = list(pool.map(fetch, ASSETS))
    # This official binary ZIP is still inspected before extraction. No executables are launched by this script.
    with zipfile.ZipFile(DEST / 'whisper-bin-x64.zip') as z:
        for info in z.infolist():
            p = pathlib.PurePosixPath(info.filename.replace('\\', '/'))
            if p.is_absolute() or '..' in p.parts or ':' in info.filename or info.file_size > 100_000_000:
                raise RuntimeError('Invalid upstream binary ZIP')
        z.extractall(DEST / 'whisper')
    (DEST / 'sources.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
