using System;
using System.Collections.Generic;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace PJAnimation;

/// <summary>
/// A time-based animation. A leaf animation interpolates <see cref="StartValue"/> → <see cref="EndValue"/>
/// and, when <see cref="Target"/> and <see cref="Property"/> are set, writes the value to the target with
/// <see cref="BindableObject.SetValue(BindableProperty, object)"/>. A group animation ticks its children.
/// Times are in seconds.
/// </summary>
/// <remarks>Animations are not thread-safe and must be used on the UI thread.</remarks>
public class Animation : IDisposable
{
	List<Animation> childrenAnimations = new();
	WeakReference<BindableObject>? target;
	Lerp? lerp;
	object? trackToken;
	WeakReference<VisualElement>? owner;
	bool paused;
	bool completedRaised;
	bool disposedValue;

	/// <summary>Creates an empty animation.</summary>
	public Animation()
	{
	}

	/// <summary>Creates a group animation that ticks <paramref name="animations"/> in parallel.</summary>
	public Animation(List<Animation> animations)
	{
		ArgumentNullException.ThrowIfNull(animations);
		childrenAnimations = animations;
	}

	/// <summary>Whether the animation is paused.</summary>
	public bool IsPaused => paused;

	/// <summary>Delay, in seconds, before the animation starts.</summary>
	public double StartDelay { get; set; }

	/// <summary>Duration, in seconds.</summary>
	public double Duration { get; set; }

	/// <summary>Elapsed time, in seconds, including <see cref="StartDelay"/>.</summary>
	public double CurrentTime { get; protected set; }

	/// <summary>Easing applied to the linear progress before interpolating. Defaults to <see cref="Easing.Linear"/>.</summary>
	public Easing Easing { get; set; } = Easing.Linear;

	/// <summary>Whether the animation has finished.</summary>
	public bool HasFinished { get; protected set; }

	/// <summary>Start value.</summary>
	public object? StartValue { get; set; }

	/// <summary>End value.</summary>
	public object? EndValue { get; set; }

	/// <summary>The last computed value.</summary>
	public object? CurrentValue { get; protected set; }

	/// <summary>Whether the animation restarts when it finishes.</summary>
	public bool Repeats { get; set; }

	/// <summary>An optional identifier.</summary>
	public string? Id { get; set; }

	/// <summary>
	/// The object whose <see cref="Property"/> is animated. Held weakly: an animation never keeps its target alive.
	/// Init-only: the (target, property) track is fixed once the animation is created.
	/// </summary>
	public BindableObject? Target
	{
		get => target.GetValueOrDefault();
		init => target = value is null ? null : new WeakReference<BindableObject>(value);
	}

	/// <summary>The animated property of <see cref="Target"/>. Init-only, like <see cref="Target"/>.</summary>
	public BindableProperty? Property { get; init; }

	/// <summary>Invoked once when the animation finishes (not invoked for repeating animations or on dispose).</summary>
	public Action? Completed { get; set; }

	/// <summary>
	/// Whether the animation was cancelled, either because a newer animation took over its
	/// (<see cref="Target"/>, <see cref="Property"/>) track or because its target was collected.
	/// </summary>
	public bool IsCancelled { get; private set; }

	/// <summary>Whether the animation has been disposed.</summary>
	public bool IsDisposed => disposedValue;

	internal IReadOnlyList<Animation> Children => childrenAnimations;

	internal bool IsInManager { get; set; }

	/// <summary>Paused because its owner view was unloaded (not by the user); resumed when the owner is loaded again.</summary>
	internal bool IsLifecyclePaused { get; private set; }

	/// <summary>The view this animation was added to with <c>AddAnimation</c> (held weakly).</summary>
	internal VisualElement? Owner
	{
		get => owner.GetValueOrDefault();
		set => owner = value is null ? null : new WeakReference<VisualElement>(value);
	}

	internal object TrackToken
	{
		get => trackToken ??= new object();
		set => trackToken = value;
	}

	Lerp Lerp
	{
		get => lerp ??= ResolveLerp(StartValue, EndValue);
		set => lerp = value;
	}

	/// <summary>Advances the animation by <paramref name="secondsSinceLastUpdate"/> seconds.</summary>
	public void Tick(double secondsSinceLastUpdate)
	{
		if (IsPaused || IsDisposed)
			return;

		OnTick(secondsSinceLastUpdate);

		if (HasFinished)
			RaiseCompleted();
	}

