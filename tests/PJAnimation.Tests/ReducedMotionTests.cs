namespace PJAnimation.Tests;

public class ReducedMotionTests : TestBase
{
	[Fact]
	public void ReducedMotionJumpsToEndAndInvokesCompleted()
	{
		ReducedMotion.IsReducedMotionEnabled = true;
		var label = new Label();
		var completed = 0;
		label.Animate(l => { l.TranslationX = 100; l.TextColor = Colors.Red; }, () => completed++, duration: 1);
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(Colors.Red, label.TextColor);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
		Assert.False(Ticker.IsRunning);
		Assert.Empty(label.Animations);
	}

	[Fact]
	public void RepeatingAnimationAppliesEndStateOnce()
	{
		ReducedMotion.IsReducedMotionEnabled = true;
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1, repeats: true);
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void RepeatingSequenceAppliesEndStateOnce()
	{
		ReducedMotion.IsReducedMotionEnabled = true;
		var label = new Label();
		var steps = 0;
		var completed = 0;
		label.BeginAnimationSequence(() => completed++, repeats: true)
			.Animate(l => l.TranslationX = 50, () => steps++, duration: 1)
			.Animate(l => l.TranslationX = 100, () => steps++, duration: 1)
			.EndAnimationSequence();
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(2, steps);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void ReducedMotionTurnedOnMidAnimationFinishesOnNextFrame()
	{
		var label = new Label();
		var completed = 0;
		label.Animate(l => l.TranslationX = 100, () => completed++, duration: 1);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
		ReducedMotion.IsReducedMotionEnabled = true;
		Frame(.01);
		Assert.Equal(100, label.TranslationX);
		Assert.Equal(1, completed);
		Assert.False(Ticker.IsRunning);
	}

	[Fact]
	public void OptOutKeepsAnimating()
	{
		ReducedMotion.IsReducedMotionEnabled = true;
		AnimationManager.RespectReducedMotion = false;
		try
		{
			var label = new Label();
			label.Animate(l => l.TranslationX = 100, duration: 1);
			Run(.5);
			Assert.Equal(50, label.TranslationX, 6);
			Assert.True(AnimationManager.AnimationsEnabled);
		}
		finally
		{
			AnimationManager.RespectReducedMotion = true;
		}
	}

	[Fact]
	public void ProviderIsQueriedOncePerFrame()
	{
		var label = new Label();
		label.Animate(l => { l.TranslationX = 100; l.TranslationY = 100; l.Opacity = 0; }, duration: 1);
		label.Animate(l => l.Rotation = 90, duration: 1);
		var before = ReducedMotion.Queries;
		Frame(.1);
		Assert.Equal(before + 1, ReducedMotion.Queries);
	}

	[Fact]
	public void SystemProviderIsFalseOnNet()
	{
		Assert.False(SystemReducedMotionProvider.Instance.IsReducedMotionEnabled);
		Assert.Throws<ArgumentNullException>(() => AnimationManager.ReducedMotionProvider = null!);
	}
}

public class DiagnosticsTests : TestBase
{
	[Fact]
	public void FrameTickedReportsCountDeltaAndCost()
	{
		var frames = new List<AnimationFrameInfo>();
		void Handler(AnimationFrameInfo info) => frames.Add(info);
		AnimationManager.FrameTicked += Handler;
		try
		{
			var label = new Label();
			label.Animate(l => l.TranslationX = 100, duration: .1);
			label.Animate(l => l.Opacity = 0, duration: 1);
			Frame(.05);
			Frame(.5);
			Frame(1);
		}
		finally
		{
			AnimationManager.FrameTicked -= Handler;
		}

		Assert.Equal(3, frames.Count);
		Assert.Equal(2, frames[0].AnimationCount);
		Assert.Equal(TimeSpan.FromSeconds(.05), frames[0].FrameDelta);
		Assert.Equal(2, frames[1].AnimationCount);
		Assert.Equal(TimeSpan.FromMilliseconds(100), frames[1].FrameDelta);
		Assert.True(frames[0].TickDuration >= TimeSpan.Zero);
	}

	[Fact]
	public void NoSubscribersNoEvents()
	{
		var label = new Label();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Frame(.1);
		Assert.Equal(10, label.TranslationX, 6);
	}
}
