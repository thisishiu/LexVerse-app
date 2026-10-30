# LexVerse Windows distribution

## MSI for GitHub Releases

From `release/msi`, run on Windows with .NET 10 SDK and Node.js/npm:

```powershell
.\scripts\package-msi.ps1 -Version 1.0.0
```

The script installs WiX 6.0.2 locally if needed and produces
`artifacts/msi/1.0.0/win-x64/LexVerse-1.0.0-x64.msi` and `.msi.sha256`.
Upload these two files as GitHub Release assets, not the publish directory.
The installer targets Windows 10 build 19041+ x64, installs per-machine
(administrator permission required), adds a Start menu shortcut, and supports
major upgrades while blocking downgrades. Use increasing three-part MSI versions.
The .NET runtime is included; Microsoft Edge WebView2 Evergreen Runtime must
already be installed. MSIX installations remain separate from MSI installations.
MSI updates are manual, not managed by App Installer.

The output is **unsigned**; Windows may show an unknown-publisher warning.
Sign with a trusted code-signing certificate before public distribution and
regenerate the SHA-256 checksum after signing. Test installation, launch,
upgrade, repair and uninstall on a clean Windows VM before publishing.
Only `packaging/client.env` is included; root developer dotenv files are not used.

## Microsoft Store submission

For Microsoft Store releases, use the identity registered in Partner Center and saved in `packaging/store-identity.json`. Do not substitute the direct-distribution identity or a local signing certificate.

```powershell
.\scripts\package-msix.ps1 -Version 1.0.0.0 -Store
```

This produces an unsigned, self-contained x64 MSIX and SHA-256 checksum under `artifacts/store/stable/1.0.0.0/win-x64/`. Upload the MSIX in the app's Partner Center submission. Microsoft signs and distributes the accepted package and manages updates; no hosting URL, PFX, or `.appinstaller` is needed. The fourth version component must be zero. Use Store package flights for testing rather than the separate direct-distribution Beta identity.

The signed release candidate workflow below is for direct distribution, not Store submission. Before submitting, run the Release build and tests, check the package with the Windows App Certification Kit, and complete the Store listing, privacy policy, age ratings, and certification notes (including the reason for `runFullTrust`). An unsigned submission artifact is not a locally installable test package; use a separately signed test copy or Store flights to test installation.

## Direct distribution

LexVerse uses one self-contained MSIX package per release channel. Windows App Installer owns installation, integrity verification, background/on-launch updates, and repair. Stable and Beta use separate package identities, so a tester can keep both installed without Beta replacing Stable.

## Build a package

The desktop project copies `packaging/client.env` to `.env` beside the executable
in both build and publish output. This file contains public production endpoints,
the desktop OAuth client ID, and the entitlement verification public key. Keep it
in portable ZIP distributions as well. Never add credentials, private keys, or
developer localhost settings to this file. The repository-root `.env` and
`.env.local` are not distribution inputs.

Run from the repository root on Windows with the .NET SDK and Windows SDK packaging tools installed:

Set `LEXVERSE_PLANS_URL` in `packaging/client.env` to the official public HTTPS
plans page before packaging. For local development, set the same environment
variable through the existing dotenv configuration. Example (replace the domain
and path with the real page):

```dotenv
LEXVERSE_PLANS_URL=https://example.com/plans
```

Leave it empty to disable “View plans on the web”. Invalid URLs, non-HTTPS URLs,
and URLs containing embedded credentials also disable the button. The desktop
opens this exact public URL in the default browser without adding account data
or tokens; sign-in and purchases happen on the website. After paying, return to
the account page and select “Sync access”.

```powershell
.\scripts\package-msix.ps1 `
  -Version 1.0.0.0 `
  -Channel stable `
  -ReleaseBaseUri https://downloads.example.com/lexverse `
  -Publisher 'CN=Your verified certificate subject' `
  -PublisherDisplayName 'Your company' `
  -CertificateThumbprint 'CERTIFICATE_SHA1_THUMBPRINT'
