using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace PJAnimation;

/// <summary>
/// Runs all active <see cref="Animation"/>s from a single shared <see cref="PJAnimation.Ticker"/>.
/// Animations are ticked sequentially on the UI thread with one timestamp per frame, so animations
/// started together stay in sync.
/// </summary>
/// <remarks>
/// Not thread-safe: configure it from the UI thread. <see cref="Add"/> and <see cref="Remove"/> dispatch to the
/// animation target's dispatcher when called from another thread.
/// </remarks>
public static class AnimationManager
{
	static readonly List<Animation> animations = new();
	static readonly List<Animation> frame = new();
	static Ticker ticker = null!;
	static long lastUpdate;

	static AnimationManager()
	{
		SetTicker(CreateDefaultTicker());
	}

	static Ticker CreateDefaultTicker()
#if IOS || MACCATALYST
		=> new iOSTicker();
#elif ANDROID
		=> new AndroidTicker();
#else
		=> new Ticker();
#endif

	/// <summary>Multiplier applied to elapsed time (e.g. 0.5 = half speed).</summary>
	public static double SpeedModifier { get; set; } = 1;

	/// <summary>
	/// Maximum elapsed time applied in a single frame. Larger gaps (app suspended, debugger, long frame)
	/// are clamped so animations don't jump. Defaults to 100 ms.
	/// </summary>
	public static TimeSpan MaxFrameDelta { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>Monotonic clock used to measure frame times. Replaceable for tests.</summary>
	public static TimeProvider TimeProvider { get; set; } = TimeProvider.System;

	/// <summary>
	/// Whether the platform "reduce motion" accessibility setting (or Android's animator duration scale 0 / battery saver)
	/// is honored. When <see langword="true"/> (default) and the system reports reduced motion,
	/// animations jump to their end state and invoke their completed callbacks, as when <see cref="Ticker.SystemEnabled"/>
	/// is <see langword="false"/>. Set to <see langword="false"/> to always animate.
	/// </summary>
	public static bool RespectReducedMotion { get; set; } = true;

	/// <summary>
	/// Whether animations currently run: <see cref="Ticker.SystemEnabled"/> and not reduced motion
	/// (unless <see cref="RespectReducedMotion"/> is <see langword="false"/>).
	/// </summary>
	public static bool AnimationsEnabled
		=> ticker.SystemEnabled && !(RespectReducedMotion && SystemReducedMotionProvider.Instance.IsReducedMotionEnabled);

	/// <summary>
	/// Raised on the UI thread after every frame with the number of animations ticked and the time spent ticking them
	/// (measured with <see cref="Stopwatch"/>). Intended for diagnostics/HUDs; when there are no subscribers no
	/// measurement is taken.
	/// </summary>
	public static event Action<AnimationFrameInfo>? FrameTicked;

	/// <summary>The current ticker.</summary>
	public static Ticker Ticker => ticker;

	internal static int Count => animations.Count;

	/// <summary>Replaces the ticker. The previous ticker is stopped; the new one starts if animations are running.</summary>
	public static void SetTicker(Ticker ticker)
	{
		ArgumentNullException.ThrowIfNull(ticker);
		var previous = AnimationManager.ticker;
		var isRunning = previous?.IsRunning ?? false;
		if (previous is not null)
		{
			previous.Stop();
			previous.Fire = null;
		}

		AnimationManager.ticker = ticker;
		ticker.Fire = OnFire;
		if (isRunning || animations.Count > 0)
			Start();
	}

	/// <summary>
	/// Adds an animation. When the system has animations disabled, the animation jumps to its end state
	/// and its completed callback is invoked immediately.
	/// </summary>
	public static void Add(Animation animation)
	{
		ArgumentNullException.ThrowIfNull(animation);
		if (animation.IsDisposed || animation.HasFinished)
			return;
		if (DispatchIfRequired(animation, Add))
			return;

		if (!AnimationsEnabled)
		{
			Remove(animation);
			animation.FinishImmediately();
			animation.RaiseCompleted();
			return;
		}

		if (!animation.IsInManager)
		{
			animation.IsInManager = true;
			animations.Add(animation);
		}

		if (!ticker.IsRunning)
			Start();
	}

	/// <summary>Removes an animation (it stops being ticked; its current values stay applied).</summary>
	public static void Remove(Animation animation)
	{
		ArgumentNullException.ThrowIfNull(animation);
		if (DispatchIfRequired(animation, Remove))
			return;
		RemoveCore(animation);
		if (animations.Count == 0)
			End();
	}

	/// <summary>Stops and removes every animation (test/reset helper).</summary>
	internal static void Clear()
	{
		foreach (var a in animations)
			a.IsInManager = false;
		animations.Clear();
		End();
	}

	static void RemoveCore(Animation animation)
	{
		if (!animation.IsInManager)
			return;
		animation.IsInManager = false;
		animations.Remove(animation);
	}

	static bool DispatchIfRequired(Animation animation, Action<Animation> action)
	{
		var dispatcher = DispatcherHelper.FindDispatcher(animation.FindTarget());
		if (dispatcher is not { IsDispatchRequired: true })
			return false;
		dispatcher.Dispatch(() => action(animation));
		return true;
	}

	static void Start()
	{
		lastUpdate = TimeProvider.GetTimestamp();
		ticker.Start();
	}

	static void End() => ticker.Stop();

	static void OnFire()
	{
		var frameTicked = FrameTicked;
		var started = frameTicked is null ? 0 : Stopwatch.GetTimestamp();
		var now = TimeProvider.GetTimestamp();
		var elapsed = TimeProvider.GetElapsedTime(lastUpdate, now);
		lastUpdate = now;
		if (elapsed > MaxFrameDelta)
			elapsed = MaxFrameDelta;
		if (elapsed < TimeSpan.Zero)
			elapsed = TimeSpan.Zero;
		var seconds = elapsed.TotalSeconds * SpeedModifier;
		var enabled = AnimationsEnabled;
		var ticked = animations.Count;

		ExceptionDispatchInfo? error = null;
		frame.Clear();
		frame.AddRange(animations);
		try
		{
			foreach (var animation in frame)
			{
				if (!animation.IsInManager)
					continue;
				try
				{
					if (animation.HasFinished || animation.IsDisposed)
					{
						RemoveCore(animation);
						continue;
					}

					if (!enabled)
					{
						RemoveCore(animation);
						animation.FinishImmediately();
						animation.RaiseCompleted();
						continue;
					}

					animation.Tick(seconds);
					if (animation.HasFinished)
						RemoveCore(animation);
				}
				catch (Exception ex)
				{
					RemoveCore(animation);
					error ??= ExceptionDispatchInfo.Capture(ex);
				}
			}
		}
		finally
		{
			frame.Clear();
		}

		if (animations.Count == 0)
			End();

		frameTicked?.Invoke(new AnimationFrameInfo(ticked, elapsed, Stopwatch.GetElapsedTime(started)));

		error?.Throw();
	}
}

/// <summary>Per-frame diagnostics reported by <see cref="AnimationManager.FrameTicked"/>.</summary>
/// <param name="AnimationCount">Number of top-level animations ticked in the frame.</param>
/// <param name="FrameDelta">Elapsed (clamped) time since the previous frame, before <see cref="AnimationManager.SpeedModifier"/>.</param>
/// <param name="TickDuration">Wall-clock time spent ticking all animations (interpolation + <c>SetValue</c>), i.e. the per-frame cost on the UI thread.</param>
public readonly record struct AnimationFrameInfo(int AnimationCount, TimeSpan FrameDelta, TimeSpan TickDuration);
