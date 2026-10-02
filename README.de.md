<p align="center">
  <img src="1a.png" alt="PGPKeyDesk Logo" width="260">
</p>

# PGPKeyDesk

[English](README.md) | **Deutsch**

[![Release](https://img.shields.io/github/v/release/domezos2024/PGPKeyDesk)](../../releases/latest) [![Lizenz: MIT](https://img.shields.io/badge/Lizenz-MIT-green.svg)](LICENSE)

Schlanke Windows-Desktop-Anwendung (WPF, VB.NET, .NET 10) zum Verwalten von **OpenPGP-Schlüsselprofilen** sowie zum **Ver- und Entschlüsseln von PGP-Nachrichten** – ohne GnuPG-Installation. Die Kryptografie übernimmt [PgpCore](https://github.com/mattosaurus/PgpCore) (BouncyCastle).

> Die Programmoberfläche ist derzeit englisch, die Dokumentation deutsch.

## Funktionen

- **Profile verwalten:** Private-/Public-Key-Paare als Profile hinzufügen, auswählen und entfernen
- **Schlüsselpaar erzeugen:** RSA 2048 / 4096 Bit mit Passphrase, Export als `.asc`
- **Entschlüsseln:** PGP-Nachricht einfügen oder aus Datei öffnen, Passphrase eingeben, Klartext kopieren
- **Verschlüsseln:** Public Key des Empfängers einfügen oder importieren, Text verschlüsseln und kopieren; ist ein Profil gewählt, wird zusätzlich mit dessen privatem Schlüssel **signiert** (Passphrase erforderlich)
- **Schlüssel-Validierung** beim Import (Format und Vorhandensein eines Master-Keys)
- **Empfänger-Schlüsselbund** mit Fingerprints und **Ablaufwarnungen** (abgelaufene/widerrufene Schlüssel werden blockiert, Schlüssel mit Ablauf in 30 Tagen markiert)
- **Dateien ver-/entschlüsseln**, **signieren** und **Signaturen prüfen** (Nachrichten und Dateien; Absender-Schlüssel aus dem Schlüsselbund)
- Sicherheitsbewertung: [docs/SECURITY-AUDIT.md](docs/SECURITY-AUDIT.md) (englisch)
- Dunkles Design, Tastatur: `Strg+Enter` führt die Aktion des aktiven Tabs aus

## Änderungen in 2.1.0

- Empfänger-Schlüsselbund, Ablaufwarnungen, Datei-Verschlüsselung, Signaturprüfung
- Sicherheitsfixes (Datenverlust beim Laden defekter Profildatei, atomares Speichern, Zwischenablage-Löschung)
- Unit-Tests, GitHub-Actions (Build/Test/Release), Windows-Installer

## Änderungen in 2.0.1

- Umbenennung von „OpenGPG“ zu **PGPKeyDesk** (Profile aus der Vorgängerversion werden automatisch übernommen)
- Self-contained Release für Windows x64 (keine .NET-Installation nötig)
- MIT-Lizenz, README, überarbeitete `.gitignore`

## Installation (für jeden Nutzer, ohne Vorbedingungen)

1. Auf der Seite [Releases](../../releases/latest) die Datei **`PGPKeyDesk-<Version>-win-x64.zip`** herunterladen.
2. ZIP entpacken (z. B. nach `C:\Tools\PGPKeyDesk`).
3. `PGPKeyDesk.exe` starten.

Das Release ist **self-contained**: Es bringt die .NET-Laufzeit mit. Es müssen weder .NET, GnuPG noch sonstige Software installiert werden. Voraussetzung: **Windows 10 (1809) oder neuer, 64 Bit**.

Prüfsumme (SHA-256) steht in den Release-Notes bzw. in `PGPKeyDesk-<Version>-win-x64.zip.sha256`:

```powershell
Get-FileHash .\PGPKeyDesk-<Version>-win-x64.zip -Algorithm SHA256
```

> Hinweis: Die EXE ist nicht code-signiert. Windows SmartScreen kann daher beim ersten Start warnen („Weitere Informationen“ → „Trotzdem ausführen“).

## Installer

Alternativ die Datei **`PGPKeyDesk-<Version>-win-x64-setup.exe`** herunterladen (Inno Setup, Windows 10 1809+ x64, Deutsch/Englisch). Installation pro Benutzer oder für alle Benutzer, Startmenü-Verknüpfung (optional Desktop-Verknüpfung) und eine standardmäßig deaktivierte „Öffnen mit“-Registrierung für `.asc`/`.pgp`/`.gpg` (die App akzeptiert noch kein Datei-Argument). Beim Deinstallieren wird gefragt (Standard **Nein**), ob `%AppData%\PGPKeyDesk` gelöscht werden soll; Updates behalten Ihre Daten. Stille Installation: `setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="desktopicon"`. Prüfsumme in der `.sha256`-Datei. Details und Build-Anleitung: [installer/README.md](installer/README.md).

## Aus dem Quellcode bauen

Voraussetzungen: [.NET 10 SDK](https://dotnet.microsoft.com/download) unter Windows.

```powershell
git clone https://github.com/domezos2024/PGPKeyDesk.git
cd PGPKeyDesk
dotnet run -c Release --project PGPKeyDesk.vbproj
```

Eigenständiges Paket erzeugen (wie im Release):

```powershell
dotnet publish PGPKeyDesk.vbproj -c Release -r win-x64 --self-contained true -o publish
```

## Sicherheit & Datenspeicherung

- Profile liegen in `%AppData%\PGPKeyDesk\profiles.json` (Profile der früheren Version „OpenGPG“ aus `%AppData%\OpenGPG` werden automatisch übernommen) und sind mit **Windows DPAPI** (`DataProtectionScope.CurrentUser`) verschlüsselt: lesbar nur für denselben Windows-Benutzer auf demselben Rechner. Für ein Backup die Schlüssel daher über **Export** sichern.
- **Passphrasen werden nie gespeichert**, sie existieren nur während der jeweiligen Operation im Speicher.
- Der Export privater Schlüssel erfolgt im Klartext (armored, weiterhin passphrase-geschützt) – sicher aufbewahren.
- Die Anwendung baut keine Netzwerkverbindungen auf und sendet keine Telemetrie.
- Dies ist kein auditiertes Produkt. Für hochkritische Anwendungsfälle bitte etablierte Werkzeuge (z. B. GnuPG) verwenden. Sicherheitslücken bitte vertraulich über GitHub „Security → Report a vulnerability“ melden.

## Projektstruktur

| Pfad | Inhalt |
|------|--------|
| `MainWindow.xaml(.vb)` | Hauptfenster: Profile, Entschlüsseln, Verschlüsseln |
| `GenerateKeyPairWindow.xaml(.vb)` | Schlüsselpaar-Generator |
| `AddProfileWindow.xaml(.vb)` | Profil importieren |
| `PassphraseDialog.xaml(.vb)` | Passphrase-Abfrage |
| `Services/` | `ProfileStore` (DPAPI-Speicher), `KeyValidator` |
| `Models/` | `PGPProfile` |
| `Themes/` | Farben und Control-Styles |

## Lizenz

[MIT License](LICENSE) © 2026 Michael Bergfeld. Enthaltene Drittkomponenten ([PgpCore](https://github.com/mattosaurus/PgpCore), [BouncyCastle](https://www.bouncycastle.org/)) stehen unter ihren eigenen Lizenzen (beide MIT-kompatibel).
