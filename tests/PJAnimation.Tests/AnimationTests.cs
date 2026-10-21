namespace PJAnimation.Tests;

// DoubleAnimatesOverTime ported from Comet tests/Comet.Tests/Animations/AnimationTests.cs.
public class AnimationTests : TestBase
{
	[Fact]
	public void DoubleAnimatesOverTime()
	{
		var animation = new Animation
		{
			StartValue = 0.0,
			EndValue = 1.0,
			Easing = Easing.Linear,
		};

		foreach (var progress in new[] { .1, .2, .33333, .75, .99, 1 })
		{
			animation.Update(progress);
			Assert.Equal(progress, animation.CurrentValue);
		}
	}

	[Fact]
	public void DefaultEasingIsLinear()
	{
		Assert.Same(Easing.Linear, new Animation().Easing);
		Assert.Same(Easing.Linear, new AnimationStep<Label>().Easing);

		var label = new Label { Opacity = 1 };
		var animation = AnimationExtensions.CreateAnimation(label, null!, l => l.Opacity = 0, duration: 1)!;
		Assert.Same(Easing.Linear, animation.Easing);

		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
	}

	[Fact]
	public void EasingIsAppliedBeforeLerp()
	{
		var animation = new Animation { StartValue = 0.0, EndValue = 10.0, Easing = Easing.CubicIn };
		animation.Update(.5);
		Assert.Equal(10 * Easing.CubicIn.Ease(.5), (double)animation.CurrentValue!, 10);

		var label = new Label();
		label.Animate(Easing.CubicIn, l => l.TranslationX = 100, duration: 1);
		Run(.5);
		Assert.Equal(12.5, label.TranslationX, 6);
	}

	[Fact]
	public void UpdateLandsExactlyOnEndValue()
	{
		var animation = new Animation { StartValue = .1, EndValue = .3, Easing = Easing.SpringOut };
		animation.Update(1);
		Assert.Equal(.3, animation.CurrentValue);
		Assert.True(animation.HasFinished);
	}

	[Fact]
	public void ZeroDurationFinishesOnFirstTick()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 10, () => completed++, duration: 0);
		Assert.Equal(0, label.TranslationX);
		Frame(.016);
		Assert.Equal(10, label.TranslationX);
		Assert.Equal(1, completed);
	}

	[Fact]
	public void CreateAutoReversingMirrorsComet()
	{
		var animation = new Animation { StartValue = 0.0, EndValue = 1.0, Duration = 1, StartDelay = .5, Repeats = true };
		var parent = animation.CreateAutoReversing();
		Assert.True(parent.Repeats);
		Assert.False(animation.Repeats);
		Assert.Equal(2.5, parent.Duration);
	}
}