```

The certificate subject must exactly match the `Publisher` value. The script signs with SHA-256, timestamps the signature, verifies it, and writes the MSIX, `.appinstaller`, and SHA-256 checksum under `artifacts/release/<channel>/<version>/win-x64/`.

For a local or CI packaging smoke test only:

```powershell
.\scripts\package-msix.ps1 `
  -Version 1.0.0.0 `
  -Channel beta `
  -ReleaseBaseUri https://downloads.example.com/lexverse `
  -Unsigned
```

Unsigned packages are deliberately opt-in and are not installable as production releases.

## Build a protected signed candidate in GitHub Actions

The release branch is `release/msix`, created from `dev`. Prepare and review packaging changes there; merging into `main` is not required to release. Push the branch before running the workflow and select `release/msix` as its source branch.

The manual `LexVerse signed release candidate` workflow accepts package/channel versions, channel, HTTPS release root, exact publisher subject, and publisher display name. It only runs from the `release/msix` branch and requires the source commit to be reachable from that remote branch, then repeats Release build, tests, startup/memory budgets, and dependency audit before signing.

Create protected GitHub environments named `lexverse-beta` and `lexverse-stable`. Configure required reviewers, prevent self-review, restrict deployment branches to `release/msix`, and store these environment secrets. These environment settings must be configured in GitHub; changing this repository does not update them automatically:

- `SIGNING_PFX_BASE64`: base64 of the code-signing PFX;
- `SIGNING_PFX_PASSWORD`: PFX password.

The PFX is imported into the ephemeral runner's current-user certificate store, never made exportable, and removed in an `always()` cleanup step. The packaging script requires an accessible private key, current certificate validity, an exact X.500 subject match with `Publisher`, and Code Signing EKU when the certificate declares EKUs.

The workflow does not publish to the customer channel. It creates a reviewable immutable candidate containing the signed MSIX, checksum, App Installer pointer, release metadata, and an offline Sigstore provenance bundle. Verify provenance with GitHub CLI before publishing:

```powershell
gh attestation verify .\LexVerse-beta-1.2.0.0-x64.msix `
  --repo thisishiu/LexVerse-Real-time-screen-translate
```

This separation ensures environment approval grants access to signing material but does not silently replace the live channel pointer.

## Publish atomically

1. Upload the versioned `.msix` and checksum first.
2. Verify the uploaded MSIX hash matches the generated `.sha256` file.
3. Upload `LexVerse-<channel>.appinstaller` last. It is the channel pointer and must never reference an artifact that is not already available.
4. Keep older versioned MSIX files available for rollback and repair.

The App Installer checks on launch and in the background. `ForceUpdateFromAnyVersion` permits an operator to point a channel manifest at a previously signed version for rollback. During rollback, keep the channel-manifest version increasing even though the package version decreases:

```powershell
.\scripts\package-msix.ps1 `
  -Version 1.4.0.0 `
  -AppInstallerVersion 1.5.0.1 `
  -Channel stable `
  -ReleaseBaseUri https://downloads.example.com/lexverse `
  -Publisher 'CN=Your verified certificate subject' `
  -CertificateThumbprint 'CERTIFICATE_SHA1_THUMBPRINT'
```

Stable and Beta have independent identities and update streams.

## Production requirements

- Use HTTPS hosting with stable URLs and the correct MIME types for `.msix` and `.appinstaller`.
- Sign every public package using Microsoft Store signing, Microsoft Artifact Signing, or a trusted code-signing certificate. Timestamp all non-Store signatures.
- Keep signing material outside the repository. CI should import a short-lived certificate or use a managed signing service; never pass a PFX password in source or commit a PFX.
- Publish release notes and retain at least the current and previous known-good package per channel.
- Test install, update, downgrade rollback, repair, uninstall, settings preservation, and launch on a clean supported Windows VM before promoting Beta to Stable.
- Follow `packaging/BETA_REHEARSAL.md` and retain its evidence bundle for every promoted build.
