# Security policy

PGPKeyDesk handles private PGP keys, so security reports are taken seriously.

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Use GitHub's private reporting instead:
[Report a vulnerability](https://github.com/domezos2024/OpenGPG/security/advisories/new).

Include the affected version, steps to reproduce and the expected impact. You will get an answer as soon as possible; this is a volunteer-maintained project, so there is no fixed SLA.

## Supported versions

Only the latest release receives security fixes.

## Scope and known limits

An internal review is documented in [docs/SECURITY-AUDIT.md](docs/SECURITY-AUDIT.md). It is **not** an independent audit. Notably, DPAPI protects the profile store against other Windows users, not against code running as the same user, and passphrases are .NET strings that cannot be zeroed.
