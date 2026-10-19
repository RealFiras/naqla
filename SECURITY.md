# Security

Naqla processes files locally and does not upload content or download models at runtime.

The public beta's separate installer offers an explicit one-time FFmpeg/HEIF download
from pinned upstream URLs. It verifies SHA-256 before extraction and before activating
the staged installation. The tray application does not call this network code. The
official Microsoft Visual C++ runtime is a separate prerequisite, not an automatic
system installation. [Release details](docs/releases/v0.8.0-beta.1.md).

## Reporting a vulnerability

Please **do not** open a public issue for a security problem. Report it privately through
GitHub Security Advisories on the repository, or contact the maintainer through the
profile at [github.com/RealFiras](https://github.com/RealFiras).

Include the affected version, a minimal reproduction, and the expected impact. Do not
attach personal files, rename recovery records, or unredacted diagnostic logs.

## Scope and boundaries

The threat model, process limits, plugin isolation and retained limitations are documented
in [docs/SECURITY-REVIEW.md](docs/SECURITY-REVIEW.md) and
[docs/PLUGIN-THREAT-MODEL.md](docs/PLUGIN-THREAT-MODEL.md).

Development binaries are unsigned. Same-user malicious code can modify user-owned files;
hash checks are not an OS trust boundary. Review the limitations before redistribution.
