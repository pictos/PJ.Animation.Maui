using PJAnimation;

namespace PJAnimation.Sample.Pages;

/// <summary>Port of Comet's sample/Comet.Samples/AnimationSample.cs to MAUI Controls.</summary>
public class AnimationSamplePage : ContentPage
{
	bool shouldAnimate = true;

	public AnimationSamplePage()
	{
		Title = "AnimationSample";

		var button = new Button { Text = "Pause", BackgroundColor = Colors.Blue, TextColor = Colors.White };

		var animatedText = new Label
		{
			Text = "Text to Animate!",
			BackgroundColor = Colors.Orange,
			TextColor = Colors.Blue,
			FontSize = 10,
			// MAUI defaults FontAutoScalingEnabled to true, which makes the iOS font manager round the point size
			// to whole points (UIFontMetrics.GetScaledFont): the animated value advances every frame but the rendered
			// font (and the label frame the background is drawn in) only steps ~4x/s. See README "Performance guidance".
			FontAutoScalingEnabled = false,
		}
		.Animate(duration: 3, repeats: true, autoReverses: true, action: text =>
		{
			text.BackgroundColor = Colors.Blue;
			text.TextColor = Colors.Orange;
			text.FontSize = 30;
		});

		// Comet mutates `button` from inside the steps; in PJAnimation objects outside the view's
		// visual tree must be listed in alsoCapture so their changes are animated (not applied instantly).
		var sequenced = new Label { Text = "Regular Text Above..." }
			.BeginAnimationSequence(repeats: true, alsoCapture: [button])
			.Animate(duration: 1, action: text =>
			{
				text.BackgroundColor = Colors.Fuchsia;
				button.BackgroundColor = Colors.Green;
			}).Animate(duration: 1, action: text =>
			{
				text.BackgroundColor = Colors.AliceBlue;
			}).Animate(duration: 1, action: text =>
			{
				text.BackgroundColor = Colors.Beige;
				button.BackgroundColor = Colors.Blue;
			}).Animate(duration: 1, action: text =>
			{
				text.BackgroundColor = Colors.BlueViolet;
			}).Animate(duration: 1, action: text =>
			{
				text.BackgroundColor = Colors.Lavender;
			}).Animate(duration: 1, action: text =>
			{
				text.BackgroundColor = Colors.Fuchsia;
				button.BackgroundColor = Colors.Green;
			}).EndAnimationSequence();

		// PJAnimation addition (not in Comet): await instead of passing a completed callback.
		var awaitButton = new Button { Text = "Await a two-step move", BackgroundColor = Colors.DarkSlateGray, TextColor = Colors.White };
		awaitButton.Clicked += async (_, _) =>
		{
			awaitButton.IsEnabled = false;
			await awaitButton.AnimateAsync(b => b.TranslationX = 120, duration: .4);
			await awaitButton.AnimateAsync(Easing.SpringOut, b => b.TranslationX = 0, duration: .6);
			awaitButton.IsEnabled = true;
		};

		var stack = new VerticalStackLayout
		{
			Padding = 20,
			Spacing = 12,
			Children =
			{
				new Label { Text = "Regular Text Behind..." },
				animatedText,
				sequenced,
				button,
				awaitButton,
			},
		};

		button.Clicked += (_, _) =>
		{
			shouldAnimate = !shouldAnimate;
			button.Text = shouldAnimate ? "Pause" : "Resume";
			// Cascades to every animation registered on the stack and its descendants.
			if (shouldAnimate)
				stack.ResumeAnimations();
			else
				stack.PauseAnimations();
		};

		Content = stack;
	}
}
