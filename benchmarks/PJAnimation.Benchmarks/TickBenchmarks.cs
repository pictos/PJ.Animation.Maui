using BenchmarkDotNet.Attributes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PJAnimation;

namespace PJAnimation.Benchmarks;

/// <summary>
/// One <see cref="AnimationManager"/> frame (<see cref="ManualTicker.Tick"/>) with N running, repeating animations on
/// handler-less views: interpolation + BindableObject.SetValue + property-changed plumbing, without platform mapping.
/// </summary>
[MemoryDiagnoser]
public class TickBenchmarks
{
	public enum Kind
	{
		/// <summary>TranslationX + Rotation + Opacity (3 doubles).</summary>
		Transform,
		/// <summary>BackgroundColor (Color).</summary>
		Color,
	}

	readonly ManualTicker ticker = new();
	readonly List<Label> views = new();

	[Params(1, 10, 100, 1000)]
	public int N { get; set; }

	[Params(Kind.Transform, Kind.Color)]
	public Kind Properties { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		AnimationManager.TimeProvider = new SteppingClock(TimeSpan.FromSeconds(1 / 60.0));
		AnimationManager.RespectReducedMotion = false;
		AnimationManager.SetTicker(ticker);

		for (var i = 0; i < N; i++)
		{
			var label = new Label { BackgroundColor = Colors.Red };
			if (Properties == Kind.Transform)
				label.Animate(Easing.CubicInOut, l => { l.TranslationX = 100; l.Rotation = 90; l.Opacity = .5; }, duration: 1, repeats: true, autoReverses: true);
			else
				label.Animate(Easing.CubicInOut, l => l.BackgroundColor = Colors.Blue, duration: 1, repeats: true, autoReverses: true);
			views.Add(label);
		}

		// Warm up so every animation has captured its values and is in steady state.
		for (var i = 0; i < 10; i++)
			ticker.Tick();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		foreach (var view in views)
		{
			foreach (var animation in view.GetAnimations())
				view.RemoveAnimation(animation);
		}
		views.Clear();
	}

	[Benchmark]
	public void Frame() => ticker.Tick();

	/// <summary>Every call to <see cref="GetTimestamp"/> advances time by one frame.</summary>
	sealed class SteppingClock(TimeSpan step) : TimeProvider
	{
		long ticks;

		public override long TimestampFrequency => TimeSpan.TicksPerSecond;

		public override long GetTimestamp() => ticks += step.Ticks;
	}
}
