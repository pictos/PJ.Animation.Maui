using System.Threading;
using System.Threading.Tasks;

namespace PJAnimation.Tests;

public class AsyncTests : TestBase
{
	static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task CompletesWithFalseWhenFinished()
	{
		var label = new Label();
		var task = label.AnimateAsync(l => l.TranslationX = 100, duration: 1, cancellationToken: Ct);
		Assert.False(task.IsCompleted);

		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
		Assert.False(task.IsCompleted);

		Run(.6);
		Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
		Assert.Equal(100, label.TranslationX);
		Assert.Empty(label.Animations);
	}

	[Fact]
	public async Task UsesEasing()
	{
		var label = new Label();
		var task = label.AnimateAsync(Easing.CubicIn, l => l.TranslationX = 100, duration: 1, cancellationToken: Ct);
		Run(.5);
		Assert.Equal(100 * Easing.CubicIn.Ease(.5), label.TranslationX, 6);
		Run(.6);
		Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
	}

	[Fact]
	public async Task CompletesWithFalseWhenNothingChanged()
	{
		var label = new Label();
		Assert.False(await label.AnimateAsync(_ => { }, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct));
	}

	[Fact]
	public async Task CancellationTokenStopsInPlaceAndReturnsTrue()
	{
		var label = new Label();
		using var cts = new CancellationTokenSource();
		var task = label.AnimateAsync(l => l.TranslationX = 100, duration: 1, cancellationToken: cts.Token);
		Run(.5);

		cts.Cancel();
		Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
		Assert.Equal(50, label.TranslationX, 6);
		Assert.Empty(label.Animations);
		Assert.Equal(0, AnimationManager.Count);

		Run(1);
		Assert.Equal(50, label.TranslationX, 6);
	}

	[Fact]
	public async Task AlreadyCancelledTokenDoesNotRunTheAction()
	{
		var label = new Label();
		var ran = false;
		var result = await label.AnimateAsync(l => { ran = true; l.TranslationX = 100; }, cancellationToken: new CancellationToken(true));
		Assert.True(result);
		Assert.False(ran);
		Assert.Equal(0, label.TranslationX);
	}

	[Fact]
	public async Task NewerAnimationOfSamePropertyCancels()
	{
		var label = new Label();
		var first = label.AnimateAsync(l => l.TranslationX = 100, duration: 1, cancellationToken: Ct);
		Run(.5);
		var second = label.AnimateAsync(l => l.TranslationX = 0, duration: 1, cancellationToken: Ct);
		Run(.05);
		Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5), Ct));
		Assert.False(second.IsCompleted);
		Run(1);
		Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(5), Ct));
	}

	[Fact]
	public async Task DisposeCompletesWithTrue()
	{
		var label = new Label();
		var task = label.AnimateAsync(l => l.TranslationX = 100, duration: 1, cancellationToken: Ct);
		Run(.25);
		Assert.Single(label.Animations).Dispose();
		Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
	}

	[Fact]
	public async Task SystemDisabledCompletesImmediately()
	{
		Ticker.IsSystemEnabled = false;
		var label = new Label();
		Assert.False(await label.AnimateAsync(l => l.TranslationX = 100, duration: 1, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct));
		Assert.Equal(100, label.TranslationX);
	}

	[Fact]
	public async Task AutoReversingCompletesAfterBothLegs()
	{
		var label = new Label();
		var task = label.AnimateAsync(l => l.TranslationX = 100, duration: 1, autoReverses: true, cancellationToken: Ct);
		Run(1);
		Assert.False(task.IsCompleted);
		Run(1.1);
		Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
		Assert.Equal(0, label.TranslationX, 6);
	}

	[Fact]
	public async Task ActionExceptionFaultsTheTask()
	{
		var label = new Label();
		var task = label.AnimateAsync(_ => throw new InvalidOperationException("boom"), cancellationToken: Ct);
		await Assert.ThrowsAsync<InvalidOperationException>(() => task);
	}

	[Fact]
	public async Task DispatchesFromBackgroundThread()
	{
		var label = new Label();
		TestDispatcher.Instance.RequireDispatch = true;
		var task = label.AnimateAsync(l => l.TranslationX = 100, duration: 1, cancellationToken: Ct);
		Assert.Equal(0, label.TranslationX);
		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.Pump();
		Run(1.1);
		Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
		Assert.Equal(100, label.TranslationX);
	}
}
