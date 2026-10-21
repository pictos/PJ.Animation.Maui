using System;
using System.Collections.Generic;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace PJAnimation;

/// <summary>
/// Plays animation steps one after another. Each step's action is captured lazily when the step starts,
/// so it animates from the values left by the previous step (Comet semantics).
/// </summary>
/// <remarks>
/// The step actions (closures) are kept for the lifetime of the sequence. A running repeating sequence is referenced by
/// the <see cref="AnimationManager"/>, so anything its closures capture stays alive while it runs: use the action's
/// parameter instead of capturing the view, pass other animated objects through <c>alsoCapture</c> (held weakly), and
/// rely on the view's <c>Unloaded</c> event (which pauses and releases the sequence from the manager) or call
/// <see cref="Animation.Dispose()"/> for views that are never shown.
/// </remarks>
public class AnimationSequence<T> : Animation where T : VisualElement
{
	readonly List<AnimationStep<T>> steps = new();
	readonly List<WeakReference<BindableObject>> alsoCapture = new();
	WeakReference<T>? view;
	Animation? currentAnimation;
	int currentIndex;

	/// <summary>Creates an empty sequence.</summary>
	public AnimationSequence()
	{
	}

	/// <summary>Creates a sequence for <paramref name="view"/>.</summary>
	public AnimationSequence(T view)
	{
		View = view;
	}

	/// <summary>The view the sequence belongs to. Held weakly.</summary>
	public T? View
	{
		get => view.GetValueOrDefault();
		set => view = value is null ? null : new WeakReference<T>(value);
	}

	/// <summary>Additional objects (outside the view's visual tree) captured by every step. Held weakly.</summary>
	public IReadOnlyList<BindableObject> AlsoCapture
	{
		get
		{
			var result = new List<BindableObject>(alsoCapture.Count);
			foreach (var weak in alsoCapture)
			{
				if (weak.GetValueOrDefault() is { } obj)
					result.Add(obj);
			}
			return result;
		}
	}

	/// <summary>Adds objects (outside the view's visual tree) whose changes every step animates. They are held weakly.</summary>
	public AnimationSequence<T> AddAlsoCapture(IEnumerable<BindableObject> objects)
	{
		ArgumentNullException.ThrowIfNull(objects);
		foreach (var obj in objects)
		{
			if (obj is not null)
				alsoCapture.Add(new WeakReference<BindableObject>(obj));
		}
		return this;
	}

	/// <summary>The steps of the sequence.</summary>
	public IReadOnlyList<AnimationStep<T>> Steps => steps;

	/// <summary>Appends a step.</summary>
	public AnimationSequence<T> Animate(Easing easing, Action<T> action, Action? completed = null, double duration = .2, double delay = 0)
	{
		ArgumentNullException.ThrowIfNull(action);
		steps.Add(new AnimationStep<T>
		{
			Easing = easing ?? Easing.Linear,
			Action = action,
			Completed = completed,
			Duration = duration,
			Delay = delay,
		});
		return this;
	}

	/// <summary>Appends a step using <see cref="Easing.Linear"/>.</summary>
	public AnimationSequence<T> Animate(Action<T> action, Action? completed = null, double duration = .2, double delay = 0)
		=> Animate(Easing.Linear, action, completed, duration, delay);

	/// <summary>Finishes building the sequence, starts it and returns the view.</summary>
	/// <exception cref="InvalidOperationException">The sequence has no view.</exception>
	public T EndAnimationSequence()
	{
		var v = View ?? throw new InvalidOperationException("The animation sequence has no View.");
		var duration = 0.0;
		foreach (var step in steps)
			duration += step.Delay + step.Duration;
		Duration = duration;
		v.AddAnimation(this);
		return v;
	}

	/// <inheritdoc/>
	protected override void OnTick(double secondsSinceLastUpdate)
	{
		if (HasFinished)
			return;

		CurrentTime += secondsSinceLastUpdate;
		if (CurrentTime < StartDelay)
			return;

		currentAnimation ??= GetNextAnimation();
		if (currentAnimation is null)
		{
			HasFinished = true;
			return;
		}

		currentAnimation.Tick(secondsSinceLastUpdate);
		if (currentAnimation.HasFinished)
			currentAnimation = null;
	}

	/// <inheritdoc/>
	public override void Reset()
	{
		base.Reset();
		currentAnimation?.Dispose();
		currentAnimation = null;
		currentIndex = 0;
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			currentAnimation?.Dispose();
			currentAnimation = null;
			steps.Clear();
			alsoCapture.Clear();
		}
		base.Dispose(disposing);
	}

	internal override void FinishImmediately()
	{
		if (HasFinished || IsDisposed)
			return;

		currentAnimation?.FinishImmediately();
		currentAnimation?.RaiseCompleted();
		currentAnimation = null;

		while (currentIndex < steps.Count && View is not null)
		{
			var animation = CreateStepAnimation(steps[currentIndex++]);
			if (animation is null)
				continue;
			animation.FinishImmediately();
			animation.RaiseCompleted();
		}
		HasFinished = true;
	}

	private protected override BindableObject? GetTargetHint() => View;

	Animation? GetNextAnimation()
	{
		for (var attempts = 0; attempts < steps.Count; attempts++)
		{
			if (View is null)
				return null;
			if (currentIndex >= steps.Count)
			{
				if (!Repeats)
					return null;
				currentIndex = 0;
			}

			var animation = CreateStepAnimation(steps[currentIndex++]);
			if (animation is not null)
				return animation;
		}
		return null;
	}

	Animation? CreateStepAnimation(AnimationStep<T> step)
	{
		var v = View;
		if (v is null || step.Action is null)
			return null;
		var animation = AnimationExtensions.CreateAnimation(v, step.Easing, step.Action, step.Completed, step.Duration, step.Delay, alsoCapture: AlsoCapture);
		if (animation is null)
			step.Completed?.Invoke();
		return animation;
	}
}

/// <summary>A step of an <see cref="AnimationSequence{T}"/>.</summary>
public class AnimationStep<T>
{
	/// <summary>Easing of the step. Defaults to <see cref="Easing.Linear"/>.</summary>
	public Easing Easing { get; set; } = Easing.Linear;

	/// <summary>Assigns the step's target values.</summary>
	public Action<T>? Action { get; set; }

	/// <summary>Invoked when the step finishes.</summary>
	public Action? Completed { get; set; }

	/// <summary>Duration in seconds.</summary>
	public double Duration { get; set; }

	/// <summary>Delay in seconds before the step starts.</summary>
	public double Delay { get; set; }
}