	/// <summary>Advances the animation. Override to customize ticking (e.g. sequences).</summary>
	protected virtual void OnTick(double secondsSinceLastUpdate)
	{
		if (HasFinished)
			return;

		CurrentTime += secondsSinceLastUpdate;
		if (childrenAnimations.Count > 0)
		{
			var hasFinished = true;
			var allCancelled = true;
			foreach (var animation in childrenAnimations)
			{
				animation.OnTick(secondsSinceLastUpdate);
				if (!animation.HasFinished)
					hasFinished = false;
				if (!animation.IsCancelled)
					allCancelled = false;
			}
			HasFinished = hasFinished;
			if (hasFinished && allCancelled)
				IsCancelled = true;
		}
		else if (target is not null && target.GetValueOrDefault() is null)
		{
			Cancel();
		}
		else
		{
			if (CurrentTime < StartDelay)
				return;
			var percent = Duration <= 0 ? 1 : Math.Min((CurrentTime - StartDelay) / Duration, 1);
			Update(percent);
		}

		if (HasFinished)
		{
			if (Repeats && !IsCancelled)
				Reset();
			else
				ReleaseTrack();
		}
	}

	/// <summary>
	/// Sets the animation to <paramref name="percent"/> (0..1) of its duration:
	/// <c>CurrentValue = Lerp(StartValue, EndValue, Easing.Ease(percent))</c>, then applies it to the target.
	/// </summary>
	public virtual void Update(double percent)
	{
		object? value;
		var finished = percent >= 1;
		if (finished)
		{
			value = EndValue;
		}
		else
		{
			try
			{
				var progress = (Easing ?? Easing.Linear).Ease(percent);
				value = Lerp.Calculate(StartValue, EndValue, progress);
			}
			catch (Exception ex) when (ex is InvalidCastException or NullReferenceException or ArithmeticException)
			{
				value = EndValue;
				finished = true;
			}
		}

		var unchanged = ReferenceEquals(value, CurrentValue) && value is not null;
		CurrentValue = value;
		if (!ApplyValue(value, unchanged))
			return;
		HasFinished = finished;
	}

	/// <summary>
	/// Wraps this animation in a parent that plays it forward and then backwards (Comet semantics).
	/// The parent inherits <see cref="Repeats"/>; this animation stops repeating.
	/// </summary>
	public Animation CreateAutoReversing()
	{
		var total = GetTotalTime();
		var reversed = CreateReverse(total);
		var parentAnimation = new Animation
		{
			Duration = reversed.GetTotalTime(),
			Repeats = Repeats,
		};
		parentAnimation.childrenAnimations.Add(this);
		parentAnimation.childrenAnimations.Add(reversed);
		Repeats = false;
		return parentAnimation;
	}

	/// <summary>Resets the animation (and its children) to the beginning.</summary>
	public virtual void Reset()
	{
		CurrentTime = 0;
		HasFinished = false;
		completedRaised = false;
		foreach (var x in childrenAnimations)
		{
			if (!x.IsCancelled)
				x.Reset();
		}
	}

	/// <summary>
	/// Pauses the animation and removes it from the <see cref="AnimationManager"/>. An explicitly paused animation
	/// stays paused when its view is loaded again (it is only resumed by <see cref="Resume"/>).
	/// </summary>
	public void Pause()
	{
		paused = true;
		IsLifecyclePaused = false;
		AnimationManager.Remove(this);
	}

	/// <summary>Resumes the animation and adds it to the <see cref="AnimationManager"/> (no time jump).</summary>
	public void Resume()
	{
		paused = false;
		IsLifecyclePaused = false;
		if (!HasFinished && !IsDisposed)
			AnimationManager.Add(this);
	}

	/// <summary>Pauses because the owner view was unloaded. No-op when already paused (keeps an explicit pause explicit).</summary>
	internal void LifecyclePause()
	{
		if (paused || HasFinished || IsDisposed)
			return;
		paused = true;
		IsLifecyclePaused = true;
		AnimationManager.Remove(this);
	}

	/// <summary>Resumes only if the animation was paused by <see cref="LifecyclePause"/>.</summary>
	internal void LifecycleResume()
	{
		if (!IsLifecyclePaused)
			return;
		Resume();
	}

