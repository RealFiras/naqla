from pathlib import Path
import json,os
ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
idle=read(ROOT/'artifacts/portable-lifecycle.json'); perf=Path(os.environ.get('NAQLA_PERF_RESULTS',str(ROOT/'artifacts/performance-runtime-final'))); workloads=read(perf/'results.json'); env=read(ROOT/'artifacts/environment.json')
assert all(x['ExitCode']==0 and x['HooksRemoved'] and x['MainWindowHandle']==0 for x in idle)
assert all(x['ExitCode']==0 for x in workloads)
rows='\n'.join(f"| {r['Cycle']} | {r['StartupToAppReadySeconds']:.3f} s | {r['IdleSampleSeconds']:.1f} s | {r['IdleCpuSeconds']:.6f} s | {r['IdleRssMaxBytes']/1048576:.1f} MB |" for r in idle)
work='\n'.join(f"| {r['Workload']} | {r['WallSeconds']:.3f} s | {r['SampledTreePeakBytes']/1048576:.1f} MB | {r['Samples']} |" for r in workloads)
text=f'''# Measured development performance

Environment: {env['Windows']} {env['Version']}, build {env['Build']};
{env['Cpu']}, {env['LogicalProcessors']} logical processors,
{env['PhysicalMemoryBytes']/1073741824:.1f} GB RAM. One developer PC, partly warm file
caches and other test activity; results are not broad hardware benchmarks.

## Actual self-contained tray process

| Cycle | Launch → logged AppReady | Idle sample | CPU consumed in sample | Peak sampled RSS |
|---|---|---|---|---|
{rows}

Three actual launch/duplicate-launch/graceful-exit cycles. MainWindowHandle stayed
zero; bundled coreclr was loaded from the app folder; heavy Magick/ONNX native
engines were absent at idle. NotifyIcon.Visible and successful native-hook removal
were checked. This is a process/property smoke, not a tray screenshot or Explorer
gesture test. Launch-to-AppReady is **not** Shift-to-visible-wheel latency.
Evidence: `artifacts/portable-lifecycle.json`.

## Actual processing workloads

| Workload | Wall time | Peak sampled root + descendants | 100 ms samples |
|---|---|---|---|
{work}

Each workload executes real tests in an owned directory. Sampling records only that
test process and its descendants. Values are sampled lower bounds, not instantaneous
peak guarantees or the production tray's idle memory. Workloads include fixtures,
startup, validation and file I/O, so these are not pure encoder throughput figures.
Evidence: `{perf.relative_to(ROOT).as_posix()}/results.json` and workload test logs/results.

The core queue allows one heavyweight operation at a time and at most 16 pending
submissions. Model/native components load lazily. Native processes run below-normal
priority with bounded capture/deadlines/kill-on-close jobs. Actual deadline/descendant
cleanup, model/codec cancellation and repeated image batches have acceptance tests.
Stress covers 310 genuine small-image outputs; it does not establish hours-long
session stability, 4K throughput or large-photo quality.

The Office pack is large (about 1.5 GB installed), contains ~19,000 files, and explicit
import/first integrity verification can take minutes, especially in this OneDrive
workspace. Unchanged native trees are cached with file-change invalidation. Derived
Office cache directory notifications do not masquerade as modified static native
files; actual modified executable checks remain tested.

## Still pending

Actual Shift/wheel paint latency, Ctrl responsiveness during a real OLE drag,
cross-monitor/DPI input, representative long/higher-resolution media, post-job
resident memory over hours, native crash injection and a clean-PC/offline benchmark.
No numbers are invented for these measurements. Follow MANUAL-WINDOWS-TESTS.md.
'''
(ROOT/'docs/PERFORMANCE-REPORT.md').write_text(text,encoding='utf-8')
print('Wrote measured performance report.')
