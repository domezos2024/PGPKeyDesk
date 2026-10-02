---
name: Project PGPKeyDesk
description: WPF/.NET 10 VB.NET desktop app for OpenPGP profile management and message decryption
type: project
---


Desktop WPF application to manage OpenPGP private key profiles and to encrypt/decrypt PGP messages.

Why
- Designed for a single-user desktop scenario: the user stores their private keys (armored) locally and uses the app to decrypt incoming messages or to encrypt messages for recipients.

How it works
- Target: .NET 10 (net10.0-windows), language: VB.NET, UI: WPF.
- Uses PgpCore (v6.5.0) which wraps BouncyCastle for OpenPGP operations.
- Profiles are stored as JSON at %AppData%\PGPKeyDesk\profiles.json. Each profile contains a profile Id, name, armored private key and optional public key. Passphrases are not persisted — the user enters them when decrypting.
- Decryption uses PgpCore.PGP.DecryptArmoredStringAsync(privateKey, passphrase) and encryption uses PgpCore.PGP.EncryptArmoredStringAsync.
- UI features: profile list (add / delete), display active profile, decrypt pane (paste armored message + passphrase), encrypt pane (paste recipient public key + plaintext), copy results to clipboard, simple friendly error messages and status indicators. Visual theme is a Catppuccin-like dark theme.

Key files / locations
- Models/PGPProfile.vb — PGPProfile model (Id, Name, PrivateKey, PublicKey, CreatedAt)
- Services/ProfileStore.vb — load/save profiles to %AppData%\PGPKeyDesk\profiles.json using System.Text.Json
- MainWindow.xaml & MainWindow.xaml.vb — main application UI and core encrypt/decrypt flows, status handling and clipboard actions
- AddProfileWindow.xaml & AddProfileWindow.xaml.vb — dialog to add/import a new profile
- Application.xaml(.vb) and AssemblyInfo.vb — WPF application entry and assembly metadata
- PGPKeyDesk.vbproj — project file (references PgpCore)

Notes / next steps
- Profiles are stored unencrypted on disk. If stronger security is needed, consider encrypting the profiles file with DPAPI or protecting private keys individually.
- There is no automatic key parsing/validation when adding profiles beyond what PgpCore will throw at runtime; consider validating key format earlier and surfacing clear errors.
- No network or remote components — this is strictly local.

