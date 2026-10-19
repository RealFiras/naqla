# Local development security review

Scope: source review plus the real acceptance suites listed in END-TO-END-TEST-REPORT.
This is not penetration-test certification or an OS sandbox claim.

| Surface | Enforced behavior | Evidence / residual risk |
|---|---|---|
| File identity | Actual CF_HDROP re-read at drop; hooks never supply filenames | Physical Explorer delivery remains manual |
| Originals | Read locks, source hashes, new sibling outputs and atomic collision naming | Confirmed Smart Rename is the explicit actual-rename exception; identity/rollback/undo tests |
| Outputs/temp | Owned GUID files/folders, no overwrite, cleanup scoped to owned entries | Same-user concurrent filesystem attacks are not an OS isolation boundary |
| Processes | ArgumentList, shell disabled, hidden below-normal workers, output bounds, timeouts, kill-on-close jobs | 1.5 GB/process and four processes/job; no filesystem/network denial |
| Images/models | 40 MP, fixed decoder limits, trusted model hashes, bounded CPU inference, cancellation | Native parser vulnerabilities/quality require maintained upstream dependencies |
| PDF | Bounded pages/sources; refuse encrypted/signed/interactive/layered cases; independent parse/render | Visible or hidden personal content is not redacted by metadata cleanup |
| Documents | Bounded ZIP/XML/HTML; DTD/resolvers off, active/external content refused; isolated LO profile; macros/updates/links off | No OS network barrier; disconnected acceptance and hostile corpus expansion pending |
| Archives | Traversal/UNC/ADS/device/collision checks; links/hardlinks/special/encrypted/split entries refused; expansion limits | ZIP/7Z/TAR real malicious fixtures exercised; conservative limits reject some valid archives |
| Plugins | Strict version/capabilities/schema, two-entry bounded declarative package, disabled-by-default hash-bound consent | No arbitrary package code; fixed-code separate worker, not community-code sandbox |
| Components | Explicit local ZIP only; exact build-pinned length/SHA/closure; modified files not overwritten/deleted | SHA identifies bytes, not a publisher; runtime verification cache invalidated by filesystem watcher |
| Diagnostics | Closed enums/numeric fields; no free strings, filenames, paths, exception bodies or file content | Screenshots/result UI can contain private paths: crop/redact before sharing |
| Installer | Per-user/no admin/service; fresh staging, path/reparse checks, owned-file hashes, preserve unowned/modified data | Actual clean-account registry upgrade/uninstall and injected rollback need manual acceptance |

NuGet vulnerability check is recorded in `artifacts/nuget-vulnerability-audit.json`.
An older AngleSharp release failed NU1902 and was upgraded to pinned 1.8.5; the
warning was not suppressed. An empty advisory result is a point-in-time NuGet
check, not proof that bundled native codecs or every parser are vulnerability-free.

Default plugin/model behavior contains no downloader, HTTP client, telemetry or
cloud processing. Fetch scripts are developer-only and are not bundled for runtime
use. No host networking was disabled to fabricate offline acceptance. Actual
disconnected testing is listed explicitly in MANUAL-WINDOWS-TESTS.

Before public release: code signing, independent hostile-corpus/long-session review,
native-engine advisory review, clean-PC tests and complete redistribution/source
compliance review remain gates. No remote publication was performed.
