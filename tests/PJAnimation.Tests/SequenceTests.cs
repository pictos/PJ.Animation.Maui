namespace PJAnimation.Tests;

public class SequenceTests : TestBase
{
	[Fact]
	public void StepsRunSequentiallyAndCaptureLazily()
	{
		var label = new Label();
		var log = new List<string>();
		var returned = label.BeginAnimationSequence(() => log.Add("sequence"))
			.Animate(l => l.TranslationX = 100, () => log.Add("step1"), duration: 1)
			.Animate(l => l.TranslationX = l.TranslationX + 50, () => log.Add("step2"), duration: 1)
			.EndAnimationSequence();
		Assert.Same(label, returned);
		Assert.Equal(0, label.TranslationX);

		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
		Run(.55);
		Assert.Equal(["step1"], log);
		Run(.5);
		// Step 2 was captured when it started: from 100 to 150.
		Assert.InRange(label.TranslationX, 120, 130);
		Run(1);
		Assert.Equal(150, label.TranslationX, 6);
		Assert.Equal(["step1", "step2", "sequence"], log);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void SequenceCapturesExtraObjects()
	{
		var text = new Label();
		var button = new Button { Opacity = 1 };
		text.BeginAnimationSequence(alsoCapture: [button])
			.Animate(_ => button.Opacity = 0, duration: 1)
			.EndAnimationSequence();
		Run(.5);
		Assert.Equal(.5, button.Opacity, 6);
	}

	[Fact]
	public void SequenceDelayIsHonored()
	{
		var label = new Label();
		label.BeginAnimationSequence(delay: .5)
			.Animate(l => l.TranslationX = 100, duration: 1)
			.EndAnimationSequence();
		Run(.5);
		Assert.Equal(0, label.TranslationX);
		Run(.25);
		Assert.InRange(label.TranslationX, 20, 30);
	}

	[Fact]
	public void RepeatingSequenceWithoutChangesDoesNotHang()
	{
		var label = new Label();
		var stepCompleted = 0;
		label.BeginAnimationSequence(repeats: true)
			.Animate(_ => { }, () => stepCompleted++)
			.EndAnimationSequence();
		Run(.1);
		Assert.Equal(0, AnimationManager.Count);
		Assert.True(stepCompleted >= 1);
	}

	[Fact]
	public void RepeatingSequenceLoops()
	{
		var label = new Label { Opacity = 1 };
		label.BeginAnimationSequence(repeats: true)
			.Animate(l => l.Opacity = 0, duration: .5)
			.Animate(l => l.Opacity = 1, duration: .5)
			.EndAnimationSequence();
		Run(.25);
		Assert.Equal(.5, label.Opacity, 6);
		Run(3);
		Assert.Equal(1, AnimationManager.Count);
	}

	[Fact]
	public void SystemDisabledSequenceJumpsToEnd()
	{
		Ticker.IsSystemEnabled = false;
		var label = new Label();
		var completed = 0;
		label.BeginAnimationSequence(() => completed++)
			.Animate(l => l.TranslationX = 100, duration: 1)
			.Animate(l => l.TranslationY = 50, duration: 1)
			.EndAnimationSequence();
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(50, label.TranslationY);
		Assert.Equal(1, completed);
	}
}