	/// <summary>Disposes the animation and its children and removes it from the <see cref="AnimationManager"/>.</summary>
	protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }
        
        if (disposing)
        {
            foreach (var child in childrenAnimations)
                child.Dispose();
            childrenAnimations.Clear();
        }
        ReleaseTrack();
        target = null;
        disposedValue = true;
        AnimationManager.Remove(this);
        DetachFromOwner();
        RaiseEnded();
    }

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>Jumps to the final state (used when system animations are disabled).</summary>
	internal virtual void FinishImmediately()
	{
		if (HasFinished || IsDisposed)
			return;
		if (childrenAnimations.Count > 0)
		{
			foreach (var child in childrenAnimations)
				child.FinishImmediately();
		}
		else
		{
			Update(1);
		}
		HasFinished = true;
		ReleaseTrack();
	}

	internal void RaiseCompleted()
	{
		if (completedRaised)
			return;
		completedRaised = true;
		DetachFromOwner();
		try
		{
			Completed?.Invoke();
		}
		finally
		{
			RaiseEnded();
		}
	}

	/// <summary>
	/// One-shot hook raised when the animation completes (finished or cancelled) or is disposed, whichever comes first.
	/// Used by <c>AnimateAsync</c> so an awaiting caller is never left hanging on dispose.
	/// </summary>
	internal Action<Animation>? Ended { get; set; }

	void RaiseEnded()
	{
		var ended = Ended;
		Ended = null;
		ended?.Invoke(this);
	}

	void DetachFromOwner()
	{
		var o = Owner;
		owner = null;
		if (o is not null)
			AnimationExtensions.OnAnimationEnded(o, this);
	}

	internal BindableObject? FindTarget()
	{
		if (Target is { } t)
			return t;
		foreach (var child in childrenAnimations)
		{
			if (child.FindTarget() is { } ct)
				return ct;
		}
		return GetTargetHint();
	}

	/// <summary>A target used to locate the hosting MAUI context when this animation has no leaf target.</summary>
	private protected virtual BindableObject? GetTargetHint() => null;

	double GetTotalTime()
	{
		if (childrenAnimations.Count == 0)
			return StartDelay + Duration;
		var total = 0.0;
		foreach (var child in childrenAnimations)
			total = Math.Max(total, child.GetTotalTime());
		return total;
	}

	Animation CreateReverse(double mirror)
	{
		if (childrenAnimations.Count > 0)
		{
			var reversedChildren = new List<Animation>(childrenAnimations.Count);
			for (var i = childrenAnimations.Count - 1; i >= 0; i--)
				reversedChildren.Add(childrenAnimations[i].CreateReverse(mirror));
			return new Animation(reversedChildren)
			{
				Easing = Easing,
				Duration = Duration,
			};
		}

		return new Animation
		{
			Easing = Easing,
			Duration = Duration,
			StartDelay = 2 * mirror - (StartDelay + Duration),
			StartValue = EndValue,
			EndValue = StartValue,
			target = target,
			Property = Property,
			Lerp = Lerp,
			TrackToken = TrackToken,
		};
	}

	bool ApplyValue(object? value, bool unchanged)
	{
		if (Property is null || target is null)
			return true;

		if (target.GetValueOrDefault() is not { } t)
		{
			Cancel();
			return false;
		}

		if (!AnimationTracks.TryClaim(t, Property, TrackToken))
		{
			Cancel();
			return false;
		}

		if (!unchanged)
			t.SetValue(Property, value);
		return true;
	}

	void Cancel()
	{
		IsCancelled = true;
		HasFinished = true;
	}

	void ReleaseTrack()
	{
		if (Property is not null && Target is { } t && trackToken is not null)
			AnimationTracks.Release(t, Property, trackToken);
	}

	static Lerp ResolveLerp(object? start, object? end)
	{
		var startType = start?.GetType();
		var endType = end?.GetType();
		var type = startType is null ? endType
			: endType is null ? startType
			: CommonBaseType(startType, endType);
		return type is null ? Lerp.Discrete : Lerp.GetLerp(type) ?? Lerp.Discrete;
	}

	static Type? CommonBaseType(Type a, Type b)
	{
		for (Type? t = a; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
		{
			if (t.IsAssignableFrom(b))
				return t;
		}
		return null;
	}
}
