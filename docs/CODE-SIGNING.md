# Code signing (SignPath Foundation)

PGPKeyDesk is prepared for free Authenticode signing by the [SignPath Foundation](https://signpath.org/) (open-source programme). The release workflow signs `PGPKeyDesk.exe`, `PGPKeyDesk.dll` and the installer **only when the setup below is done**; without it, releases are published unsigned (Windows SmartScreen then warns).

> What cannot be automated: SignPath Foundation reviews every project by hand and issues the certificate to its own organisation. Only the project owner can apply and approve signing requests.

## One-time setup (project owner)

1. **Apply:** <https://signpath.org/apply> – project `domezos2024/PGPKeyDesk`, MIT licence, public repository. Requirements they check: OSI licence, no proprietary parts, builds on GitHub-hosted runners, a published code-signing policy (see README "Code signing policy" – already included), no malware/PUA.
2. After approval you receive a SignPath **organisation**. In it create:
   * **Project** with slug `PGPKeyDesk` (or set the repository variable `SIGNPATH_PROJECT_SLUG`).
   * **Artifact configurations** – paste the XML files from [`docs/signpath/`](signpath/): slug **`app`** (`app.xml`) and slug **`installer`** (`installer.xml`). Only the project's own binaries are signed, never third-party DLLs.
   * **Signing policy** with slug `release-signing` (or set `SIGNPATH_POLICY_SLUG`), certificate: *SignPath Foundation*, origin verification via the **GitHub.com trusted build system**, approval by the owner.
3. **GitHub connector:** in SignPath add GitHub.com as trusted build system and link the repository; create an API user and token (submitter role for the policy).
4. In this repository (*Settings → Secrets and variables → Actions*):
   * secret `SIGNPATH_API_TOKEN`
   * variable `SIGNPATH_ORGANIZATION_ID` (organisation GUID from the SignPath URL)
5. Publish a release (`Actions → Release → Run workflow`, or push a `v*` tag). The workflow submits both signing requests, waits for approval, **verifies the signature (`Get-AuthenticodeSignature` must be `Valid`)**, then continues with ZIP, installer, smoke test and release. If the secret/variable are missing it warns and releases unsigned.

## Notes

* Signing happens before the ZIP and installer are built, and the installer is signed separately; SHA-256 files are generated from the signed files.
* The signed installer carries the SignPath Foundation certificate; SmartScreen reputation starts to build with downloads, OV-class certificates may still show a warning for the first downloads.
* Verify a download: `Get-AuthenticodeSignature .\PGPKeyDesk-<Version>-win-x64-setup.exe | Format-List`.
