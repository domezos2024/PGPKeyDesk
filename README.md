<p align="center">
  <img src="1a.png" alt="PGPKeyDesk Logo" width="260">
</p>

# PGPKeyDesk

**English** | [Deutsch](README.de.md)

[![Release](https://img.shields.io/github/v/release/domezos2024/PGPKeyDesk)](../../releases/latest) [![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A lightweight Windows desktop application (WPF, VB.NET, .NET 10) for managing **OpenPGP key profiles** and for **encrypting and decrypting PGP messages** – no GnuPG installation required. Cryptography is provided by [PgpCore](https://github.com/mattosaurus/PgpCore) (BouncyCastle).

## Features

- **Manage profiles:** add, select and remove private/public key pairs as profiles
- **Generate key pairs:** RSA 2048 / 4096 bit with passphrase, export as `.asc`
- **Decrypt:** paste a PGP message or open it from a file, enter the passphrase, copy the plaintext
- **Encrypt:** paste or import the recipient's public key, encrypt and copy the text; if a profile is selected, the message is additionally **signed** with its private key (passphrase required)
- **Key validation** on import (format and presence of a master key)
- **Recipient keyring** with fingerprints and **key expiry warnings** (expired/revoked keys are blocked, keys expiring within 30 days are flagged)
- **File encryption/decryption**, **signing** and **signature verification** (messages and files; pick the sender key from the keyring)
- See [docs/SECURITY-AUDIT.md](docs/SECURITY-AUDIT.md) for the security review
- Dark theme; keyboard: `Ctrl+Enter` runs the action of the active tab

## Changes in 2.1.0

- Recipient keyring, key expiry warnings, file encryption, signature verification
- Security fixes (data loss when loading a damaged profile file, atomic saves, clipboard clearing)
- Unit tests, GitHub Actions (build/test/release), Windows installer

## Changes in 2.0.1

- Renamed from "OpenGPG" to **PGPKeyDesk** (profiles from the previous version are migrated automatically)
- Self-contained release for Windows x64 (no .NET installation required)
- MIT license, README, revised `.gitignore`

## Installation (for every user, no prerequisites)

1. On the [Releases](../../releases/latest) page, download **`PGPKeyDesk-<Version>-win-x64.zip`**.
2. Extract the ZIP (e.g. to `C:\Tools\PGPKeyDesk`).
3. Start `PGPKeyDesk.exe`.

The release is **self-contained**: it ships with the .NET runtime. Neither .NET, GnuPG nor any other software needs to be installed. Requirement: **Windows 10 (1809) or newer, 64-bit**.

The SHA-256 checksum is listed in the release notes and in `PGPKeyDesk-<Version>-win-x64.zip.sha256`:

```powershell
Get-FileHash .\PGPKeyDesk-<Version>-win-x64.zip -Algorithm SHA256
```

> Note: The EXE is not code-signed, so Windows SmartScreen may show a warning on first launch ("More info" → "Run anyway").

## Installer

Alternatively download **`PGPKeyDesk-<Version>-win-x64-setup.exe`** (Inno Setup, Windows 10 1809+ x64, English/German). It installs per user or per machine, creates a Start-menu shortcut (optional desktop shortcut), and offers an unchecked "Open with" registration for `.asc`/`.pgp`/`.gpg` (the app does not take a file argument yet). Uninstalling asks, default **No**, whether to delete `%AppData%\PGPKeyDesk`; upgrades keep your data. Silent install: `setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="desktopicon"`. Verify with the `.sha256` file. Details and build instructions: [installer/README.md](installer/README.md).

## Building from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
git clone https://github.com/domezos2024/PGPKeyDesk.git
cd PGPKeyDesk
dotnet run -c Release --project PGPKeyDesk.vbproj
```

Create a self-contained package (as in the release):

```powershell
dotnet publish PGPKeyDesk.vbproj -c Release -r win-x64 --self-contained true -o publish
```

## Security & data storage

- Profiles are stored in `%AppData%\PGPKeyDesk\profiles.json` (profiles from the earlier "OpenGPG" version in `%AppData%\OpenGPG` are migrated automatically) and encrypted with **Windows DPAPI** (`DataProtectionScope.CurrentUser`): readable only by the same Windows user on the same machine. For backups, use **Export** to save your keys.
- **Passphrases are never stored**; they exist in memory only for the duration of the respective operation.
- Private keys are exported in plain text (armored, still passphrase-protected) – keep them safe.
- The application makes no network connections and sends no telemetry.
- This is not an audited product. For highly critical use cases, please use established tools (e.g. GnuPG). Please report vulnerabilities privately via GitHub "Security → Report a vulnerability".

## Project structure

| Path | Contents |
|------|----------|
| `MainWindow.xaml(.vb)` | Main window: profiles, decrypt, encrypt |
| `GenerateKeyPairWindow.xaml(.vb)` | Key pair generator |
| `AddProfileWindow.xaml(.vb)` | Import a profile |
| `PassphraseDialog.xaml(.vb)` | Passphrase prompt |
| `Services/` | `ProfileStore` (DPAPI storage), `KeyValidator` |
| `Models/` | `PGPProfile` |
| `Themes/` | Colors and control styles |

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org). (Active once the project is approved by the foundation; see [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md).)

- **Committers, reviewers and approvers:** [Michael Bergfeld](https://github.com/domezos2024) (project owner)
- Only binaries built from this repository by the GitHub Actions release workflow are signed (`PGPKeyDesk.exe`, `PGPKeyDesk.dll`, installer). Third-party libraries are not re-signed.
- **Privacy:** this program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

## License

[MIT License](LICENSE) © 2026 Michael Bergfeld. Bundled third-party components ([PgpCore](https://github.com/mattosaurus/PgpCore), [BouncyCastle](https://www.bouncycastle.org/)) are under their own licenses (both MIT-compatible).
