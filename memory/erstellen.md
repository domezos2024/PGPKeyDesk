# Erstellen: Projekt "OpenPGP Decryptor"  Implementierungs-Prompt

Ziel dieser Datei
- Eine präzise, sprach- und plattformneutrale Anleitung (Prompt-Format), die jede KI oder jeder Entwickler nutzen kann, um die vollständige App nachzubauen. Designfragen (Farben, Layout-Details) bleiben offen; Funktionalität, Datenmodell, API, Workflows, Fehlerbehandlung, Sicherheit und Cross-Platform-Alternativen sind vollständig beschrieben.

Kurzbeschreibung der App
- Desktop/Mobil-App zum Verwalten persönlicher OpenPGP-Profile (armored private/public keys) und zum Verschlüsseln/Entschlüsseln von Nachrichten.
- Lokale Speicherung von Profilen (JSON). Passphrasen werden niemals persistent gespeichert  nur im Arbeitsspeicher für eine Operation.

Must-have-Funktionen (funktionsgetrieben)
- Profile verwalten
  - Liste aller Profile anzeigen
  - Neues Profil hinzufügen (Name + Armored Private Key, optional Public Key)
  - Profil löschen
  - Profil exportieren / importieren (armored key oder JSON)
- Entschlüsseln
  - Benutzer kann einen PGP-armored String einfügen
  - Benutzer gibt die Passphrase ein
  - App verwendet den privaten Schlüssel des aktiven Profils + Passphrase, um zu entschlüsseln
  - Fehler werden freundlich übersetzt (siehe Fehler-Mapping)
- Verschlüsseln
  - Benutzer kann einen Public Key (armored) eines Empfängers einfügen und Klartext eingeben
  - App erzeugt PGP-armored Ciphertext
- Clipboard-Interaktionen (Kopieren / Einfügen)
- Status-/Fehlermeldungen und Ladeindikatoren

Datenmodell
- PGPProfile
  - id: string (UUID)
  - name: string
  - privateKey: string (armored ASCII-openpgp)
  - publicKey: string (armored ASCII-openpgp, optional)
  - createdAt: ISO8601 timestamp

Beispiel JSON (profiles.json)
{
  "profiles": [
    {
      "id": "b3f9f8e2-...",
      "name": "Meine Hauptsignatur",
      "privateKey": "-----BEGIN PGP PRIVATE KEY BLOCK-----\\n...",
      "publicKey": "-----BEGIN PGP PUBLIC KEY BLOCK-----\\n...",
      "createdAt": "2025-05-01T12:34:56Z"
    }
  ]
}

Speicherort & Persistenz
- Desktop: Standardpfad %AppData%/PGPKeyDesk/profiles.json (Windows) oder $XDG_CONFIG_HOME/PGPKeyDesk/profiles.json (Linux) bzw. ~/Library/Application Support/PGPKeyDesk/profiles.json (macOS).
- Mobil: sichere, private App-Daten (Sandbox). Auf iOS Keychain/secure file, Android EncryptedSharedPreferences / File with MasterKey.
- Anforderungen:
  - Verzeichnisse bei Bedarf anlegen
  - Atomare Schreibvorgänge (temp-file -> replace) um Datenkorruption zu vermeiden
  - Optional: Dateisperre/Mutex beim Schreiben

Wichtig: Standard-Implementierung speichert Profildaten unverschlüsselt. Empfohlenes Upgrade: Schutz durch OS-geschützte Speichermechanismen (DPAPI / Keychain / Android Keystore). Hinweise unten.

Kryptographie (Operationen)
- Primäre Operationen (Pseudocode-Signaturen):
  - decryptArmoredString(encryptedArmored: string, privateKeyArmored: string, passphrase: string) -> string
  - encryptArmoredString(plainText: string, recipientPublicKeyArmored: string) -> string

- Erwartungen:
  - Async / nicht-blockierend bei UI
  - Klare Fehlermeldungen/Exceptions weitergeben, aber intern maskierte Fehler für Nutzerfreundlichkeit (siehe Mapping)

Fehler-Mapping (für freundliche UI-Texte)
- bad pass / passphrase / password -> "Falsches Passwort."
- checksum mismatch -> "Fehler: Integrität oder Passwort falsch."
- no secret key -> "Kein passender privater Schlüssel gefunden."
- public key invalid -> "Ungültiger Public Key."
- network / io errors -> "Datei-/Zugriffsfehler." (mit optionaler Ursache)

UI-Komponenten und Interaktionen (plattformneutral)
- Hauptbildschirm  Aufteilung flexibel (z. B. Master/Detail):
  - Links: Profil-Liste mit Add/Delete/Select
  - Rechts oben: Aktives Profil anzeigen (Name, ggf. PublicKey-Actions)
  - Rechts Mitte: Entschlüsselungs-Pane
    - Textfeld: Armored Ciphertext (multiline)
    - PasswordField: Passphrase
    - Button: Entschlüsseln
    - Result-Anzeige: entschlüsselter Text (read-only)
    - Copy-Button
  - Rechts unten: Verschlüsselungs-Pane
    - Textfeld: Recipient Public Key (armored)
    - Textarea: Plaintext
    - Button: Verschlüsseln
    - Result-Anzeige: armored ciphertext

