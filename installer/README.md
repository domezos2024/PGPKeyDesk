# PGPKeyDesk installer (Inno Setup 6)

Builds `PGPKeyDesk-<Version>-win-x64-setup.exe` from the self-contained win-x64 publish output.

## Build

```powershell
choco install innosetup -y        # or: winget install JRSoftware.InnoSetup
.\installer\build-installer.ps1   # publishes, compiles, writes installer\Output\*.exe and *.sha256
```

Options: `-SkipPublish -PublishDir publish` (reuse an existing publish folder). The version is parsed from `<Version>` in `PGPKeyDesk.vbproj` and passed as `/DAppVersion=<x>` (fallback `0.0.0`). Manual: `ISCC.exe /DAppVersion=2.0.1 /DPublishDir=..\publish installer\PGPKeyDesk.iss`.

## Behaviour

- Windows 10 1809+ / 11, x64 only (`x64compatible`, MinVersion 10.0.17763). Per-user or per-machine (dialog); default `{autopf}\PGPKeyDesk`.
- English + German UI. LZMA2 compression. MIT license page.
- Start-menu shortcut; optional desktop shortcut (task `desktopicon`); "launch after install".
- Uninstall entry (version, publisher, URL), App Paths entry.
- Running instance is closed via Restart Manager (`CloseApplications=yes`, `RestartApplications=no`).
- Upgrade in place (same fixed AppId) never touches `%AppData%\PGPKeyDesk`. Files removed from a newer release are not cleaned up on upgrade (Inno default).
- Uninstall removes app files only. In interactive mode it asks whether to also delete `%AppData%\PGPKeyDesk` (default **No**; `profiles.json` holds DPAPI-encrypted private keys). Silent uninstall never deletes user data.

## File association (unchecked by default)

Task `fileassoc` registers ProgID `PGPKeyDesk.File` and adds it to `OpenWithProgids` of `.asc`, `.pgp`, `.gpg` ("Open with"). It does not change default handlers. Note: the app currently does not accept a file argument, so this entry only launches the app; hence it is off by default.

## Silent install / uninstall

```
PGPKeyDesk-<Version>-win-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="desktopicon"
PGPKeyDesk-<Version>-win-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR="C:\Temp\PGPKD"
"<app dir>\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

## Visual C++ runtime: conclusion

Not bundled and not required. The installer ships the self-contained .NET 10 runtime; its native components (coreclr, WPF's `wpfgfx_cor3`/`PresentationNative_cor3`) link against the app-local `vcruntime140_cor3.dll` that is part of the publish output (the `_cor3` suffix marks the private copy), and the UCRT is part of Windows 10. So no VC++ 2015-2022 redistributable (and no download) is needed; the `[Code]` section only does a defensive Windows-version check. This is based on documentation/knowledge, not on a test on a clean VM; the CI smoke test only checks install/uninstall, not app launch. If a user reports a missing `VCRUNTIME140.dll`, add the redistributable (Microsoft permits redistribution) as a prerequisite.

## Not code-signed

SmartScreen may warn on first launch ("More info" -> "Run anyway").
