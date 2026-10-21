using System.Runtime.CompilerServices;

namespace PJAnimation.Tests;

public class MemoryTests : TestBase
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	static WeakReference StartRepeatingAnimation()
	{
		var page = new ContentPage { Content = new VerticalStackLayout { new Label() } };
		var label = (Label)((VerticalStackLayout)page.Content).Children[0];
		label.Animate(l => { l.Opacity = 0; l.TranslationX = 10; }, duration: 1, repeats: true, autoReverses: true);
		page.BeginAnimationSequence(repeats: true)
			.Animate(p => p.Opacity = .5, duration: 1)
			.Animate(p => p.Opacity = 1, duration: 1)
			.EndAnimationSequence();
		return new WeakReference(page);
	}

	[Fact]
	public void AnimatedViewsCanBeCollected()
	{
		var weak = StartRepeatingAnimation();
		Run(.5);
		Assert.Equal(2, AnimationManager.Count);

		for (var i = 0; i < 5 && weak.IsAlive; i++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
		Assert.False(weak.IsAlive);

		Run(.1);
		Assert.Equal(0, AnimationManager.Count);
		Assert.False(Ticker.IsRunning);
	}
}

public class TickerTests : TestBase
{
	[Fact]
	public void ManualTickerOnlyFiresWhileRunning()
	{
		var ticker = new ManualTicker();
		var fired = 0;
		ticker.Fire = () => fired++;
		ticker.Tick();
		Assert.Equal(0, fired);
		ticker.Start();
		ticker.Tick();
		Assert.Equal(1, fired);
		Assert.Equal(1, ticker.StartCount);
	}

	[Fact]
	public void TickerStartsOnAddAndStopsWhenEmpty()
	{
		var label = new Label();
		Assert.False(Ticker.IsRunning);
		label.Animate(l => l.TranslationX = 1, duration: .1);
		Assert.True(Ticker.IsRunning);
		Run(.2);
		Assert.False(Ticker.IsRunning);
	}

	[Fact]
	public void BaseTickerFiresOnDispatcherAndCoalescesFrames()
	{
		TestDispatcher.Instance.Pump();
		var ticker = new Ticker { MaxFps = 1000 };
		var fired = 0;
		ticker.Fire = () => fired++;
		ticker.Start();
		Assert.True(ticker.IsRunning);
		Assert.True(System.Threading.SpinWait.SpinUntil(() => !TestDispatcher.Instance.Queue.IsEmpty, 2000));
		System.Threading.Thread.Sleep(50);
		// Many timer ticks, but only one frame is waiting for the UI thread.
		Assert.Single(TestDispatcher.Instance.Queue);
		Assert.Equal(0, fired);
		TestDispatcher.Instance.Pump();
		Assert.Equal(1, fired);
		Assert.True(System.Threading.SpinWait.SpinUntil(() => !TestDispatcher.Instance.Queue.IsEmpty, 2000));
		ticker.Stop();
		Assert.False(ticker.IsRunning);
		// A frame posted before Stop is discarded.
		TestDispatcher.Instance.Pump();
		Assert.Equal(1, fired);
	}
}
