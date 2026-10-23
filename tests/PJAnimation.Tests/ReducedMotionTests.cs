namespace PJAnimation.Tests;

public class ReducedMotionTests : TestBase
{
	[Fact]
	public void SystemProviderIsFalseOnNet()
	{
		Assert.False(SystemReducedMotionProvider.Instance.IsReducedMotionEnabled);
		Assert.True(AnimationManager.AnimationsEnabled);
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
