# PJAnimation

Implicit animations for **.NET MAUI Controls**, ported from [Comet](https://github.com/dotnet/Comet). Set the
values you want inside `Animate`, and PJAnimation animates every bindable property you changed from its current
value to the new one.

```csharp
using PJAnimation;

box.Animate(duration: 0.5, action: b =>
{
    b.Color = Colors.Blue;
    b.Rotation = 180;
    b.Scale = 1.5;
});
```

No custom handlers, no MAUI fork, no `Animation` objects to wire up: write the end state, get the animation.

## Contents

1. [Install](#1-install)
2. [Your first animation](#2-your-first-animation)
3. [Duration, delay and easing](#3-duration-delay-and-easing)
4. [Repeating and auto-reversing](#4-repeating-and-auto-reversing)
5. [Doing something when it ends](#5-doing-something-when-it-ends)
6. [Animating several views at once](#6-animating-several-views-at-once)
7. [Sequences](#7-sequences)
8. [Pausing, resuming and stopping](#8-pausing-resuming-and-stopping)
9. [Using it from XAML pages](#9-using-it-from-xaml-pages)
10. [Custom controls and custom types](#10-custom-controls-and-custom-types)
11. [Reduced motion](#11-reduced-motion)
12. [Performance tips](#12-performance-tips)
13. [Diagnostics](#13-diagnostics)
14. [Unit testing your animations](#14-unit-testing-your-animations)
15. [Things to know](#15-things-to-know)
16. [How it works](#how-it-works)

## 1. Install

Targets **.NET 10** MAUI apps (Android, iOS, Mac Catalyst, Windows).

```bash
dotnet add package PJAnimation --prerelease
```

> The package hasn't been published to nuget.org yet. Until then, reference `src/PJAnimation/PJAnimation.csproj`
> directly or pack it locally (see [Building from source](#building-from-source)).

There is nothing to register in `MauiProgram`. Add the namespace wherever you animate:

```csharp
using PJAnimation;
```

## 2. Your first animation

Call `Animate` on any `VisualElement` and assign the **final** values inside the lambda:

```csharp
var box = new BoxView { Color = Colors.Red, WidthRequest = 100, HeightRequest = 100 };

box.Animate(b =>
{
    b.Color = Colors.Blue;   // Color: interpolated
    b.Rotation = 180;        // double: interpolated
    b.Scale = 1.5;
});
```

What happens:

1. Your lambda runs once, right away, on the UI thread.
2. PJAnimation records which bindable properties changed, puts the old values back (nothing flashes on screen),
   and animates each one from old to new.
3. When it ends, every property holds exactly the value you assigned.

`Animate` returns the view, so you can chain it in an object initializer:

```csharp
var label = new Label { Text = "Hello", Opacity = 0 }
    .Animate(l => l.Opacity = 1, duration: 1);
```

**What gets interpolated:** `int`, `short`, `byte`, `sbyte`, `ushort`, `uint`, `long`, `ulong`, `float`, `double`,
`decimal`, `Color`, `SolidColorBrush`, `Thickness`, `CornerRadius`, `Point`, `Size` and `Rect`. `bool` switches at
the halfway point. Any other type (strings, enums, gradient brushes…) snaps to the new value when the animation
ends. You can [add your own](#custom-types).

## 3. Duration, delay and easing

Durations and delays are in **seconds** (the default duration is `0.2`):

```csharp
box.Animate(duration: 1.5, delay: 0.5, action: b => b.TranslationX = 200);
```

The default easing is `Easing.Linear`. Pass any `Microsoft.Maui.Easing` as the first argument to change it:

```csharp
box.Animate(Easing.CubicInOut, b => b.TranslationX = 0, duration: 1);
box.Animate(Easing.SpringOut, b => b.Scale = 1, duration: 0.6);
```

## 4. Repeating and auto-reversing

```csharp
// Pulse forever: grow, shrink back, repeat.
heart.Animate(duration: 0.6, repeats: true, autoReverses: true, action: h => h.Scale = 1.2);

// Play forward then backward once, ending where it started.
button.Animate(autoReverses: true, duration: 0.15, action: b => b.Scale = 0.9);
```

A repeating animation runs until you [stop it](#8-pausing-resuming-and-stopping) or its view is removed
from the screen for good.

## 5. Doing something when it ends

Pass a `completed` callback (it runs on the UI thread, and never for repeating animations):

```csharp
toast.Animate(t => t.Opacity = 0, duration: 0.3,
    completed: () => layout.Remove(toast));
```

Or use `AnimateAsync` and `await` it. It returns `true` if the animation was cancelled and `false` if it finished,
the same convention as MAUI's `FadeToAsync`:

```csharp
async void OnShowDetails(object? sender, EventArgs e)
{
    await card.AnimateAsync(c => c.TranslationY = 0, duration: 0.3);
    await details.AnimateAsync(Easing.CubicOut, d => d.Opacity = 1, duration: 0.2);
}
```

It accepts a `CancellationToken`. Cancelling stops the animation **where it is** (it does not jump to the end):

```csharp
var cts = new CancellationTokenSource();
bool cancelled = await panel.AnimateAsync(p => p.TranslationX = 300, duration: 2, cancellationToken: cts.Token);
```

`AnimateAsync` has no `repeats` parameter, since a repeating animation would never complete. While the view is off
screen the animation is paused, so the task stays pending until the view is shown again.

## 6. Animating several views at once

Changes to the view **and its visual descendants** are animated together, so animating a layout lets you change
its children too:

```csharp
cardLayout.Animate(duration: 0.4, action: _ =>
{
    cardLayout.BackgroundColor = Colors.DarkSlateBlue;
    title.TextColor = Colors.White;      // title is inside cardLayout
    subtitle.Opacity = 0.7;              // so is subtitle
});
```

Changes to objects **outside** that tree are applied instantly, without animation. List them in `alsoCapture` to
animate them as well:

```csharp
header.Animate(alsoCapture: [fab], duration: 0.3, action: h =>
{
    h.HeightRequest = 56;
    fab.TranslationY = 80;   // fab isn't inside header, but it's in alsoCapture
});
```

## 7. Sequences

Chain steps that run one after another with `BeginAnimationSequence` … `EndAnimationSequence`. Each step animates
from wherever the previous one left off:

```csharp
var status = new Label { Text = "Syncing…" }
    .BeginAnimationSequence(repeats: true, alsoCapture: [syncButton])
    .Animate(duration: 1, action: l =>
    {
        l.BackgroundColor = Colors.Fuchsia;
        syncButton.BackgroundColor = Colors.Green;
    })
    .Animate(duration: 1, action: l => l.BackgroundColor = Colors.AliceBlue)
    .Animate(Easing.BounceOut, duration: 0.5, action: l => l.Scale = 1.1,
             completed: () => Debug.WriteLine("third step done"))
    .Animate(duration: 0.5, action: l => l.Scale = 1)
    .EndAnimationSequence();
```

- `BeginAnimationSequence(completed, delay, repeats, alsoCapture)` configures the whole sequence.
- Each `.Animate(...)` step takes its own easing, `duration`, `delay` and `completed`.
- `EndAnimationSequence()` starts it and returns the view.

## 8. Pausing, resuming and stopping

Pause and resume every animation on a view **and its descendants**, which is handy for a whole page:

```csharp
page.PauseAnimations();
page.ResumeAnimations();
```

The animations registered directly on a view are in `view.Animations` (`view.GetAnimations()` before C# 14). Each
one can be controlled on its own:

```csharp
foreach (var animation in spinner.Animations)
{
    animation.Pause();          // or Resume()
}

foreach (var animation in spinner.Animations)
{
    spinner.RemoveAnimation(animation);   // stop it where it is
}
```

`animation.Dispose()` also stops an animation. Starting a new animation on a property that is already animating
replaces the old one: the old one is cancelled and the new one starts from the current value. You don't need to
stop it first.

**Navigation is handled for you.** When a view leaves the screen (`Unloaded`), its animations pause and stop
ticking; when it comes back (`Loaded`), they resume. Animations you paused yourself stay paused until you call
`Resume` / `ResumeAnimations`.

## 9. Using it from XAML pages

Give the element an `x:Name` and animate it from the code-behind:

```xml
<VerticalStackLayout Padding="24">
    <Label x:Name="Headline" Text="Welcome!" FontSize="32" Opacity="0" TranslationY="20" />
    <Button x:Name="StartButton" Text="Get started" Clicked="OnStartClicked" />
</VerticalStackLayout>
```

```csharp
using PJAnimation;

public partial class WelcomePage : ContentPage
{
    public WelcomePage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Headline.Animate(Easing.CubicOut, duration: 0.5, action: h =>
        {
            h.Opacity = 1;
            h.TranslationY = 0;
        });
    }

    async void OnStartClicked(object? sender, EventArgs e)
    {
        await StartButton.AnimateAsync(b => b.Scale = 0.95, duration: 0.08, autoReverses: true);
        await Navigation.PushAsync(new MainPage());
    }
}
```

Start entrance animations from `OnAppearing` or `Loaded`. An animation started on a view that isn't on screen yet
begins right away, so it may finish before the user ever sees it.

> Assigning a property inside `Animate` works exactly like assigning it directly: it **replaces a XAML binding or
> `DynamicResource`** on that property. Animate properties you don't bind, or set the binding again afterwards.

## 10. Custom controls and custom types

### Your own bindable properties

Any property backed by a public static `XxxProperty` field is animated automatically, including those on your own
controls:

```csharp
public class ProgressRing : GraphicsView
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(ProgressRing), 0d,
        propertyChanged: (bindable, _, _) => ((ProgressRing)bindable).Invalidate());

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }
}

ring.Animate(Easing.CubicInOut, r => r.Progress = 0.75, duration: 1);
```

Plain CLR properties (no `BindableProperty`) are applied instantly. If the field doesn't follow the `XxxProperty`
naming convention, or your app is trimmed or NativeAOT-compiled, register the property once at startup:

```csharp
AnimatableProperties.Register(ProgressRing.ProgressProperty);
```

<a id="custom-types"></a>
### Your own value types

Teach PJAnimation how to interpolate a type with `Lerp.Register`. `progress` goes from `0` to `1`:

```csharp
public readonly record struct Hsl(double H, double S, double L);

Lerp.Register<Hsl>((start, end, progress) => new Hsl(
    start.H + (end.H - start.H) * progress,
    start.S + (end.S - start.S) * progress,
    start.L + (end.L - start.L) * progress));
```

Registered lerps also apply to derived types. Types without a lerp snap to the end value when the animation ends.

## 11. Reduced motion

PJAnimation respects the operating system's accessibility setting by default. When the user asked for reduced
motion, animations **jump to their end values and `completed` still runs**. A repeating animation applies its end
state once, and an auto-reversing one ends at its start values.

| Platform | Setting read |
|---|---|
| iOS / Mac Catalyst | `UIAccessibility.IsReduceMotionEnabled` |
| Android 8+ (API 26) | `!ValueAnimator.AreAnimatorsEnabled()` (animator duration scale 0, battery saver) |
| Android < 8 | `Settings.Global.animator_duration_scale == 0` |
| Windows | `!UISettings.AnimationsEnabled` |
| other / `net10.0` | never reduced |

```csharp
// Ignore it (for animations that carry essential information):
AnimationManager.RespectReducedMotion = false;

// Drive it from an in-app setting instead:
AnimationManager.ReducedMotionProvider = new MyAppMotionSetting();   // implements IReducedMotionProvider

bool willAnimate = AnimationManager.AnimationsEnabled;
```

## 12. Performance tips

- **Prefer transform properties:** `TranslationX/Y`, `Scale`, `Rotation`, `Opacity`. They don't trigger layout.
  `WidthRequest`, `HeightRequest`, `Margin`, `Padding` and `FontSize` re-measure the layout on every frame.
- **Animate `BackgroundColor`, not `Background`.** Each `SolidColorBrush` frame allocates a new brush (~1.1 KB,
  a `BindableObject`) versus 32 bytes for a `Color`.
- **Set `FontAutoScalingEnabled = false` on text whose `FontSize` you animate.** On iOS, MAUI's font manager scales
  the font through `UIFontMetrics.GetScaledFont`
  ([`FontManager.iOS.cs`](https://github.com/dotnet/maui/blob/main/src/Core/src/Fonts/FontManager.iOS.cs)), which
  **rounds to whole points**. The text then grows in visible steps (~4 per second for 10 → 30 over 5 s) even at a
  steady 60 fps. Measured on the iOS simulator over 1 005 frames, turning it off took platform font changes from
  67 to 1 005 and tick cost from 1.09 ms to 0.86 ms.
- **Speed everything up or down** for debugging with `AnimationManager.SpeedModifier` (`2` = twice as fast).

Under the hood, one shared ticker on the UI thread drives every animation using the platform frame clock
(`CADisplayLink` on iOS / Mac Catalyst, `ValueAnimator` on Android). Interpolation costs nanoseconds per property;
the real cost is MAUI applying the value (`SetValue` → handler → platform layout and draw). Value types are boxed
once per property per frame; `bool` and snap-at-end properties don't allocate and aren't set every frame.

Indicative numbers (BenchmarkDotNet ShortRun, Apple M4 Max, .NET 10, handler-less `Label`s, one frame):

| Animations | 3 doubles each (Translation/Rotation/Opacity) | 1 `Color` each |
|---:|---:|---:|
| 1 | 0.20 µs / 96 B | 0.08 µs / 32 B |
| 100 | 25 µs / 9.6 KB | 8.8 µs / 3.2 KB |
| 1000 | 274 µs / 96 KB | 103 µs / 32 KB |

Run them with `dotnet run -c Release --project benchmarks/PJAnimation.Benchmarks -- --filter '*'`.

More detail in [docs/perf-validation.md](docs/perf-validation.md).

## 13. Diagnostics

Measure what animations cost per frame:

```csharp
AnimationManager.FrameTicked += info =>
    Debug.WriteLine($"{info.AnimationCount} animations, tick {info.TickDuration.TotalMilliseconds:F3} ms, Δ {info.FrameDelta}");
```

`FrameTicked` is raised on the UI thread after every frame. `TickDuration` is the time PJAnimation spent
interpolating and calling `SetValue`; it does **not** include MAUI layout or platform rendering. With no subscribers,
no timestamps are taken. The sample app's stress page shows a HUD built on it.

## 14. Unit testing your animations

Replace the frame clock and the time source to step through animations deterministically, with no UI thread or
device. `FakeTimeProvider` comes from the
[`Microsoft.Extensions.TimeProvider.Testing`](https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing)
package:

```csharp
using Microsoft.Extensions.Time.Testing;
using PJAnimation;

[Fact]
public void Fades_in_over_one_second()
{
    var ticker = new ManualTicker();
    var clock = new FakeTimeProvider();
    AnimationManager.SetTicker(ticker);
    AnimationManager.TimeProvider = clock;
    AnimationManager.RespectReducedMotion = false;

    var label = new Label { Opacity = 0 };
    label.Animate(l => l.Opacity = 1, duration: 1);

    for (var i = 0; i < 10; i++)          // ten 50 ms frames = 0.5 s
    {
        clock.Advance(TimeSpan.FromMilliseconds(50));
        ticker.Tick();
    }

    Assert.Equal(0.5, label.Opacity, 3);
}
```

`AnimationManager` is static, so restore its settings after each test and don't run animation tests in parallel.
Frame deltas are capped by `AnimationManager.MaxFrameDelta` (100 ms by default), so advance the clock in small steps.

## 15. Things to know

- **Name clashes.** `Animation` and `AnimationExtensions` also exist in `Microsoft.Maui.Controls`, which MAUI apps
  import globally. Calls like `view.Animate(...)` are unaffected. When you name the types, add an alias:
  `using Animation = PJAnimation.Animation;`.
- **Only bindable properties are animated.** Plain CLR properties, attached properties (such as
  `AbsoluteLayout.LayoutBounds` or `Grid.Row`), read-only properties and objects outside the
  [capture scope](#6-animating-several-views-at-once) change instantly.
- **Your setters really run.** `PropertyChanged` handlers, handlers and TwoWay bindings see the final value, then
  the restored old value, then every frame.
- **No nesting.** Calling `Animate` inside another `Animate` lambda throws `InvalidOperationException`.
- **Any thread.** `Animate`, `BeginAnimationSequence`, `AddAnimation`, `RemoveAnimation` and
  `Pause/ResumeAnimations` can be called from any thread; the work is moved to the view's `Dispatcher`.
- **Memory.** Animations hold their views weakly, so animated views can still be garbage-collected. The exception is
  a repeating sequence: its step lambdas are strong references, so a lambda that captures a page keeps it alive while
  the sequence runs. Unloading the view stops the sequence. If the view is **never** loaded, call `RemoveAnimation`
  or `Dispose` yourself.

## How it works

1. `Animate` runs your action synchronously on the UI thread while listening to `PropertyChanging` /
   `PropertyChanged` on the **capture scope**: the view, its visual descendants
   (`IVisualTreeElement.GetVisualChildren()`) and any `alsoCapture` objects.
2. The old values are restored before a frame is rendered, and one animation per changed
   `(object, BindableProperty)` interpolates `old → new` with `Lerp(start, end, easing.Ease(t))`, applied with
   `SetValue` on the UI thread.
3. A single shared `AnimationManager` ticks all animations with one timestamp per frame. Frames come from the
   platform ticker (`iOSTicker`, `AndroidTicker`, or elsewhere a timer whose frames are dispatched to the UI
   thread); `AnimationManager.SetTicker(...)` plugs in your own.

### API reference (namespace `PJAnimation`)

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

## Differences from Comet

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
- Property names are resolved to `BindableProperty` via public static `<Name>Property` fields on the changed
  object's type (or registered with `AnimatableProperties.Register`). Attached properties and read-only bindable
  properties are not animated.
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
- Off-screen views stop ticking (Loaded/Unloaded pause/resume, see [Pausing, resuming and stopping](#8-pausing-resuming-and-stopping)); reduced motion is honored.

## Building from source

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
