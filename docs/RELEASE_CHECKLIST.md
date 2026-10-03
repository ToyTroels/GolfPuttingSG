# Release checklist

Use this checklist for every distributable build. A green repository build is necessary, but store credentials, signing identities, reference-data rights, and physical-device acceptance remain release-owner gates.

## Repository gates

- [ ] The release tag matches `VersionPrefix` and `VersionSuffix`, and `ApplicationBuildNumber` is greater than every previously published build.
- [ ] Locked restore succeeds with the SDK in `global.json`.
- [ ] All 203+ Release tests pass.
- [ ] Android, Windows, iOS simulator, and Mac Catalyst Release compile checks pass in CI.
- [ ] The dependency vulnerability audit reports no actionable vulnerabilities.
- [ ] `git diff --check` is clean and release notes describe user-visible changes.
- [ ] The critical journeys in [RELEASE_SMOKE_TESTS.md](RELEASE_SMOKE_TESTS.md) pass on release packages.

## Product, data, and accessibility

- [ ] The maintainer has documented the source/derivation of every strokes-gained table and confirmed redistribution rights, as required by [REFERENCE_DATA.md](REFERENCE_DATA.md).
- [ ] Store privacy declarations match [PRIVACY.md](../PRIVACY.md), the Apple privacy manifest, and Android backup behavior.
- [ ] Export, import, delete, backup recovery, upgrade installation, and offline use are tested.
- [ ] Large text, screen reader, keyboard/focus where applicable, contrast, and touch targets are checked on each shipping platform.
- [ ] Store descriptions do not call the reference data official PGA Tour or ShotLink data.

## Android

- [ ] GitHub signing secrets are configured and backed up outside the repository.
- [ ] The release workflow produces both AAB and APK files and rejects the Android debug certificate.
- [ ] The signing fingerprint matches the previously released application.
- [ ] The AAB passes Play Console validation and an internal-test install/update is exercised on a physical device.
- [ ] Store listing, privacy URL, screenshots, content rating, data-safety answers, and support contact are complete.

## Apple

- [ ] The build uses the current App Store-required Xcode/SDK and valid distribution profiles.
- [ ] Bundle identifiers, version/build, encryption declaration, privacy manifest, and App Store privacy answers agree.
- [ ] TestFlight installs and the smoke checklist pass on physical iPhone/iPad targets selected for support.
- [ ] App Store listing, screenshots, age rating, privacy URL, and support URL are complete.

## Windows

- [ ] The package identity and publisher match the Microsoft Store reservation or the production signing certificate.
- [ ] A signed MSIX installs, launches, upgrades over the preceding version, and uninstalls without losing exported data.
- [ ] Store listing, declarations, screenshots, privacy URL, and support contact are complete.

## Release decision

Release only when every applicable item above is checked and the exact tested commit is tagged. Record any platform intentionally excluded from the release.