- Modal / Dialog: Profil hinzufügen
  - Name, PrivateKey (textarea, armored), optional PublicKey
  - Validate: non-empty name, private key contains PGP BEGIN/END
  - On Save: create PGPProfile and persist

Plattform- & Sprachalternativen (Empfohlene OpenPGP-Bibliotheken)
- .NET (WPF / WinForms / MAUI): PgpCore (BouncyCastle)  bereits vorhanden
- Java / Android: BouncyCastle (bcpg), openpgp-java
- JavaScript / Web / React Native: OpenPGP.js, openpgp
- Go: ProtonMail/gopenpgp
- Python: GnuPG via python-gnupg oder pgpy
- Swift / iOS: ObjectivePGP, SwiftOpenPGP implementations (oder Aufruf von gpg via CLI)

Cross-Platform-Implementierungsprinzipien
- Trenne UI- und Crypto-/Storage-Logik (MVVM / MVC / Presenter / Clean Architecture)
- Crypto-Service API: eine schmale, getestete Abstraktion für Encrypt/Decrypt
- ProfileStore API: LoadAll(), SaveAll(list), Add(profile), Remove(id), Export(profile)
- Alle IO-Operationen async/await oder entsprechende Futures/Promises

Sicherheitsanforderungen & Empfehlungen
- Niemals Passphrasen auf Disk speichern
- Minimieren der Zeit, in der PrivateKey und Passphrase im Speicher sind; clear byte-arrays nach Gebrauch wenn möglich
- Schütze profiles.json nach Möglichkeit mit OS-Mechanismen (DPAPI, Keychain, EncryptedSharedPreferences / MasterKey).
- Beim Export/Import nur im klar benannten Austauschformat (armored key oder JSON) und Benutzer bestätigen Klartext-Export

Tests, Validierung und CI
- Unit Tests:
  - ProfileStore: Load/Save/Atomicity/Corrupted file handling
  - CryptoService: encrypt->decrypt roundtrip, wrong passphrase, invalid keys
  - UI: Integrationstests für Add/Delete/Profile-Select flows
- E2E: automatischer Encrypt/Decrypt-Fluss mit Test-Keys
- CI: Build, Unit Tests, Linting, optionales Signing der Releases

Build & Run (als Beispiel: .NET WPF reference)
- Abhängigkeiten: .NET 10 SDK, PgpCore NuGet (6.5.0), WPF
- Projektstruktur (empfohlen):
  - src/
    - App (UI)
    - Libs/CryptoService (Encrypt/Decrypt Abstraktion)
    - Libs/Storage (ProfileStore)
    - Models
  - tests/
  - docs/
- Schritte:
  1. dotnet restore
  2. dotnet build
  3. dotnet run (oder pack & deploy)

Weitere Features / Optionen (nicht zwingend, optional)
- Secure profiles file encryption with a user-provided master passphrase (with caution)
- Import keys from file system (file picker) and from clipboard
- Key parsing: auto-extract public key from private key on import
- Multiple recipients support for encrypt
- File encryption/decryption (binary) in addition zum Text

Konkrete API-Spezifikation (als Blaupause)
- ProfileStore
  - Load() -> List<PGPProfile>
  - Save(list: List<PGPProfile>) -> void
  - Add(profile: PGPProfile) -> void
  - Remove(id: string) -> void

- CryptoService
  - decryptArmoredString(encrypted: string, privateKeyArmored: string, passphrase: string) -> string or error
  - encryptArmoredString(plain: string, recipientPublicKeyArmored: string) -> string or error

Akzeptanzkriterien (so muss es funktionieren)
1. App listet Profile und erlaubt Anlegen/Löschen.
2. Mit einem Profil kann ein gültiger PGP-armored Text entschlüsselt werden, wenn richtige Passphrase eingegeben wurde.
3. Text kann mit einem gültigen Empfänger-Public-Key verschlüsselt werden und ergibt einen Armored Ciphertext, der mit dem passenden PrivateKey entschlüsselt werden kann.
4. Passphrasen werden nicht persistiert.
5. Fehler erscheinen in benutzerfreundlicher Form (siehe Mapping).

Lokalisierung & Accessibility
- Textlabels und Fehler über Ressourcen/strings externalisieren (mehrsprachig)
- UI muss screen-reader freundlich sein, Fokus-Reihenfolge und ausreichende Farbkontraste bieten

Deployment
- Desktop: native installer (MSI, DMG), portable builds
- Mobile: Standard App-Store-Pipelines (Google Play, App Store)  beachte sichere Storage-APIs

Prompt-Verwendung (Wie eine KI dieses Dokument nutzen soll)
- Verwende diese Datei als Spezifikation. Implementiere:
  1. Ein schlankes CryptoService, das OpenPGP-Operationen kapselt (tests vorhanden)
  2. Ein Storage-Layer, der profiles.json atomar schreibt/liest
  3. Eine UI mit den genannten Komponenten, asynchronen Operationen, Ladeindikatoren und Statusmeldungen
  4. Automatisches Mapping technischer Fehler auf freundliche Nutzertexte
  5. Sicherheitsvorgaben: kein Persistieren von Passphrasen, Speicherhärtung empfohlenerweise via OS

Wenn du das Projekt in einer anderen Sprache/Plattform implementierst, halte dich an die APIs, das Datenmodell und die Akzeptanzkriterien; passe nur die Bibliothekswahl und UI-Frameworks an.

Ende
