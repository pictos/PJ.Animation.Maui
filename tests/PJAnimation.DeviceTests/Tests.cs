#if ANDROID
using PlatformView = Android.Views.View;
#elif IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#endif

namespace PJAnimation.DeviceTests;

/// <summary>Checks that need real handlers / platform views. Each returns an informational detail or throws on failure.</summary>
static class Tests
{
	public static async Task<string?> DefaultTickerDrivesFrames(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "ticker" });
#if IOS || MACCATALYST
		var expected = typeof(iOSTicker);
#elif ANDROID
		var expected = typeof(AndroidTicker);
#else
		var expected = typeof(Ticker);
#endif
		Check(AnimationManager.Ticker.GetType() == expected, $"Default ticker is {AnimationManager.Ticker.GetType().Name}, expected {expected.Name}.");

		label.Animate(l => l.TranslationX = 50, duration: .5);
		await NextFrames(3);
		Check(AnimationManager.Ticker.IsRunning, "PJAnimation ticker not running.");
		await WaitFor(() => label.Animations.Count == 0, 3000, "animation did not finish");
		Check(Math.Abs(label.TranslationX - 50) < 0.001, $"TranslationX={label.TranslationX}, expected 50.");
		Check(!AnimationManager.Ticker.IsRunning, "Ticker still running after the last animation finished.");
		return $"ticker={AnimationManager.Ticker.GetType().Name}";
	}

	public static async Task<string?> TicksRunOnUIThread(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "ticks" });
		var frames = 0;
		var offThread = 0;
		var deltas = new List<double>();
		var ticks = new List<double>();
		void OnFrame(AnimationFrameInfo info)
		{
			frames++;
			if (!MainThread.IsMainThread)
				offThread++;
			deltas.Add(info.FrameDelta.TotalMilliseconds);
			ticks.Add(info.TickDuration.TotalMilliseconds);
		}

		var completedOnMain = new TaskCompletionSource<bool>();
		AnimationManager.FrameTicked += OnFrame;
		try
		{
			label.Animate(l => { l.TranslationX = 100; l.Opacity = .5; l.BackgroundColor = Colors.Red; },
				() => completedOnMain.TrySetResult(MainThread.IsMainThread), duration: 1);
			Check(await completedOnMain.Task.WaitAsync(TimeSpan.FromSeconds(5)), "completed callback ran off the UI thread.");
		}
		finally
		{
			AnimationManager.FrameTicked -= OnFrame;
		}

		Check(offThread == 0, $"{offThread}/{frames} frames ticked off the UI thread.");
		Check(frames >= 10, $"only {frames} frames in 1 s.");
		Check(label.TranslationX == 100 && label.Opacity == .5, "end values not applied.");
		return $"frames={frames} avgDelta={deltas.Average():F1}ms maxDelta={deltas.Max():F1}ms avgTick={ticks.Average():F3}ms maxTick={ticks.Max():F3}ms";
	}

	public static async Task<string?> CaptureAndRestoreLeavesPlatformValueUnchangedBeforeFirstTick(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "flash", Opacity = 1 });
		Check(PlatformOpacity(label) == 1, "precondition: platform alpha should be 1.");

		// Observe the platform value while the user action runs: the mapper pushes the final value synchronously,
		// then the capture restores the old one before returning (no frame can be rendered in between).
		var nativeDuringAction = double.NaN;
		label.PropertyChanged += OnChanged;
		void OnChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(VisualElement.Opacity) && double.IsNaN(nativeDuringAction))
				nativeDuringAction = PlatformOpacity(label);
		}

		var firstFrame = new TaskCompletionSource<(double Opacity, double TranslationX)>();
		void OnFrame(AnimationFrameInfo info) => firstFrame.TrySetResult((PlatformOpacity(label), PlatformTranslationX(label)));
		try
		{
			label.Animate(l => { l.Opacity = 0; l.TranslationX = 200; }, duration: 1);
			label.PropertyChanged -= OnChanged;
			var opacityAfterCall = PlatformOpacity(label);
			var translationAfterCall = PlatformTranslationX(label);
			Check(opacityAfterCall == 1, $"platform alpha after Animate returned = {opacityAfterCall}, expected 1 (flash).");
			Check(Math.Abs(translationAfterCall) < .01, $"platform translationX after Animate returned = {translationAfterCall}, expected 0 (flash).");

			AnimationManager.FrameTicked += OnFrame;
			var (opacity, translation) = await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(3));
			Check(opacity > .8 && opacity <= 1, $"first frame alpha = {opacity}, expected close to 1.");
			Check(translation >= 0 && translation < 40, $"first frame translationX = {translation}, expected close to 0.");
			return $"nativeAlphaDuringAction={nativeDuringAction:F2} afterCall={opacityAfterCall:F2} firstFrame alpha={opacity:F3} tx={translation:F2}";
		}
		finally
		{
			label.PropertyChanged -= OnChanged;
			AnimationManager.FrameTicked -= OnFrame;
		}
	}

	public static async Task<string?> UnloadedPausesAndLoadedResumes(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "lifecycle" });
		label.Animate(l => l.TranslationX = 100, duration: 2, repeats: true);
		await Task.Delay(200);
		var animation = label.Animations.Single();
		Check(!animation.IsPaused, "should be running.");

		page.Host.Remove(label);
		await WaitFor(() => animation.IsPaused, 3000, "Unloaded did not pause the animation.");
		var frozen = label.TranslationX;
		await Task.Delay(300);
		Check(label.TranslationX == frozen, "animation kept running while unloaded.");

		page.Host.Add(label);
		await WaitFor(() => !animation.IsPaused, 3000, "Loaded did not resume the animation.");
		await Task.Delay(300);
		Check(label.TranslationX != frozen, "animation did not advance after resume.");
		animation.Dispose();
		return $"frozenAt={frozen:F1}";
	}

	public static async Task<string?> ExplicitPauseSurvivesReload(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "explicit pause" });
		label.Animate(l => l.TranslationX = 100, duration: 2, repeats: true);
		await Task.Delay(100);
		label.PauseAnimations();
		var animation = label.Animations.Single();
		page.Host.Remove(label);
		await WaitFor(() => !label.IsLoaded, 3000, "not unloaded");
		page.Host.Add(label);
		await WaitFor(() => label.IsLoaded, 3000, "not reloaded");
		await Task.Delay(300);
		Check(animation.IsPaused, "explicitly paused animation was resumed by Loaded.");
		label.ResumeAnimations();
		Check(!animation.IsPaused, "ResumeAnimations did not resume.");
		animation.Dispose();
		return null;
	}

	public static async Task<string?> ReducedMotionJumpsToEndOnPlatformView(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "reduced motion" });
		AnimationManager.ReducedMotionProvider = new AlwaysReduced();
		var completed = false;
		label.Animate(l => l.Opacity = .25, () => completed = true, duration: 1);
		Check(completed, "completed not invoked synchronously.");
		Check(Math.Abs(PlatformOpacity(label) - .25) < .001, $"platform alpha = {PlatformOpacity(label)}, expected 0.25.");
		await Task.Yield();
		return $"system reduced motion = {SystemReducedMotionProvider.Instance.IsReducedMotionEnabled}";
	}

	public static async Task<string?> BackgroundThreadAnimateIsDispatched(HarnessPage page)
	{
		var label = await AddLoaded(page, new Label { Text = "background" });
		var actionOnMain = false;
		var completed = new TaskCompletionSource<bool>();
		await Task.Run(() => label.Animate(l => { actionOnMain = MainThread.IsMainThread; l.Opacity = .5; },
			() => completed.TrySetResult(MainThread.IsMainThread), duration: .2));
		Check(await completed.Task.WaitAsync(TimeSpan.FromSeconds(3)), "completed ran off the UI thread.");
		Check(actionOnMain, "action ran off the UI thread.");
		return null;
	}

	sealed class AlwaysReduced : IReducedMotionProvider
	{
		public bool IsReducedMotionEnabled => true;
	}

	static async Task<T> AddLoaded<T>(HarnessPage page, T view) where T : View
	{
		page.Host.Add(view);
		await WaitFor(() => view.IsLoaded && view.Handler is not null, 3000, "view not loaded");
		return view;
	}

	static async Task NextFrames(int count)
	{
		for (var i = 0; i < count; i++)
			await Task.Delay(20);
	}

	static async Task WaitFor(Func<bool> condition, int timeoutMs, string message)
	{
		var deadline = Environment.TickCount64 + timeoutMs;
		while (!condition())
		{
			if (Environment.TickCount64 > deadline)
				throw new TimeoutException(message);
			await Task.Delay(16);
		}
	}

	static void Check(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

#if ANDROID || IOS || MACCATALYST
	// Transforms/alpha are applied to the container view when there is one.
	static PlatformView GetPlatformView(VisualElement view)
	{
		var handler = (IPlatformViewHandler)(view.Handler ?? throw new InvalidOperationException("No handler."));
		return handler.ContainerView ?? handler.PlatformView ?? throw new InvalidOperationException("No platform view.");
	}
#endif

	static double PlatformOpacity(VisualElement view)
	{
		var platform = GetPlatformView(view);
#if ANDROID
		return platform.Alpha;
#elif IOS || MACCATALYST
		return platform.Alpha;
#else
		throw new PlatformNotSupportedException();
#endif
	}

	static double PlatformTranslationX(VisualElement view)
	{
		var platform = GetPlatformView(view);
#if ANDROID
		return platform.TranslationX / (platform.Context?.Resources?.DisplayMetrics?.Density ?? 1);
#elif IOS || MACCATALYST
		return platform.Layer.Transform.M41;
#else
		throw new PlatformNotSupportedException();
#endif
	}
}
