# Measured development performance

Environment: Microsoft Windows 11 Home 10.0.26300, build 26300;
13th Gen Intel(R) Core(TM) i5-13420H, 12 logical processors,
15.6 GB RAM. One developer PC, partly warm file
caches and other test activity; results are not broad hardware benchmarks.

## Actual self-contained tray process

| Cycle | Launch → logged AppReady | Idle sample | CPU consumed in sample | Peak sampled RSS |
|---|---|---|---|---|
| 1 | 2.281 s | 10.0 s | 0.046875 s | 100.4 MB |
| 2 | 0.769 s | 2.0 s | 0.031250 s | 99.5 MB |
| 3 | 0.691 s | 2.0 s | 0.000000 s | 97.6 MB |

Three actual launch/duplicate-launch/graceful-exit cycles. MainWindowHandle stayed
zero; bundled coreclr was loaded from the app folder; heavy Magick/ONNX native
engines were absent at idle. NotifyIcon.Visible and successful native-hook removal
were checked. This is a process/property smoke, not a tray screenshot or Explorer
gesture test. Launch-to-AppReady is **not** Shift-to-visible-wheel latency.
Evidence: `artifacts/portable-lifecycle.json`.

## Actual processing workloads

| Workload | Wall time | Peak sampled root + descendants | 100 ms samples |
|---|---|---|---|
| ocr | 2.092 s | 164.4 MB | 19 |
| background | 1.126 s | 368.0 MB | 10 |
| speech | 9.152 s | 383.3 MB | 79 |
| media | 4.039 s | 105.7 MB | 35 |
| plugins | 1.139 s | 87.0 MB | 10 |

Each workload executes real tests in an owned directory. Sampling records only that
test process and its descendants. Values are sampled lower bounds, not instantaneous
peak guarantees or the production tray's idle memory. Workloads include fixtures,
startup, validation and file I/O, so these are not pure encoder throughput figures.
Evidence: `artifacts/performance-runtime-final/results.json` and workload test logs/results.

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
