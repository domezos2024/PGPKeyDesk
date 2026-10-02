# Security audit – PGPKeyDesk 2.1.0

Scope: key storage, passphrase handling, dependencies, file handling. Method: code review of all `*.vb`, empirical tests of PgpCore behaviour, `dotnet list package --vulnerable --include-transitive`. This is an internal review, **not** an independent audit.

## Findings

| # | Severity | Finding | Status |
|---|----------|---------|--------|
| 1 | High | **Silent data loss:** `ProfileStore.Load` swallowed every error and returned an empty list; the next `Save` then overwrote the (merely unreadable) store, destroying all private keys. | **Fixed.** The unreadable file is moved to `profiles.json.unreadable-<timestamp>`, the user is warned, nothing is deleted. Covered by tests. |
| 2 | Medium | Non-atomic write of `profiles.json`: a crash or power loss mid-write could truncate the store. | **Fixed.** Write to `.tmp`, then atomic replace. |
| 3 | Medium | **Encrypting to expired or revoked keys** was silently possible (PgpCore does not check). | **Fixed.** `PgpService.CheckRecipientKey` blocks expired/revoked/no-encryption-key, warns 30 days before expiry. |
| 4 | Medium | Decrypted plaintext stayed in the clipboard indefinitely. | **Fixed.** Cleared after 60 s if unchanged. |
| 5 | Low | Passphrases remained in the `PasswordBox` after use. | **Fixed** for decrypt/encrypt/file operations (cleared after success / always for files). |
| 6 | Low | Recipient key import had no size limit (memory/CPU with huge files). | **Fixed.** 1 MB limit for key files, 200 MB for file encryption. |
| 7 | Low | `KeyValidator` threw `NullReferenceException` for `Nothing` input. | **Fixed**, covered by test. |
| 8 | Info | Passphrases are .NET `String`s (immutable, cannot be zeroed) and are handed to BouncyCastle as `char[]` copies. They live until garbage collection. | **Accepted** – inherent to WPF `PasswordBox` + PgpCore; mitigated by short lifetime and clearing the UI field. |
| 9 | Info | Legacy `%AppData%\OpenGPG\profiles.json` (≤ 1.0) may be plaintext and is left in place after migration (private keys in it are still passphrase-protected). | **Accepted / documented**; delete it manually after verifying the migration. |
| 10 | Info | Exported private keys are plain armored files (passphrase-protected key material). Export requires re-entering the passphrase and a warning dialog. | Accepted, by design. |
| 11 | Info | Signature verification: a *signed message from an unknown sender* is reported as "not checked", never as valid. A wrong sender key reports **invalid**. | By design; covered by tests. |
| 12 | Info | The EXE and installer are not code-signed (SmartScreen warning). | Open (needs a certificate). |
| 13 | Info | PgpCore upgraded 6.5.0 → 8.0.0 (all 59 tests pass; API unchanged for our usage). | Done. |

## Dependencies

`dotnet list package --vulnerable --include-transitive`: **no known vulnerabilities** (PgpCore 8.0.0). CI fails the build if a vulnerable package appears, and Dependabot proposes NuGet and GitHub Actions updates weekly.

## Storage model

* `%AppData%\PGPKeyDesk\profiles.json`: private keys, DPAPI (`CurrentUser`) encrypted. Not portable between users/machines – use *Export* for backups.
* `%AppData%\PGPKeyDesk\keyring.json`: recipients' **public** keys, plain JSON (not secret), atomic writes.
* No passphrase is ever persisted. No network access, no telemetry.

## Not covered

Side channels, memory forensics, a compromised Windows account (DPAPI protects against other users, not against code running as the same user), and the cryptographic implementation of BouncyCastle itself.
