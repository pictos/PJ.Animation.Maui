# PJAnimation

Comet-style implicit animations for **.NET MAUI Controls** — no MAUI fork required.
Assign the values you want inside `Animate`, and PJAnimation animates every bindable property you changed
from its current value to the new one.

```csharp
using PJAnimation;

label.Animate(duration: 3, repeats: true, autoReverses: true, action: l =>
{
    l.BackgroundColor = Colors.Blue;
    l.TextColor = Colors.Orange;
    l.FontSize = 30;
});

text.BeginAnimationSequence(repeats: true, alsoCapture: [button])
    .Animate(duration: 1, action: t => { t.BackgroundColor = Colors.Fuchsia; button.BackgroundColor = Colors.Green; })
    .Animate(duration: 1, action: t => t.BackgroundColor = Colors.AliceBlue)
    .EndAnimationSequence();

page.PauseAnimations();   // cascades to visual descendants
page.ResumeAnimations();

// Awaitable alternative to the `completed` callback (PJAnimation addition):
bool cancelled = await box.AnimateAsync(b => b.TranslationX = 120, duration: .4, cancellationToken: token);
```

Durations and delays are in **seconds** (default duration `0.2`), as in Comet.

## How it works

1. `Animate` runs your action synchronously on the UI thread while observing `PropertyChanging` /
   `PropertyChanged` on the **capture scope**: the view, its visual descendants
   (`IVisualTreeElement.GetVisualChildren()`) and any objects passed in `alsoCapture`.
2. The old values are restored (no frame is rendered in between, so nothing flashes) and one animation per
   changed `(object, BindableProperty)` interpolates `old → new`:
   `value = Lerp(start, end, easing.Ease(t))`, applied with `SetValue` on the UI thread.
3. A single shared `AnimationManager` ticks all animations sequentially with one timestamp per frame. By default
   frames come from the window's MAUI `IAnimationManager` (platform frame clock); use
   `AnimationManager.SetTicker(...)` to plug in your own (`ManualTicker` is handy for tests).

## API (namespace `PJAnimation`)

