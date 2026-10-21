using System;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace PJAnimation;

/// <summary>
/// Frame source for the <see cref="AnimationManager"/>. Port of Comet's <c>Ticker</c>: a <see cref="Timer"/> elapsing
/// <see cref="MaxFps"/> times per second.
/// </summary>
/// <remarks>
/// Unlike Comet, <see cref="Fire"/> is not invoked on the timer thread: it is dispatched to the UI thread (the
/// <see cref="IDispatcher"/> found by <see cref="Start"/>) because animated values are applied with <c>SetValue</c>.
/// Frames are coalesced: while one frame is waiting for the UI thread further elapses are dropped, so a busy UI thread
/// never builds a backlog of stale frames.
/// </remarks>
public class Ticker
{
	readonly Lock gate = new();
	readonly Action fireOnUIThread;
	Timer? timer;
	IDispatcher? dispatcher;
	int framePending;

	/// <summary>Creates a ticker.</summary>
	public Ticker() => fireOnUIThread = FireOnUIThread;

	void Timer_Elapsed(object? sender, ElapsedEventArgs e)
	{
		IDispatcher? d;
		lock (gate)
		{
			if (!ReferenceEquals(sender, timer))
				return;
			d = dispatcher;
		}

		if (d is null || Interlocked.Exchange(ref framePending, 1) == 1)
			return;
		if (!d.Dispatch(fireOnUIThread))
			Volatile.Write(ref framePending, 0);
	}

	void FireOnUIThread()
	{
		Volatile.Write(ref framePending, 0);
		if (timer is not null)
			Fire?.Invoke();
	}

	/// <summary>Target frames per second.</summary>
	public virtual int MaxFps { get; set; } = 60;

	/// <summary>Invoked on the UI thread on every frame (set by the <see cref="AnimationManager"/>).</summary>
	public Action? Fire { get; set; }

	/// <summary>Whether the ticker is running.</summary>
	public virtual bool IsRunning => timer?.Enabled ?? false;

	/// <summary>Whether the system allows animations. When <see langword="false"/>, animations jump to their end state.</summary>
	public virtual bool SystemEnabled => true;

	/// <summary>Starts the ticker. Call it on the UI thread (the <see cref="AnimationManager"/> does).</summary>
	/// <exception cref="InvalidOperationException">No UI dispatcher is available.</exception>
	public virtual void Start()
	{
		lock (gate)
		{
			if (timer is not null)
				return;

			dispatcher = DispatcherHelper.FindDispatcher(null)
				?? throw new InvalidOperationException("No UI dispatcher is available to drive animations. Call AnimationManager.SetTicker with a custom Ticker.");
			timer = new Timer
			{
				AutoReset = true,
				Interval = 1000.0 / Math.Max(1, MaxFps),
			};
			timer.Elapsed += Timer_Elapsed;
			timer.Start();
		}
	}

	/// <summary>Stops the ticker. A frame already posted to the UI thread is discarded.</summary>
	public virtual void Stop()
	{
		lock (gate)
		{
			if (timer is null)
				return;
			timer.AutoReset = false;
			timer.Stop();
			timer.Elapsed -= Timer_Elapsed;
			timer.Dispose();
			timer = null;
			dispatcher = null;
		}
	}
}

/// <summary>
/// A ticker driven manually by calling <see cref="Tick"/>. Useful for tests and custom hosts.
/// </summary>
public class ManualTicker : Ticker
{
	bool running;

	/// <summary>Controls <see cref="SystemEnabled"/>.</summary>
	public bool IsSystemEnabled { get; set; } = true;

	/// <summary>Number of times <see cref="Start"/> was called while stopped.</summary>
	public int StartCount { get; private set; }

	/// <inheritdoc/>
	public override bool IsRunning => running;

	/// <inheritdoc/>
	public override bool SystemEnabled => IsSystemEnabled;

	/// <inheritdoc/>
	public override void Start()
	{
		if (!running)
			StartCount++;
		running = true;
	}

	/// <inheritdoc/>
	public override void Stop() => running = false;

	/// <summary>Fires one frame if the ticker is running.</summary>
	public void Tick()
	{
		if (running)
			Fire?.Invoke();
	}
}

static class DispatcherHelper
{
	/// <summary>Finds a dispatcher without throwing when none is available.</summary>
	public static IDispatcher? FindDispatcher(BindableObject? bindable)
	{
		if (bindable is not null)
		{
			try
			{
				return bindable.Dispatcher;
			}
			catch (InvalidOperationException)
			{
			}
		}

		if (Dispatcher.GetForCurrentThread() is { } current)
			return current;

		try
		{
			return Application.Current?.Dispatcher;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}
}
