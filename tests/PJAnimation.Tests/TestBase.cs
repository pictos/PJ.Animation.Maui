using System.Collections.Concurrent;
using Microsoft.Maui.Dispatching;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PJAnimation.Tests;

/// <summary>Deterministic clock: time only moves when <see cref="Advance"/> is called.</summary>
sealed class FakeClock : TimeProvider
{
	long ticks = TimeSpan.TicksPerHour;

	public override long TimestampFrequency => TimeSpan.TicksPerSecond;

	public override long GetTimestamp() => ticks;

	public void Advance(double seconds) => ticks += (long)Math.Round(seconds * TimeSpan.TicksPerSecond);
}

sealed class TestDispatcher : IDispatcher
{
	public static readonly TestDispatcher Instance = new();
	public readonly ConcurrentQueue<Action> Queue = new();

	public bool RequireDispatch { get; set; }
	public bool RejectDispatch { get; set; }

	public bool IsDispatchRequired => RequireDispatch;

	public bool Dispatch(Action action)
	{
		if (RejectDispatch)
			return false;
		Queue.Enqueue(action);
		return true;
	}

	public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

	public IDispatcherTimer CreateTimer() => new TestTimer();

	public void Pump()
	{
		while (Queue.TryDequeue(out var action))
			action();
	}
}

sealed class TestTimer : IDispatcherTimer
{
	public static int Created;

	public TestTimer() => Created++;

	public TimeSpan Interval { get; set; }
	public bool IsRepeating { get; set; }
	public bool IsRunning { get; private set; }
	public event EventHandler? Tick;
	public void Start() => IsRunning = true;
	public void Stop() => IsRunning = false;
	public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
}

sealed class TestDispatcherProvider : IDispatcherProvider
{
	public IDispatcher? GetForCurrentThread() => TestDispatcher.Instance;
}

public abstract class TestBase : IDisposable
{
	static TestBase()
	{
		DispatcherProvider.SetCurrent(new TestDispatcherProvider());
	}

	internal readonly FakeClock Clock = new();
	internal readonly ManualTicker Ticker = new();

	protected TestBase()
	{
		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.RejectDispatch = false;
		TestDispatcher.Instance.Queue.Clear();
		AnimationManager.Clear();
		AnimationManager.TimeProvider = Clock;
		AnimationManager.SpeedModifier = 1;
		AnimationManager.MaxFrameDelta = TimeSpan.FromMilliseconds(100);
		AnimationManager.SetTicker(Ticker);
		AnimationManager.RespectReducedMotion = true;
	}

	/// <summary>Advances the clock by <paramref name="seconds"/> in frames of at most <paramref name="frame"/> seconds.</summary>
	protected void Run(double seconds, double frame = 0.05)
	{
		var remaining = seconds;
		while (remaining > 1e-9)
		{
			var step = Math.Min(frame, remaining);
			Clock.Advance(step);
			Ticker.Tick();
			remaining -= step;
		}
	}

	/// <summary>Fires a single frame after advancing the clock.</summary>
	protected void Frame(double seconds)
	{
		Clock.Advance(seconds);
		Ticker.Tick();
	}

	public virtual void Dispose()
	{
		AnimationManager.Clear();
		AnimationManager.RespectReducedMotion = true;
		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.RejectDispatch = false;
		TestDispatcher.Instance.Queue.Clear();
		GC.SuppressFinalize(this);
	}
}
