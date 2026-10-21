using Stopwatch = System.Diagnostics.Stopwatch;
using PJAnimation;

namespace PJAnimation.Sample.Pages;

/// <summary>
/// Animates N views either with transform-only properties (TranslationX/Rotation/Scale/Opacity: no layout pass)
/// or layout-affecting ones (WidthRequest/HeightRequest/Margin: re-measure every frame) and shows a HUD with the
/// per-frame tick cost measured by <see cref="AnimationManager.FrameTicked"/> and the tick rate.
/// </summary>
public class StressPage : ContentPage
{
	static readonly int[] Counts = [1, 10, 50, 200];

	readonly Picker countPicker = new() { Title = "N", ItemsSource = Counts, SelectedIndex = 1 };
	readonly Switch layoutSwitch = new();
	readonly Label hud = new() { FontFamily = "Courier", FontSize = 12 };
	readonly AbsoluteLayout arena = new() { HeightRequest = 420, BackgroundColor = Color.FromArgb("#F2F2F7") };
	readonly IDispatcherTimer hudTimer;

	int frames;
	long tickTicks;
	long maxTickTicks;
	int lastCount;
	long windowStart = Stopwatch.GetTimestamp();

	public StressPage()
	{
		Title = "Stress";

		countPicker.SelectedIndexChanged += (_, _) => Rebuild();
		layoutSwitch.Toggled += (_, _) => Rebuild();

		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Padding = 12,
				Spacing = 8,
				Children =
				{
					new HorizontalStackLayout
					{
						Spacing = 8,
						Children =
						{
							new Label { Text = "Views:", VerticalOptions = LayoutOptions.Center },
							countPicker,
							new Label { Text = "Layout-affecting:", VerticalOptions = LayoutOptions.Center },
							layoutSwitch,
						},
					},
					hud,
					arena,
				},
			},
		};

		hudTimer = Dispatcher.CreateTimer();
		hudTimer.Interval = TimeSpan.FromSeconds(1);
		hudTimer.Tick += (_, _) => UpdateHud();

		Loaded += (_, _) =>
		{
			AnimationManager.FrameTicked += OnFrameTicked;
			hudTimer.Start();
			Rebuild();
		};
		Unloaded += (_, _) =>
		{
			AnimationManager.FrameTicked -= OnFrameTicked;
			hudTimer.Stop();
			Clear();
		};
	}

	// Runs on the UI thread once per frame; keep it allocation-free.
	void OnFrameTicked(AnimationFrameInfo info)
	{
		frames++;
		var ticks = info.TickDuration.Ticks;
		tickTicks += ticks;
		if (ticks > maxTickTicks)
			maxTickTicks = ticks;
		lastCount = info.AnimationCount;
	}

	void UpdateHud()
	{
		var elapsed = Stopwatch.GetElapsedTime(windowStart).TotalSeconds;
		var fps = frames / elapsed;
		var avg = frames == 0 ? 0 : TimeSpan.FromTicks(tickTicks / frames).TotalMilliseconds;
		var max = TimeSpan.FromTicks(maxTickTicks).TotalMilliseconds;
		hud.Text = $"mode={(layoutSwitch.IsToggled ? "layout" : "transform")} views={arena.Children.Count} running={lastCount}\n" +
			$"ticks/s={fps,6:F1}  tick avg={avg,6:F3}ms  max={max,6:F3}ms\n" +
			"(tick = PJAnimation work only; use platform tools for render cost — see docs/perf-validation.md)";
		frames = 0;
		tickTicks = 0;
		maxTickTicks = 0;
		windowStart = Stopwatch.GetTimestamp();
	}

	void Clear()
	{
		foreach (var child in arena.Children)
		{
			if (child is VisualElement element)
			{
				foreach (var animation in element.GetAnimations())
					element.RemoveAnimation(animation);
			}
		}
		arena.Children.Clear();
	}

	void Rebuild()
	{
		if (!IsLoaded)
			return;

		Clear();
		var count = Counts[Math.Max(0, countPicker.SelectedIndex)];
		var layout = layoutSwitch.IsToggled;
		var columns = (int)Math.Ceiling(Math.Sqrt(count));
		const double cell = 24;

		for (var i = 0; i < count; i++)
		{
			var box = new BoxView
			{
				Color = Color.FromHsla(i / (double)count, .7, .5),
				WidthRequest = cell - 4,
				HeightRequest = cell - 4,
			};
			AbsoluteLayout.SetLayoutBounds(box, new Rect((i % columns) * cell, (i / columns) * cell, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
			arena.Children.Add(box);

			var duration = 1 + (i % 7) * .15;
			if (layout)
			{
				box.Animate(Easing.CubicInOut, b =>
				{
					b.WidthRequest = cell * 2.5;
					b.HeightRequest = cell * 1.5;
					b.Margin = new Thickness(cell, cell / 2, 0, 0);
				}, duration: duration, repeats: true, autoReverses: true);
			}
			else
			{
				box.Animate(Easing.CubicInOut, b =>
				{
					b.TranslationX = cell * 3;
					b.TranslationY = cell;
					b.Rotation = 180;
					b.Scale = 1.5;
					b.Opacity = .4;
				}, duration: duration, repeats: true, autoReverses: true);
			}
		}
	}
}
