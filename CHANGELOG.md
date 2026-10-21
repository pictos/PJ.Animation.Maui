# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0-preview.1] - Unreleased

First preview: a port of Comet's implicit animation API (dotnet/Comet PR #146) to .NET MAUI Controls.

### Added
- `view.Animate(action, completed, duration, delay, repeats, autoReverses, alsoCapture)` (+ `Easing` overload),
  `AnimateAsync` (awaitable, `Task<bool>` = cancelled, `CancellationToken`), `CreateAnimation`, `BeginAnimationSequence` / `AnimationSequence<T>` / `EndAnimationSequence`.
- `Animation`, `Lerp` (numeric types, `bool`, `Color`, `SolidColorBrush`, `Thickness`, `CornerRadius`, `Point`, `Size`,
  `Rect`; `Lerp.Register`), `AnimationManager`, `Ticker` (`System.Timers.Timer`, frames coalesced onto the UI thread), `iOSTicker` (`CADisplayLink`, default on iOS/Mac Catalyst), `AndroidTicker` (`ValueAnimator`, default on Android), `ManualTicker`.
- `AddAnimation` / `RemoveAnimation` / `GetAnimations` / `PauseAnimations` / `ResumeAnimations` on `VisualElement`
  (cascading to visual descendants), `AnimatableProperties.Register`.
- Lifecycle: `Unloaded` pauses and `Loaded` resumes a view's animations; explicit pauses are preserved.
- Thread safety: animation APIs called off the UI thread are dispatched to the view's `Dispatcher`.
- Reduced motion: `AnimationManager.RespectReducedMotion`, `ReducedMotionProvider`, `IReducedMotionProvider`,
  `SystemReducedMotionProvider` (iOS/Mac Catalyst, Android, Windows), `AnimationsEnabled`.
- Diagnostics: `AnimationManager.FrameTicked` / `AnimationFrameInfo` (zero cost without subscribers).
- Sample app (Comet AnimationSample port, stress page with HUD), on-device test harness, BenchmarkDotNet benchmarks.
- NuGet package with README, symbols (`.snupkg`), Source Link, trimming / NativeAOT compatibility.

### Changed (vs. Comet)
- Default easing is `Easing.Linear`; capture scope is the view, its visual descendants and `alsoCapture`;
  `AnimationManger` is spelled `AnimationManager`; `Microsoft.Maui.Easing` is reused. See README "Deviations".

### Fixed (vs. Comet)
- `completed` callbacks run; integer lerps keep their type; sequence delays are honored; sequential UI-thread ticking;
  disabled system animations jump to the end instead of dropping the change; conflicting animations on the same
  property are replaced; weak targets.

### Documentation
- README "Performance guidance": animating `FontSize` on iOS requires `FontAutoScalingEnabled = false`, otherwise
  MAUI's font manager rounds the point size to whole points (`UIFontMetrics.GetScaledFont`) and the text steps
  ~4 times per second. The sample's `AnimationSample` page sets it.
