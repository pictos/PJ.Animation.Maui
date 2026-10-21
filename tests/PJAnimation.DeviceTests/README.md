# PJAnimation.DeviceTests

A minimal MAUI app (`net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`) that runs checks against **real
handlers** — things the headless `net10.0` unit tests cannot observe. It is intentionally not part of
`PJAnimation.slnx` / `dotnet test`.

| Check | What it proves |
|---|---|
| `DefaultTickerDrivesFrames` | The default platform ticker (`iOSTicker` on iOS/Mac Catalyst, `AndroidTicker` on Android) drives an animation to its end value and stops when idle |
| `TicksRunOnUIThread` | Every frame runs on the main thread; reports frame count, frame delta and tick cost |
| `CaptureAndRestoreLeavesPlatformValueUnchangedBeforeFirstTick` | The native view (`UIView.Alpha` / `Android.Views.View.Alpha`, translation) never sees the end value during capture + restore — no flash |
| `UnloadedPausesAndLoadedResumes` | Removing the view from the tree freezes the animation; re-adding resumes it |
| `ExplicitPauseSurvivesReload` | An explicitly paused animation is not resumed by `Loaded` |
| `ReducedMotionJumpsToEndOnPlatformView` | With reduced motion, the native value is the end value right away and `completed` runs |
| `BackgroundThreadAnimateIsDispatched` | `Animate` from a worker thread runs the action and `completed` on the UI thread |

When the OS has animations disabled (e.g. Android `animator_duration_scale = 0`) the ticking checks report `SKIP`.

## Output

Each line is printed to stdout / logcat with the prefix `PJANIMATION-DEVICETESTS` (`START`, `PASS`, `FAIL`, `SKIP`,
`DONE passed=… failed=… skipped=…`) and the report is written to `$PJANIMATION_RESULTS` (absolute path) or
`FileSystem.AppDataDirectory/devicetest-results.txt`. Android and Mac Catalyst quit when done; iOS stays open.

## Running

```bash
# Mac Catalyst (launch the bundle directly so the env var reaches the app)
dotnet build tests/PJAnimation.DeviceTests -f net10.0-maccatalyst
open -W -n --env PJANIMATION_RESULTS=$PWD/artifacts/devicetests/maccatalyst.txt \
  "tests/PJAnimation.DeviceTests/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/PJAnimation Device Tests.app"

# Android (device or emulator)
adb logcat -c
dotnet build tests/PJAnimation.DeviceTests -t:Run -f net10.0-android
adb logcat -d | grep PJANIMATION-DEVICETESTS

# iOS simulator
dotnet build tests/PJAnimation.DeviceTests -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
xcrun simctl install booted tests/PJAnimation.DeviceTests/bin/Debug/net10.0-ios/iossimulator-arm64/PJAnimation.DeviceTests.app
SIMCTL_CHILD_PJANIMATION_RESULTS=$PWD/artifacts/devicetests/ios.txt \
  xcrun simctl launch --console booted com.pjanimation.devicetests   # iOS apps cannot quit: Ctrl+C after DONE
```

Add `-p:ValidateXcodeVersion=false` if your Xcode is newer than the one the Apple workloads expect. Apps built with
recent Apple SDKs must adopt the UIScene life cycle, hence the `UIApplicationSceneManifest` / `SceneDelegate`.
