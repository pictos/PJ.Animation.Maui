#if IOS || MACCATALYST
using CoreAnimation;
using Foundation;

namespace PJAnimation;

/// <summary>
/// Default ticker on iOS and Mac Catalyst. Port of Comet's <c>iOSTicker</c>: a <see cref="CADisplayLink"/> on the main
/// run loop (common modes, so it keeps firing while scrolling) fires once per display refresh on the UI thread.
/// </summary>
public class iOSTicker : Ticker
{
	CADisplayLink? link;

	/// <inheritdoc/>
	public override bool IsRunning => link is not null;

	/// <inheritdoc/>
	public override void Start()
	{
		if (link is not null)
			return;
		link = CADisplayLink.Create(OnFrame);
		link.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
	}

	/// <inheritdoc/>
	public override void Stop()
	{
		if (link is null)
			return;
		link.Invalidate();
		link.Dispose();
		link = null;
	}

	void OnFrame()
	{
		if (link is not null)
			Fire?.Invoke();
	}
}
#elif ANDROID
using System;
using Android.Animation;

namespace PJAnimation;

/// <summary>
/// Default ticker on Android. Port of Comet's <c>AndroidTicker</c>: an infinitely repeating <see cref="ValueAnimator"/>
/// whose update callback fires once per <c>Choreographer</c> frame on the UI thread.
/// </summary>
/// <remarks>
/// Battery saver and an animator duration scale of 0 are reported as reduced motion (see
/// <see cref="SystemReducedMotionProvider"/>), which makes animations jump to their end state. When animations are
/// forced on anyway (<see cref="AnimationManager.RespectReducedMotion"/> = <see langword="false"/>) and the system has
/// animators disabled, the ticker falls back to the <see cref="Ticker"/> timer.
/// </remarks>
public class AndroidTicker : Ticker
{
	ValueAnimator? animator;
	bool usingTimer;

	/// <inheritdoc/>
	public override bool IsRunning => usingTimer ? base.IsRunning : animator?.IsStarted == true;

	/// <inheritdoc/>
	public override void Start()
	{
		if (IsRunning)
			return;

		if (OperatingSystem.IsAndroidVersionAtLeast(26) && !ValueAnimator.AreAnimatorsEnabled())
		{
			usingTimer = true;
			base.Start();
			return;
		}

		usingTimer = false;
		if (animator is null)
		{
			animator = ValueAnimator.OfInt(0, 100)!;
			animator.RepeatCount = ValueAnimator.Infinite;
			animator.Update += OnUpdate;
		}
		animator.Start();
	}

	/// <inheritdoc/>
	public override void Stop()
	{
		if (usingTimer)
		{
			usingTimer = false;
			base.Stop();
		}
		animator?.Cancel();
	}

	void OnUpdate(object? sender, ValueAnimator.AnimatorUpdateEventArgs e)
	{
		if (animator?.IsStarted == true)
			Fire?.Invoke();
	}
}
#endif