| Comet | PJAnimation |
|---|---|
| `view.Animate(action, completed, duration, delay, repeats, autoReverses)` (+ `Easing` overload) | same, `where T : VisualElement`, plus optional `alsoCapture` |
| — | `view.AnimateAsync(action, duration, delay, autoReverses, alsoCapture, cancellationToken)` (+ `Easing` overload) → `Task<bool>`, `true` if cancelled (MAUI `FadeToAsync` convention). No `repeats` (it would never complete); cancelling stops in place |
| `AnimationExtensions.CreateAnimation(...)` | same (returns `null` when nothing changed) |
| `BeginAnimationSequence` / `AnimationSequence<T>.Animate` / `EndAnimationSequence`, `AnimationStep<T>` | same; `AlsoCapture` is a weakly-held `IReadOnlyList<BindableObject>` (add with `AddAlsoCapture`) |
| `Animation` (`StartDelay`, `Duration`, `CurrentTime`, `Easing`, `HasFinished`, `StartValue`, `EndValue`, `CurrentValue`, `Repeats`, `Id`, `IsPaused`, `IsDisposed`, `Tick`, `OnTick`, `Update`, `CreateAutoReversing`, `Reset`, `Pause`, `Resume`, `Dispose`) | same, plus init-only `Target`/`Property` (instead of Comet's settable `ContextualObject`/`PropertyName`), `Completed`, `IsCancelled` |
| `Lerp` (`Lerps`, `GetLerp`, `Calculate`, `LerpDelegate`), `Color.Lerp(progress, end)` | same, plus `Lerp.Register`, `Lerp.Discrete` and MAUI types |
| `AnimationManger` | `AnimationManager` (`SpeedModifier`, `SetTicker`, `Add`, `Remove`, `Ticker`, `MaxFrameDelta`, `TimeProvider`, `RespectReducedMotion`, `ReducedMotionProvider`, `AnimationsEnabled`, `FrameTicked`) |
| `Ticker` (`MaxFps`, `Fire`, `IsRunning`, `SystemEnabled`, `Start`, `Stop`) | same: `iOSTicker` (`CADisplayLink`) and `AndroidTicker` (`ValueAnimator`) by default, `Ticker` timer elsewhere (frames dispatched to the UI thread and coalesced instead of firing on the timer thread), plus `ManualTicker` |
| `View.AddAnimation/RemoveAnimation/Animations/PauseAnimations/ResumeAnimations` | extension members on `VisualElement` (`view.Animations` is a C# 14 extension property; `GetAnimations()` for older C#) |
| `Easing` | `Microsoft.Maui.Easing` (same functions) |

Built-in lerps: `int`, `short`, `byte`, `sbyte`, `ushort`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`,
`bool` (switches at 50%), `Color`, `SolidColorBrush`, `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`.
Any other type (strings, enums, gradients, …) snaps to the end value when the animation completes.
`GetLerp` walks base types; register your own with `Lerp.Register<T>((start, end, progress) => ...)`.

> **Name clashes:** `Animation` and `AnimationExtensions` also exist in `Microsoft.Maui.Controls` (a global using in
> MAUI apps). Extension calls (`view.Animate(...)`) are unaffected; when naming the types, use an alias such as
> `using Animation = PJAnimation.Animation;`.

## Lifecycle (Comet `ViewDidAppear` / `ViewDidDisappear`)

PJAnimation wires `VisualElement.Loaded` / `Unloaded` for every view that has registered animations
(`Animate`, `BeginAnimationSequence`, `AddAnimation`):

- **Unloaded** pauses the view's running animations and removes them from the manager (so nothing ticks, and
  nothing roots the view, while it is off-screen). **Loaded** resumes exactly those animations.
- **Explicit calls win.** An animation you paused with `Pause()` / `PauseAnimations()` is never resumed by
  `Loaded`, even if you paused it while the view was unloaded; an explicit `Resume()` / `ResumeAnimations()` while
  unloaded resumes immediately.
- The handlers are subscribed once per view (static, cached delegates) and unsubscribed as soon as the view has no
  animations left (completed, removed or disposed), so idle views carry no extra subscriptions.
- **Not-yet-loaded / handler-less views animate immediately** (Comet parity): the bindable values change every frame
  and the handler maps the current value when it is created. If you want an entrance animation the user actually
  sees, start it from `Loaded` / `OnAppearing`.
- **Threading:** `Animate`, `BeginAnimationSequence`, `AddAnimation`, `RemoveAnimation`, `Pause/ResumeAnimations`
  and `AnimationManager.Add/Remove` may be called from any thread; off the UI thread they are dispatched to the
  view's `Dispatcher` (so the action and `completed` run on the UI thread).

**Leaks.** Running animations are rooted by the manager; their targets are held weakly. A *repeating sequence's*
step lambdas, however, are strong references: a closure capturing a page keeps that page alive while the sequence
runs. Unloading the view stops (and un-roots) the sequence, `Dispose()` clears its steps, and `alsoCapture` objects
are held weakly. The remaining risk is a repeating sequence on a view that is **never** loaded nor disposed — call
`RemoveAnimation` / `Dispose` in that case.

## Reduced motion

`AnimationManager.RespectReducedMotion` (default `true`) honors the OS accessibility setting via
`AnimationManager.ReducedMotionProvider` (default `SystemReducedMotionProvider.Instance`; plug in your own
`IReducedMotionProvider`, e.g. an in-app setting or a fake for tests):

| Platform | Source |
|---|---|
| iOS / Mac Catalyst | `UIAccessibility.IsReduceMotionEnabled` (read on the main thread; cached when queried from other threads) |
| Android 8+ (API 26) | `!ValueAnimator.AreAnimatorsEnabled()` (animator duration scale 0, battery saver) |
| Android < 8 | `Settings.Global.animator_duration_scale == 0` |
| Windows | `!UISettings.AnimationsEnabled` |
| other / `net10.0` | never reduced |

When reduced motion is on — or the platform ticker reports system animations disabled
(`Ticker.SystemEnabled`) — animations **jump to their end values and `completed` runs**; repeating animations
apply their end state once (auto-reversing ones end at their start values). Set `RespectReducedMotion = false` for
animations that convey essential information. `AnimationManager.AnimationsEnabled` reports the combined state.

## Diagnostics

```csharp
AnimationManager.FrameTicked += info =>
    Debug.WriteLine($"{info.AnimationCount} animations, tick {info.TickDuration.TotalMilliseconds:F3} ms, Δ {info.FrameDelta}");
```

`FrameTicked` is raised on the UI thread after every frame with `AnimationFrameInfo(AnimationCount, FrameDelta,
TickDuration)`; `TickDuration` is the wall-clock time PJAnimation spent interpolating and calling `SetValue` (it does
**not** include MAUI layout or platform rendering). With no subscribers no timestamps are taken. The sample's stress
page shows a HUD built on it.

## Performance guidance

- One shared, UI-thread ticker drives every animation (one timestamp per frame, sequential ticks). Interpolation is
  a few nanoseconds per property; the real cost is `SetValue` → handler mapping → platform layout/draw.
- **Prefer transform-only properties** (`TranslationX/Y`, `Scale`, `Rotation`, `Opacity`): they don't invalidate
  layout. `WidthRequest`, `HeightRequest`, `Margin`, `Padding`, `FontSize`… re-measure every frame.
- **Animate `BackgroundColor` rather than `Background`**: a `SolidColorBrush` frame allocates a new brush
  (~1.1 KB, a `BindableObject`) versus 32 B for a `Color`.
- **Set `FontAutoScalingEnabled = false` on text you animate by `FontSize`.** It defaults to `true`, and MAUI's iOS
  font manager then runs the font through `UIFontMetrics.DefaultMetrics.GetScaledFont(...)`
  ([`FontManager.iOS.cs`](https://github.com/dotnet/maui/blob/main/src/Core/src/Fonts/FontManager.iOS.cs)), which
  **rounds the point size to whole points**. The animated value still advances every frame, but the rendered font
  (and therefore the label's frame, and the background fill drawn in it) only changes when it crosses an integer
  point — a 10 → 30 ramp over 5 s steps ~4 times per second and looks laggy even at a steady 60 fps. Measured on the
  iOS simulator over 1 005 vsyncs: platform font changes 67 → 1 005 and tick cost 1.09 ms → 0.86 ms with auto-scaling
  off. This is a MAUI/UIKit behavior, not a PJAnimation one.
- Value types are boxed once per property per frame (`BindableObject.SetValue(object)`); `bool` and discrete
  (snap-at-end) properties don't allocate and aren't re-set every frame.
- **No background-thread computation** is done on purpose: interpolation is far cheaper than the UI-thread
  marshalling it would need. This should be revisited only if real-device profiling shows tick cost (not layout or
  rendering) dominating frames — see [docs/perf-validation.md](docs/perf-validation.md).

Indicative numbers (BenchmarkDotNet ShortRun, Apple M4 Max, .NET 10, handler-less `Label`s, one frame):

| Animations | 3 doubles each (Translation/Rotation/Opacity) | 1 `Color` each |
|---:|---:|---:|
| 1 | 0.20 µs / 96 B | 0.08 µs / 32 B |
| 100 | 25 µs / 9.6 KB | 8.8 µs / 3.2 KB |
| 1000 | 274 µs / 96 KB | 103 µs / 32 KB |

Run them with `dotnet run -c Release --project benchmarks/PJAnimation.Benchmarks -- --filter '*'`.

## Deviations from Comet

Behavioral choices (approved):
- **Default easing is `Easing.Linear`** (Comet: `Easing.Default` = CubicInOut). Pass an `Easing` to opt in.
- **Capture scope** is the view + visual descendants + `alsoCapture` (Comet monitored globally). Changes outside the
  scope, and changes to plain CLR (non-bindable) properties, apply immediately without animation.
- Manager is named `AnimationManager` (Comet: `AnimationManger`).

Forced by MAUI:
- Your setters really run once: `PropertyChanged` handlers, handler mappers and TwoWay bindings see the final value,
  then the restored old value, then every animated frame.
- Assigning a property clears its OneWay binding / DynamicResource **exactly like a direct assignment** (the end
  state equals what the direct assignment would leave). Style values are overridden by local values.
- Property names are resolved to `BindableProperty` via public static `<Name>Property` fields. Attached properties
  and non-conventional names are not animated unless registered with `AnimatableProperties.Register(property)`
  (also recommended for trimmed/NativeAOT apps). Read-only bindable properties are never captured.
- `Animate` called inside another `Animate` action throws `InvalidOperationException`.

Comet bugs fixed (API unchanged):
- `completed` callbacks (animations, sequences and sequence steps) are invoked; `Completed` property added.
- `long` (and unsigned / small integer) lerps no longer throw or change type; sequence delay is honored; a repeating
  sequence whose steps change nothing no longer recurses forever.
- Animations are ticked sequentially on the UI thread (Comet used `Parallel.ForEach` on a non-thread-safe list).
- Unknown types snap at the end instead of failing; the final frame always lands exactly on the assigned value.
- The base `Ticker` uses a UI dispatcher timer instead of a thread-pool timer; `SetTicker` stops the previous ticker.
- When system animations are disabled, animations jump to their final state and `completed` runs (Comet dropped them,
  leaving the restored old values). Auto-reversing animations end at their start values.
- A new animation on the same `(object, property)` replaces the running one (which is marked `IsCancelled`) and
  starts from the current animated value.
- Frame deltas use a monotonic clock shared by all animations in a frame and are clamped (`MaxFrameDelta`, 100 ms).
- Animations hold their targets weakly; animated views can be garbage-collected.
- Off-screen views stop ticking (Loaded/Unloaded pause/resume, see *Lifecycle*); reduced motion is honored.

## Building

Requires the .NET 10 SDK (pinned in `global.json`). With the MAUI workload installed the library also targets
`net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` (and `net10.0-windows10.0.19041.0` on Windows); otherwise, or
with `-p:PJAnimationPlatformTargets=false`, it builds `net10.0` only.

```bash
dotnet build PJAnimation.slnx      # library, unit tests, benchmarks
dotnet test PJAnimation.slnx
dotnet pack src/PJAnimation -c Release -o artifacts
```

The MAUI apps are not part of `PJAnimation.slnx` (they need the platform SDKs/Xcode and don't run headless):

| Project | Purpose |
|---|---|
| `samples/PJAnimation.Sample` | Comet `AnimationSample` port + stress page (N = 1/10/50/200 views, transform vs layout properties, tick-cost / FPS HUD) |
| `tests/PJAnimation.DeviceTests` | On-device harness against real handlers (UI-thread ticks, no flash on capture/restore, Loaded/Unloaded, `MauiContext` resolution, reduced motion, background-thread `Animate`) — see its README |

```bash
dotnet build samples/PJAnimation.Sample -f net10.0-maccatalyst   # or net10.0-android / net10.0-ios
dotnet build tests/PJAnimation.DeviceTests -t:Run -f net10.0-maccatalyst
```

The sample builds against the library project by default. To validate the real consumer experience it can
instead consume the packed NuGet from the local `artifacts` feed (configured in `nuget.config`):

```bash
dotnet pack src/PJAnimation -c Release -o artifacts
dotnet build samples/PJAnimation.Sample -f net10.0-maccatalyst -p:UsePJAnimationPackage=true
```

If your Xcode is newer than the one the iOS/Mac Catalyst workload was built against, add
`-p:ValidateXcodeVersion=false`.

### Running the sample on a physical iPhone

The sample's bundle ID is `com.pjanimation.sample` and it is signed with an Xcode-managed development
provisioning profile (`iOS Team Provisioning Profile: com.pjanimation.sample`, team `K68WX7TNF9`).
The identity and profile are set in `PJAnimation.Sample.csproj` and apply only to `ios-arm64` device
builds; simulator and Mac Catalyst builds are unaffected.

```bash
dotnet build samples/PJAnimation.Sample -f net10.0-ios -r ios-arm64 -p:ValidateXcodeVersion=false
xcrun devicectl device install app --device <UDID> \
  samples/PJAnimation.Sample/bin/Debug/net10.0-ios/ios-arm64/PJAnimation.Sample.app
```

Override the signing values on the command line to use a different team:

```bash
dotnet build ... -p:CodesignKey="Apple Development: you@example.com (XXXXXXXXXX)" \
                 -p:CodesignProvision="iOS Team Provisioning Profile: com.pjanimation.sample"
```

Profiles issued by a **free** Apple ID expire after 7 days, so they must be regenerated. Easiest route
without opening Xcode — generate a throwaway app with the same bundle ID and let Xcode mint the profile:

```bash
brew install xcodegen   # once
mkdir -p /tmp/prov/Sources && cd /tmp/prov
cat > project.yml <<'YML'
name: Prov
options:
  deploymentTarget: { iOS: "15.0" }
targets:
  Prov:
    type: application
    platform: iOS
    sources: [Sources]
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: com.pjanimation.sample
        DEVELOPMENT_TEAM: K68WX7TNF9
        CODE_SIGN_STYLE: Automatic
        GENERATE_INFOPLIST_FILE: YES
YML
echo 'print("prov")' > Sources/main.swift
xcodegen generate
xcodebuild -project Prov.xcodeproj -scheme Prov -destination 'generic/platform=iOS' \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
```

The profile lands in `~/Library/Developer/Xcode/UserData/Provisioning Profiles/`; the build step above
is only a vehicle for provisioning, so it does not matter if it fails after signing. Verify with:

```bash
codesign -dv --verbose=2 samples/PJAnimation.Sample/bin/Debug/net10.0-ios/ios-arm64/PJAnimation.Sample.app
```

## License

MIT — see [LICENSE](LICENSE). Portions derived from [Comet](https://github.com/dotnet/Comet) (MIT, .NET Foundation).
