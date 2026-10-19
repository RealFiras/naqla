# Advanced implementation plan — 2026-10-19

This document retains the earlier batch/OCR/archive/background-removal roadmap.
Current code is 0.7.0-pdfpreview.1: Smart Rename and Target Size are implemented; the PDF
toolkit has real processing and a native editor draft pending design/desktop acceptance.
The newer eight-feature request proceeds incrementally; Feature 4 has not started.
The current status/order is [FEATURE-AUDIT.md](FEATURE-AUDIT.md); the older Phase 2 gate
still applies to OCR/archives/background removal.

The following baseline describes the original inspection, not current capability.
This is an incremental change to the existing solution. Inspection found no `.git`
directory or branch metadata in this workspace, and no applicable AGENTS.md. Current
baseline is 0.3.0-features.1 with 30 passing groups, real local codecs, multi-image PDF
merge, a single active-operation lifecycle and collision-safe output publication.
There is no general batch queue, OCR, archive backend or AI model provider yet.

## Execution order and acceptance gates

1. **Batch actions — implemented, real Explorer acceptance pending.** Generalize real OLE selections; intersect existing
   backend operations; retain per-file validation and report failures without skipping.
   Add a reusable sequential job queue (one heavyweight job at a time), cancellation,
   partial-success results, distinct output directories and compact progress/results.
   Keep circle/capsule geometry and gestures. Only hub count/action data need changes.
   Preserve explicit Merge PDF as an aggregate operation; Convert → PDF now converts
   each source separately. Add actual file/codec, error, concurrency and render tests.
   **Gate:** real Explorer multi-file drag must produce verified batch outputs.
2. **Offline OCR — pending Phase 1 acceptance.** Evaluate Arabic/English providers,
   package languages offline, lazy-load behind an OCR contract, retain page order and
   native PDF text where available. Add compact text Copy/Save and deterministic OCR
   accuracy tests. Extra wheel actions need readable paging, with a preview first if
   that requires a substantial visual alteration.
3. **Archives — pending.** Evaluate ZIP/7Z/TAR backends and licenses; reuse queue/output
   handling. Add bounded extraction in a private directory, traversal/link defenses,
   local password prompts, hash/content and adversarial archive tests.
4. **Background removal — pending.** Evaluate compact licensed ONNX models; lazy CPU
   inference; verified optional per-user model pack with explicit consent for any
   download and a manual local installation route. Add preview, alpha/mask/quality,
   cancellation and memory tests. No model/network initialization at startup.
5. **Shared UX — pending.** Reuse batch progress/results and capability registration;
   readable action overflow; preserve the wheel's identity and bottom selector.
6. **Performance/regression — per phase and final.** Compare process idle use with
   baseline, test existing codecs/Ctrl/rendering, document real desktop Shift/Escape/
   Explorer/DPI/latency checks, and perform offline acceptance without claiming a
   disconnected-network test unless connectivity was actually disabled.

## Phase 1 details

- Maximum 200 real local files; inspect each path and report invalid selections.
- Common actions must be supported for every member, never a silently filtered subset.
- Ordinary conversion creates one result beside each corresponding source. Explicit
  Merge creates one PDF from all images beside the first source, as previously chosen.
- Crop uses the first image's oriented preview and applies the selected pixel rectangle
  to every image; an out-of-bounds rectangle is a reported per-image failure.
- One active engine job bounds native CPU/memory; no unlimited parallel tasks or idle
  service. A cancellation keeps published successes and stops the remaining work.
- Completed/total jobs drives progress; it does not imply byte/frame-accurate progress.
- Compact progress can be hidden/reopened from the tray and dismissed after completion.
- Preserve original hashes; never overwrite existing outputs; retain successful jobs
  when another job fails. Failure details are local UI only, not personal log data.

## Interactive testing availability

The Computer Use skill was initialized and `sky.list_apps()` attempted this turn.
It returned: `Computer Use native pipe is unavailable ... (os error 2)`.
No Explorer drag or native input test has therefore been performed. Automated tests
must not substitute for the user-required Phase 1 Explorer acceptance gate. Prepare a
reviewable installer and checklist, then obtain the real desktop result before moving
to Phase 2. This gate comes from the user's advanced-features request, not an inferred
approval policy.
