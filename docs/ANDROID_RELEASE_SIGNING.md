# Android release signing

GolfSG's Android release workflow creates a signed APK and attaches it to a GitHub Release. The private signing key must be created once, backed up securely, and never committed to this repository.

## Create the keystore

Run this on a trusted development machine with a JDK installed:

```powershell
New-Item -ItemType Directory -Path "$HOME\GolfSG-Secrets" -Force

keytool -genkeypair `
  -v `
  -keystore "$HOME\GolfSG-Secrets\golfsg-release.keystore" `
  -alias golfsg `
  -keyalg RSA `
  -keysize 2048 `
  -validity 10000
```

Back up the keystore and both passwords in a password manager or other secure backup. The alias used by the workflow is `golfsg`. Do not create a replacement keystore for a later update: Android updates must continue to use the same signing identity.

## Add GitHub Actions secrets

In the repository, open **Settings → Secrets and variables → Actions** and add these repository secrets:

- `ANDROID_KEYSTORE_BASE64`
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_PASSWORD`

To convert the keystore to a Base64 value without committing it:

```powershell
[Convert]::ToBase64String(
  [IO.File]::ReadAllBytes("$HOME\GolfSG-Secrets\golfsg-release.keystore")
) | Set-Clipboard
```

Paste the clipboard contents into `ANDROID_KEYSTORE_BASE64`. Store the keystore password and key password in the other two secrets. If the passwords are the same, the same value may be used for both secrets.

## Test locally first

```powershell
$env:GOLFSG_SIGNING_PASSWORD = Read-Host "Keystore password"

dotnet publish src\GolfSG\GolfSG.csproj `
  -f net10.0-android `
  -c Release `
  -p:AndroidPackageFormat=apk `
  -p:PublishTrimmed=false `
  -p:RunAOTCompilation=false `
  -p:AndroidLinkMode=None `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore="$HOME\GolfSG-Secrets\golfsg-release.keystore" `
  -p:AndroidSigningKeyAlias=golfsg `
  -p:AndroidSigningKeyPass=env:GOLFSG_SIGNING_PASSWORD `
  -p:AndroidSigningStorePass=env:GOLFSG_SIGNING_PASSWORD

Remove-Item Env:GOLFSG_SIGNING_PASSWORD
```

Install the generated `*-Signed.apk` on a real device before publishing it.

## Publish a release

The workflow in `.github/workflows/release-android.yml` runs when a `v*` tag is pushed. It also supports **Actions → Android release → Run workflow** with an existing tag, which can be used to add an APK to an already-created source release such as `v0.1.0-alpha`.

For a new alpha release:

```powershell
git tag -a v0.2.0-alpha -m "GolfSG v0.2.0-alpha"
git push origin v0.2.0-alpha
```

The workflow builds the APK, creates or updates the GitHub pre-release, attaches the APK, and attaches a SHA-256 checksum file.
