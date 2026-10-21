# Real-device performance validation

PJAnimation runs every animation from **one shared UI-thread ticker** (MAUI's platform frame clock). Interpolation
cost is measured in microbenchmarks (`benchmarks/PJAnimation.Benchmarks`), but what matters on a phone is the
whole frame: `SetValue` → handler mapping → layout → platform rendering. Emulators, simulators and Mac Catalyst
are **not** representative. This procedure must be run on physical devices before deciding on any background
computation.

## Devices

Run on at least one device per tier, Release builds, device plugged in, screen brightness/thermal steady:

| Tier | Android | iOS |
|---|---|---|
| Low | ~4 GB RAM, Cortex-A53/A55 class (e.g. Galaxy A1x), 60 Hz | oldest supported iPhone (A12–A13) |
| High | flagship, 120 Hz | ProMotion iPhone (120 Hz) |

## Build

```bash
dotnet build samples/PJAnimation.Sample -c Release -f net10.0-android -t:Run      # (AOT/profiled AOT defaults)
dotnet build samples/PJAnimation.Sample -c Release -f net10.0-ios -t:Run -p:RuntimeIdentifier=ios-arm64
```

Use Release (Debug on Android uses the interpreter/fast deployment and distorts everything).

## Matrix (stress page)

For each device, run 30 s per cell after a 5 s warm-up, record the HUD and the platform tool numbers:

| N views | 1 | 10 | 50 | 200 |
|---|---|---|---|---|
| Transform-only (Translation/Rotation/Scale/Opacity) | | | | |
| Layout-affecting (Width/Height/Margin) | | | | |

Record per cell: HUD `tick avg` / `max` (PJAnimation's own cost), HUD `ticks/s`, platform jank metrics below.
Also run the AnimationSample page (colors + font size, repeating sequence) as a "typical app" data point.

## Android

```bash
adb shell dumpsys gfxinfo com.pjanimation.sample reset
# ... run the cell for 30 s ...
adb shell dumpsys gfxinfo com.pjanimation.sample > gfxinfo-<device>-<mode>-<N>.txt
```

Look at `Janky frames`, `50th/90th/95th/99th percentile` frame times and `Number Slow UI thread`. For a breakdown,
capture a **Perfetto** trace (`adb shell perfetto -o /data/misc/perfetto-traces/trace -t 20s sched freq gfx view am`
or record from Android Studio / ui.perfetto.dev) and compare the duration of the `Choreographer#doFrame` →
animation callback slice (PJAnimation tick) against `traversal` (measure/layout/draw) and `DrawFrame` (RenderThread).

## iOS

Profile with **Instruments → Animation Hitches** (Xcode) on the device: record 30 s per cell and note hitch-time
ratio (ms/s), number of hitches and whether they are *commit* (main thread: our tick + layout) or *render* hitches.
Optionally add the **Time Profiler** to attribute main-thread time to `AnimationManager.OnFire` vs. MAUI layout
(`LayoutSubviews`, `CrossPlatformMeasure/Arrange`).

## Pass criteria

- Transform-only, N ≤ 50: no sustained jank (Android janky frames < 1 %, iOS hitch ratio < 5 ms/s) on the low tier.
- Transform-only, N = 200 and layout-affecting cells: degrade gracefully; record but don't gate.
- HUD `tick avg` must stay well below the frame budget (< 10 % of 16.7 ms / 8.3 ms).

## Decision gate: background computation

PJAnimation deliberately computes interpolations on the UI thread (no background compute). Revisit **only if**,
on a real low-tier device, a cell that fails the pass criteria shows that **PJAnimation's tick** (HUD `tick avg`, or
the Perfetto/Time Profiler slice for `OnFire` minus `SetValue`/handler time) accounts for **> 25 % of the main-thread
frame time**. If the cost is in `SetValue`, handler mapping or layout/rendering, background interpolation cannot help
(those must run on the UI thread) — optimize property choice (transform-only), batching or the handlers instead.
Record the results (device, OS, build, table above, traces) in an issue before changing the architecture.
