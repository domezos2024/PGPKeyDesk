# PGPKeyDesk – Signatur-Fahrplan (kostenlos, Open Source, MIT)

Ziel: `PGPKeyDesk.exe`, `PGPKeyDesk.dll` und der Installer sind **Authenticode-signiert**, ohne dass Geld ausgegeben wird. Dann zeigt Windows SmartScreen nicht mehr „Unbekannter Herausgeber“.

**Stand:** Die technische Seite ist fertig (Workflow, Konfigurationen, Richtlinie im README). Es fehlen nur Schritte, die ausschließlich **du als Repository-Inhaber** erledigen kannst.

---

## 1. Der einzige kostenlose Weg: SignPath Foundation

Die [SignPath Foundation](https://signpath.org/) signiert Open-Source-Projekte kostenlos mit einem eigenen Zertifikat (der Herausgeber heißt dann „SignPath Foundation“, nicht dein Name).

### Bedingungen (laut deren Regeln) und unser Stand

| Bedingung | Stand |
|-----------|-------|
| OSI-Lizenz (MIT) | erfüllt (`LICENSE`) |
| Öffentliches Repository, Build auf GitHub-Servern | erfüllt (GitHub Actions, `windows-latest`) |
| Keine Malware / unerwünschte Programme | erfüllt (keine Netzwerkzugriffe, kein Tracking) |
| Kein proprietärer Code | erfüllt (PgpCore, BouncyCastle: MIT-kompatibel) |
| Gepflegt und veröffentlicht | erfüllt (Release v2.1.0) |
| Dokumentiert (Download-Seite, README) | erfüllt (README EN/DE) |
| Veröffentlichte Code-Signing-Richtlinie | erfüllt (Abschnitt „Code signing policy“ im README) |
| **„Nachprüfbare Reputation“** | **offen – der kritische Punkt**, siehe Abschnitt 2 |

Die Prüfung erfolgt von Hand, die Regeln sind laut SignPath „soft and fuzzy“. Eine feste Zahl (Sterne, Downloads) gibt es nicht, aber ein Projekt, das man per Suchmaschine nicht findet, wird mit hoher Wahrscheinlichkeit abgelehnt.

---

## 2. Sichtbarkeit aufbauen (damit Google das Projekt findet)

Das Antragsformular verlangt einen Projektnamen, bei dem eine Google-Suche **eindeutig auf dein Projekt** führt. Der Name `PGPKeyDesk` ist einzigartig – er muss nur noch auffindbar werden.

### 2.1 Sofort erledigen (ca. 30 Minuten, nur du kannst das)

- [ ] **GitHub-Repository-Seite** (Zahnrad neben „About“): Beschreibung setzen, z. B. *„Free OpenPGP desktop app for Windows: manage keys, encrypt, decrypt, sign and verify messages and files – no GnuPG needed.“*
- [ ] **Topics** setzen: `pgp`, `openpgp`, `encryption`, `gpg`, `wpf`, `windows`, `dotnet`, `vb-net`, `privacy`, `desktop-app`
- [ ] **Website-Feld** im Repository: Link auf die Releases-Seite oder eine Projektseite (2.3)
- [ ] **Social preview** (Settings → General → Social preview): Bild hochladen (`1a.png` ist als Logo vorhanden)
- [ ] Repository-Name prüfen: Alte Bezeichnung „OpenGPG“ kommt noch in Dateipfaden/Commits vor. Überall einheitlich **PGPKeyDesk** verwenden.
- [ ] **Release-Seite** v2.1.0: einen kurzen Absatz „Was ist PGPKeyDesk?“ in die Beschreibung schreiben und 1–2 Screenshots anhängen.
- [ ] **Screenshots** im README ergänzen (Hauptfenster, Keyring, Datei-Tab). Ohne Bilder wirkt das Projekt unfertig.

### 2.2 Verzeichnisse und Paketquellen (Reputation + Backlinks)

- [ ] **winget:** Manifest bei [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) einreichen (`wingetcreate`). Erzeugt eine eigene Seite und nutzt den Projektnamen.
- [ ] **Chocolatey** oder **Scoop**: Paket einreichen (Scoop-Bucket ist einfach selbst zu betreiben).
- [ ] **AlternativeTo.net:** Eintrag als Alternative zu Kleopatra / Gpg4win / GnuPG anlegen.
- [ ] **SourceForge** oder **Softpedia** Eintrag (optional, bringt Backlinks).
- [ ] **Awesome-Listen** auf GitHub (z. B. „awesome-dotnet“, „awesome-privacy“): Pull Request mit einem Eintrag.

### 2.3 Eigene Seite und Auffindbarkeit

- [ ] **GitHub Pages** aktivieren (Settings → Pages) mit einer einfachen Startseite (Titel „PGPKeyDesk – OpenPGP for Windows“, Beschreibung, Download-Link, Screenshots). Ich kann die Seite für dich erstellen (siehe Abschnitt 5).
- [ ] **Google Search Console** (https://search.google.com/search-console): die Seite/das Repository als Property hinzufügen und die URL zur Indexierung einreichen. Das beschleunigt das Auffinden deutlich.
- [ ] Prüfen: Google-Suche nach `"PGPKeyDesk"` und `PGPKeyDesk github` – das Repository sollte unter den ersten Treffern stehen.

### 2.4 Bekannt machen (je mehr unabhängige Erwähnungen, desto besser)

- [ ] Beitrag auf Reddit (`r/PGP`, `r/opensource`, `r/dotnet`, `r/privacy`) – regelkonform, kein Spam.
- [ ] „Show HN“ auf Hacker News.
- [ ] Artikel auf dev.to oder in einem eigenen Blog („Wie ich einen GnuPG-freien PGP-Client in VB.NET gebaut habe“). Der Artikel verlinkt auf das Repository.
- [ ] Deutsche Foren/Communities (z. B. administrator.de, Computerbase-Forum, heise-Foren), sofern thematisch passend.
- [ ] Optional: Sterne und Beobachter organisch sammeln (Freunde/Kollegen bitten, das Projekt anzusehen). Keine gekauften Sterne – das schadet bei der Prüfung.

**Realistischer Zeitplan:** 2–4 Wochen aktive Sichtbarkeit, bevor der Antrag sinnvoll ist. Mehr Stabilität und Pflege (weitere Releases, Issues beantworten) zählt ebenfalls als „Maintained“.

---

## 3. Antrag bei der SignPath Foundation stellen

Erst wenn Abschnitt 2 zumindest teilweise erledigt ist:

1. https://signpath.org/apply öffnen und das Formular ausfüllen:
   - **Project Name:** `PGPKeyDesk`
   - **Repository:** https://github.com/domezos2024/PGPKeyDesk
   - **Lizenz:** MIT
   - **Beschreibung:** *Free OpenPGP desktop app for Windows to manage keys and to encrypt, decrypt, sign and verify messages and files.*
   - **Download-/Projektseite:** Link auf die Releases-Seite oder GitHub Pages
   - **Build:** GitHub Actions, GitHub-gehostete Runner (`.github/workflows/release.yml`)
   - **Code-Signing-Richtlinie:** README-Abschnitt „Code signing policy“
   - Verweise auf Winget/Chocolatey/AlternativeTo/Blogbeitrag als Nachweis der Reputation.
2. Antwort abwarten (Tage bis Wochen). Bei Rückfragen sachlich und konkret antworten.

---

## 4. Nach der Freigabe: Einrichtung (ca. 30 Minuten)

Detailliert in [`CODE-SIGNING.md`](CODE-SIGNING.md). Kurzfassung:

1. In SignPath ein **Projekt** `PGPKeyDesk` anlegen.
2. **Artifact-Konfigurationen** anlegen: `app` ← Inhalt von [`signpath/app.xml`](signpath/app.xml), `installer` ← Inhalt von [`signpath/installer.xml`](signpath/installer.xml).
3. **Signing Policy** `release-signing` (Zertifikat: SignPath Foundation, Herkunftsprüfung über den **GitHub.com Trusted Build System**-Connector, Freigabe durch dich).
4. API-Benutzer und **API-Token** erzeugen (Rolle: Submitter für die Policy).
5. In GitHub (Settings → Secrets and variables → Actions):
   - Secret `SIGNPATH_API_TOKEN`
   - Variable `SIGNPATH_ORGANIZATION_ID` (GUID aus der SignPath-URL)
   - optional `SIGNPATH_PROJECT_SLUG` / `SIGNPATH_POLICY_SLUG`, falls du andere Namen gewählt hast
6. **Neuen Release starten:** Actions → Release → Run workflow (Version angeben; vorher `<Version>` in `PGPKeyDesk.vbproj` erhöhen, z. B. 2.1.1). Das ist der erste echte Test der Signatur.
7. Prüfen: `Get-AuthenticodeSignature .\PGPKeyDesk-<Version>-win-x64-setup.exe | Format-List` – Status `Valid`.

Hinweis: Auch signierte Dateien können bei den ersten Downloads noch eine SmartScreen-Meldung auslösen, weil sich der Ruf des Zertifikats erst aufbaut. Der Herausgeber wird aber angezeigt.

---

## 5. Wobei ich dir helfen kann – und wobei nicht

### Ich kann (sag einfach Bescheid)

- GitHub-Pages-Startseite für das Projekt erstellen und veröffentlichen
- README verbessern (Screenshots einbinden, Badges, Beschreibungstexte, Schlagwörter für die Suche)
- winget-Manifest, Scoop-Manifest und Chocolatey-Paket vorbereiten
- Texte entwerfen: Antrag an SignPath, Reddit-/Hacker-News-/dev.to-Beiträge, AlternativeTo-Eintrag
- Repository-Beschreibung und Topics per GitHub-Schnittstelle setzen (falls meine Rechte dafür reichen, sonst gebe ich dir die Texte)
- Release-Workflow anpassen, falls die Signatur beim ersten Lauf Fehler zeigt (Logs lesen, korrigieren, neu starten)
- Auf **Azure Trusted Signing** (ca. 10 $/Monat) umbauen, falls SignPath ablehnt
- Neue Version bauen und veröffentlichen, sobald die Secrets gesetzt sind

### Nur du kannst

- Den Antrag bei SignPath absenden und mit deinen Daten bestätigen
- SignPath-Konto, Projekt, Policy und API-Token anlegen
- Secrets/Variablen in den GitHub-Einstellungen setzen
- Öffentliche Beiträge unter deinem Namen veröffentlichen, Verzeichnis-Konten anlegen
- Die Google Search Console verifizieren
- Das Programm auf einem sauberen Windows-Rechner testen (Installation, Oberfläche, Verknüpfungen)

---

## 6. Falls SignPath ablehnt oder zu lange dauert

| Option | Kosten | Entfernt die Warnung? |
|--------|--------|------------------------|
| Azure Trusted Signing | ca. 10 $/Monat | ja, mit Identitätsprüfung, Ruf baut sich schnell auf |
| Kommerzielles OV-Zertifikat | ca. 100–400 €/Jahr | zunächst teils noch Warnung |
| EV-Zertifikat | ca. 250–500 €/Jahr | ja, sofort |
| Selbstsigniertes Zertifikat | kostenlos | **nein** (nur nützlich für eigene Rechner) |
| Unsigniert lassen und „Trotzdem ausführen“ | kostenlos | nein (Download-Hinweis im README ist vorhanden) |

Kostenlos und wirksam gibt es nur SignPath Foundation.

---

## 7. Abhak-Liste

- [ ] Repository-Beschreibung, Topics, Website, Social preview
- [ ] Screenshots im README und auf der Release-Seite
- [ ] GitHub Pages Startseite
- [ ] Google Search Console eingerichtet, Indexierung beantragt
- [ ] winget / Scoop / Chocolatey eingereicht
- [ ] AlternativeTo-Eintrag
- [ ] Mindestens 2 öffentliche Beiträge (Reddit/HN/dev.to/Blog)
- [ ] Google-Suche `PGPKeyDesk` findet das Projekt
- [ ] Antrag bei SignPath Foundation gestellt
- [ ] SignPath: Projekt, Artifact-Konfigurationen `app` und `installer`, Policy `release-signing`
- [ ] GitHub: Secret `SIGNPATH_API_TOKEN`, Variable `SIGNPATH_ORGANIZATION_ID`
- [ ] Neuer Release gestartet, Signatur mit `Get-AuthenticodeSignature` geprüft (`Valid`)
- [ ] Installer auf einem sauberen Windows-Rechner getestet
