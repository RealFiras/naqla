# Plugin threat model — current constrained API 1

Implemented lifecycle/validation/fixed-code hosting is documented in [PLUGINS.md](PLUGINS.md).
Two working local examples, consent, fingerprint invalidation, worker failure/restart,
timeout/lifetime, cancellation and malicious-package tests are recorded in the final report.
This is process separation, not an OS sandbox. General executable community plugins are unsupported.

## Historical pre-implementation design
# Plugin threat model — design draft, not an implemented security boundary

The plugin feature is eighth in the requested order and is not enabled in this build.
This review records requirements before its implementation; no plugin manager/host
or manifest is currently presented as functional.

## Assets and adversaries

Protect source files, unrelated user data, application integrity, responsiveness and
offline behavior from malicious packages/manifests, crafted paths/links, hostile input
files, excessive resource requests and compromised third-party/native dependencies.
Assume packages and declared author/update URLs are untrusted. A checksum identifies
bytes, not a publisher. An ordinary process running as the user is not a sandbox.

## Proposed first version

- Admit constrained declarative operations implemented by trusted, fixed host code.
  Do not load package DLLs, native executables, scripts, installer hooks, dynamic code
  or arbitrary command lines. A registry label alone does not make an operation safe.
- Run operations in a separate host with a versioned, bounded protocol. Give it only
  explicitly selected input streams/handles and owned work outputs, not caller-chosen
  arbitrary filesystem commands. Main app validates returned types and publishes output.
- Reject undeclared/unknown capabilities and network operations in the constrained API.
  Host-controlled code makes no network requests. This is API enforcement, **not a
  claim of OS-level network denial or full native-code sandboxing**.
- Use process timeouts/cancellation and verified Job Object resource/process cleanup
  where supported. Job management does not by itself restrict all filesystem/network
  access. Test host crash/restart without affecting the tray process.
- Dedicated per-user plugin directory only; discover metadata without execution.
  Require explicit enable consent, tie it to package content/version, and invalidate
  approval on change. Disabled/incompatible packages register no wheel actions.
- Bound package bytes, entries, expanded bytes, paths and asset dimensions; reject
  traversal/absolute/device paths, links, duplicates and executable entry points.
  Validate in a private directory before atomic installation. Uninstall only owned
  package data, never selected files. No automatic downloads or install scripts.
- Sample SHA-256 and a genuinely safe conversion example. Use SVG only if external
  entities/resources can be disabled and adversarially verified; otherwise ship the
  explicitly permitted small working alternative, with documented support limits.

## Residual risks and release gates

Trusted host/codec vulnerabilities still matter. A separate normal-user process has
the user's OS rights if its trusted code is compromised. Arbitrary native-code plugins
stay out of v1 unless an independently tested OS isolation design is added. Do not
infer isolation from JSON permissions, AssemblyLoadContext or a job object.

Verify manifest/API compatibility, disabled/change approval behavior, safe archive
installation/uninstall, traversal/link/bomb limits, bounded IPC/output validation,
cancel/timeout/crash/restart, simultaneous jobs and no host-controlled network calls.
If an AppContainer-based design is later chosen, test actual denied file/socket access
on Windows 10 and 11 and document exactly which capabilities are granted.

Primary references informing this design:

- [.NET AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext): dependency loading contexts do not provide binary isolation.
- [Windows Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects): process groups, limits and accounting.
- [Windows application isolation](https://learn.microsoft.com/en-gb/windows/security/book/application-security-application-isolation): AppContainer provides OS-enforced process/resource boundaries; the referenced Win32 isolation discussion applies to Windows 11, not blanket validation of this Windows 10/11 app.
