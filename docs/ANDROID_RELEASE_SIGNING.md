# Android release signing

GolfSG's Android release workflow creates a signed Android App Bundle (AAB) for Play distribution and a signed APK for direct testing. It refuses packages signed by the Android debug certificate. The private key must be created once, backed up securely, and never committed.

## Create and protect the keystore

Run `keytool -genkeypair` on a trusted machine with JDK 21 or newer. Use alias `golfsg`, RSA 2048 or stronger, and an appropriate validity period. Keep the keystore and both passwords in independent secure backups. Android updates must retain the same signing identity.

## Configure GitHub Actions secrets

Add these repository Actions secrets:

- `ANDROID_KEYSTORE_BASE64`
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_PASSWORD`

To copy a keystore as Base64 from PowerShell:

```powershell
[Convert]::ToBase64String(
  [IO.File]::ReadAllBytes('C:\secure\golfsg-release.keystore')
) | Set-Clipboard
```

The workflow writes secrets only under the temporary runner directory and removes them in an `always()` step. Passwords are supplied to the Android toolchain through temporary password files so they are not exposed as command-line values.

## Test a production-signed package locally

```powershell
$env:GOLFSG_SIGNING_PASSWORD = Read-Host 'Keystore password'

dotnet publish src\GolfSG\GolfSG.csproj `
  -f net10.0-android `
  -c Release `
  -p:AndroidPackageFormats=aab%3Bapk `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore='C:\secure\golfsg-release.keystore' `
  -p:AndroidSigningKeyAlias=golfsg `
  -p:AndroidSigningKeyPass=env:GOLFSG_SIGNING_PASSWORD `
  -p:AndroidSigningStorePass=env:GOLFSG_SIGNING_PASSWORD

Remove-Item Env:GOLFSG_SIGNING_PASSWORD
```

Inspect both signing identities with `keytool -printcert -jarfile <package>` and compare the certificate fingerprint with the backed-up production identity. Never distribute a package that reports `CN=Android Debug`. Install the APK on a physical device and validate the AAB through a Play internal-test track.

## Publish a release

`.github/workflows/release-android.yml` runs for `v*` tags and can also rebuild an existing version tag manually. It verifies that the tag matches the centrally defined version, runs the Release tests and dependency audit, builds signed AAB and APK files, rejects debug signing, creates SHA-256 checksums, records signing-certificate reports, and creates or updates the GitHub prerelease.

For the current alpha after every item in `docs/RELEASE_CHECKLIST.md` is satisfied:

```powershell
git tag -a v0.2.0-alpha -m 'GolfSG v0.2.0-alpha'
git push origin v0.2.0-alpha
```

Do not tag a commit that differs from the one exercised by the final packaged-app smoke tests.
