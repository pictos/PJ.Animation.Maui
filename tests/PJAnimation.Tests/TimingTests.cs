namespace PJAnimation.Tests;

public class TimingTests : TestBase
{
	[Fact]
	public void DelayIsHonored()
	{
		var label = new Label { Opacity = 1 };
		label.Animate(l => l.Opacity = 0, duration: 1, delay: .5);
		Run(.4);
		Assert.Equal(1, label.Opacity);
		Run(.6);
		Assert.Equal(.5, label.Opacity, 6);
		Run(.5);
		Assert.Equal(0, label.Opacity);
	}

	[Fact]
	public void CompletedIsInvokedOnce()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 10, () => completed++, duration: .2);
		Run(.1);
		Assert.Equal(0, completed);
		Run(.5);
		Assert.Equal(1, completed);
		Assert.Empty(label.Animations);
	}

	[Fact]
	public void GroupCompletedIsInvokedOnce()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => { l.TranslationX = 10; l.TranslationY = 10; }, () => completed++, duration: .2);
		Run(1);
		Assert.Equal(1, completed);
	}

	[Fact]
	public void RepeatsForever()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1, repeats: true);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
		Run(.5);
		Assert.Equal(100, label.TranslationX, 6);
		Run(.5);
		Assert.InRange(label.TranslationX, 40, 60);
		Run(10);
		Assert.Equal(0, completed);
		Assert.Equal(1, AnimationManager.Count);
		Assert.True(Ticker.IsRunning);
	}

	[Fact]
	public void AutoReversesBackToStart()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1, autoReverses: true);
		Assert.Equal(0, label.TranslationX);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
		Run(.5);
		Assert.Equal(100, label.TranslationX, 6);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
		Assert.Equal(0, completed);
		Run(.5);
		Assert.Equal(0, label.TranslationX, 6);
		Run(.1);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void AutoReversesAndRepeats()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1, repeats: true, autoReverses: true);
		Run(1.5);
		Assert.Equal(50, label.TranslationX, 6);
		Run(.5);
		Run(.5);
		Assert.InRange(label.TranslationX, 40, 60);
		Assert.Equal(1, AnimationManager.Count);
	}

	[Fact]
	public void PauseAndResumeDoNotJump()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.5);
		var animation = Assert.Single(label.Animations);
		animation.Pause();
		Assert.True(animation.IsPaused);
		Assert.False(Ticker.IsRunning);
		Run(5);
		Assert.Equal(50, label.TranslationX, 6);

		Clock.Advance(30);
		animation.Resume();
		Assert.True(Ticker.IsRunning);
		Run(.1);
		Assert.Equal(60, label.TranslationX, 6);
	}

	[Fact]
	public void PauseAnimationsCascadesToDescendants()
	{
		var child = new Label();
		var page = new ContentPage { Content = new VerticalStackLayout { child } };
		child.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.5);
		page.PauseAnimations();
		Run(.5);
		Assert.Equal(50, child.TranslationX, 6);
		page.ResumeAnimations();
		Run(.5);
		Assert.Equal(100, child.TranslationX, 6);
	}

	[Fact]
	public void DisposeStopsWithoutCompleting()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1);
		Run(.5);
		var animation = Assert.Single(label.Animations);
		animation.Dispose();
		Assert.True(animation.IsDisposed);
		Run(1);
		Assert.Equal(50, label.TranslationX, 6);
		Assert.Equal(0, completed);
		Assert.Equal(0, AnimationManager.Count);
		Assert.Empty(label.Animations);
	}

	[Fact]
	public void RemoveAnimationStopsIt()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.25);
		label.RemoveAnimation(Assert.Single(label.Animations));
		Run(1);
		Assert.Equal(25, label.TranslationX, 6);
		Assert.Empty(label.Animations);
	}

	[Fact]
	public void NewAnimationOnSameTrackWinsFromCurrentValue()
	{
		var label = new Label { Opacity = 1 };
		var firstCompleted = 0;
		label.Animate(l => { l.Opacity = 0; l.TranslationX = 100; }, () => firstCompleted++, duration: 1);
		Run(.5);
		var first = Assert.Single(label.Animations);
		Assert.Equal(.5, label.Opacity, 6);

		label.Animate(l => l.Opacity = 1, duration: 1);
		var second = label.Animations[1];
		Assert.Equal(.5, (double)second.StartValue!, 6);

		Run(.5);
		Assert.Equal(.75, label.Opacity, 6);
		Assert.Equal(100, label.TranslationX, 6);
		Assert.True(first.HasFinished);
		Assert.False(first.IsCancelled);
		Assert.True(first.Children[0].IsCancelled);
		Assert.Equal(1, firstCompleted);

		Run(.5);
		Assert.Equal(1, label.Opacity, 6);
	}

	[Fact]
	public void FullyReplacedAnimationIsCancelled()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1, repeats: true);
		Run(.5);
		var first = Assert.Single(label.Animations);
		label.Animate(l => l.TranslationX = 0, duration: 1);
		Run(.05);
		Assert.True(first.IsCancelled);
		Assert.True(first.HasFinished);
		Assert.Single(label.Animations);
		Run(1);
		Assert.Equal(0, label.TranslationX, 6);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void SpeedModifierScalesTime()
	{
		var label = new Label();
		AnimationManager.SpeedModifier = 2;
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.25);
		Assert.Equal(50, label.TranslationX, 6);
	}

	[Fact]
	public void LargeFrameDeltaIsClamped()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Frame(5);
		Assert.Equal(10, label.TranslationX, 6);
		AnimationManager.MaxFrameDelta = TimeSpan.FromMilliseconds(500);
		Frame(5);
		Assert.Equal(60, label.TranslationX, 6);
	}

	[Fact]
	public void SystemDisabledJumpsToEndOnAdd()
	{
		Ticker.IsSystemEnabled = false;
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1, repeats: true);
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
		Assert.False(Ticker.IsRunning);
	}

	[Fact]
	public void SystemDisabledAutoReverseEndsAtStart()
	{
		Ticker.IsSystemEnabled = false;
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1, autoReverses: true);
		Assert.Equal(0, label.TranslationX);
	}

	[Fact]
	public void SystemDisabledMidAnimationJumpsToEnd()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1);
		Run(.3);
		Ticker.IsSystemEnabled = false;
		Frame(.01);
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(1, completed);
		Assert.False(Ticker.IsRunning);
	}

	[Fact]
	public void FramesShareOneTimestamp()
	{
		var a = new Label();
		var b = new Label();
		a.Animate(l => l.TranslationX = 100, duration: 1);
		b.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.37, .37 / 7);
		Assert.Equal(a.TranslationX, b.TranslationX);
	}

	[Fact]
	public void SetTickerStopsPreviousAndContinues()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.5);
		var replacement = new ManualTicker();
		AnimationManager.SetTicker(replacement);
		Assert.False(Ticker.IsRunning);
		Assert.Null(Ticker.Fire);
		Assert.True(replacement.IsRunning);
		Clock.Advance(.1);
		replacement.Tick();
		Assert.Equal(60, label.TranslationX, 6);
	}

	[Fact]
	public void ExceptionInCompletedDoesNotBreakOtherAnimations()
	{
		var a = new Label();
		var b = new Label();
		a.Animate(l => l.TranslationX = 100, () => throw new InvalidOperationException("boom"), duration: .1);
		b.Animate(l => l.TranslationX = 100, duration: 1);
		Assert.Throws<InvalidOperationException>(() => Run(.1));
		Run(.9);
		Assert.Equal(100, b.TranslationX, 6);
	}
}
