# Run GolfSG On A Wireless Android Phone

This runbook is for future AI agents working in this repo. It documents the exact command-line flow that successfully launched the MAUI app on Troel's Samsung phone via Visual Studio wireless debugging.

## Known Good Environment

- Repo root: `C:\Users\Troel\source\repos\GolfPuttingSG`
- Project: `src\GolfSG\GolfSG.csproj`
- Android target framework: `net10.0-android`
- Visual Studio Android SDK path:

```powershell
C:\Program Files (x86)\Android\android-sdk
```

- ADB path:

```powershell
C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe
```

## Check That The Phone Is Connected

From the repo root, run:

```powershell
& 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe' devices -l
```

Known successful device output looked like this:

```text
adb-RZCY51KR00F-aTIx6L._adb-tls-connect._tcp device product:e1sxeea model:SM_S921B device:e1s
```

If no device appears, ask the user to confirm that Visual Studio wireless debugging is connected and that the phone is unlocked or has accepted the debugging prompt.

## Build, Install, And Run On The Phone

Use the device serial from `adb devices -l` as the `AdbTarget`.

Known successful command:

```powershell
dotnet build src\GolfSG\GolfSG.csproj -f net10.0-android -t:Run -p:AdbTarget='-s adb-RZCY51KR00F-aTIx6L._adb-tls-connect._tcp'
```

If you want to separate install and launch, run install first:

```powershell
dotnet build src\GolfSG\GolfSG.csproj -f net10.0-android -t:Install -p:AdbTarget='-s adb-RZCY51KR00F-aTIx6L._adb-tls-connect._tcp'
```

Then run:

```powershell
dotnet build src\GolfSG\GolfSG.csproj -f net10.0-android -t:Run -p:AdbTarget='-s adb-RZCY51KR00F-aTIx6L._adb-tls-connect._tcp'
```

## Confirm The App Is Installed And Foregrounded

Check that the package exists:

```powershell
& 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe' -s 'adb-RZCY51KR00F-aTIx6L._adb-tls-connect._tcp' shell pm list packages | Select-String -Pattern 'golf|troel'
```

Expected package:

```text
package:com.troel.golfsg
```

Check the current foreground app:

```powershell
& 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe' -s 'adb-RZCY51KR00F-aTIx6L._adb-tls-connect._tcp' shell dumpsys window | Select-String -Pattern 'mCurrentFocus|mFocusedApp|topResumedActivity'
```

Known successful foreground output:

```text
mCurrentFocus=Window{... u0 com.troel.golfsg/crc642ea24c1578677685.MainActivity}
mFocusedApp=ActivityRecord{... u0 com.troel.golfsg/crc642ea24c1578677685.MainActivity ...}
```

## Notes For Agents

- `adb` was not on the shell `PATH`; use the full Visual Studio SDK path above.
- The `Run` target may print mostly build output. Use `-v:normal` if you need to see `_Run` details such as `Found device`.
- The property that worked for selecting the phone was `AdbTarget='-s <serial>'`.
- `AndroidDeviceSerial=<serial>` built successfully but did not visibly select or launch the device in this environment.
- Do not assume the serial is stable forever. Always start with `adb devices -l` and use the current serial.
