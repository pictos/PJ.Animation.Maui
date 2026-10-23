using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace PJAnimation;

/// <summary>Comet-style animation extensions for .NET MAUI <see cref="VisualElement"/>s.</summary>
public static class AnimationExtensions
{
	static readonly ConditionalWeakTable<VisualElement, ViewAnimations> viewAnimations = new();
	static readonly EventHandler onLoaded = OnLoaded;
	static readonly EventHandler onUnloaded = OnUnloaded;

	sealed class ViewAnimations
	{
		public readonly List<Animation> Animations = new();
		public bool LifecycleAttached;
	}

	/// <summary>
	/// Animates every bindable property changed by <paramref name="action"/> on <paramref name="view"/>, its visual
	/// descendants and <paramref name="alsoCapture"/>, from its current value to the assigned value.
	/// Uses <see cref="Easing.Linear"/>. Durations and delays are in seconds.
	/// </summary>
	/// <param name="view">The view the animation belongs to (and the root of the capture scope).</param>
	/// <param name="action">Assigns the target values. Runs once, synchronously, on the UI thread.</param>
	/// <param name="completed">Invoked when the animation finishes (never for repeating animations).</param>
	/// <param name="duration">Duration in seconds.</param>
	/// <param name="delay">Delay in seconds.</param>
	/// <param name="repeats">Repeat forever.</param>
	/// <param name="autoReverses">Play forward then backwards.</param>
	/// <param name="alsoCapture">Additional objects outside the visual tree of <paramref name="view"/> whose changes should be animated.</param>
	public static T Animate<T>(this T view, Action<T> action, Action? completed = null, double duration = .2, double delay = 0, bool repeats = false, bool autoReverses = false, IEnumerable<BindableObject>? alsoCapture = null)
		where T : VisualElement
		=> view.Animate(Easing.Linear, action, completed, duration, delay, repeats, autoReverses, alsoCapture);

	/// <summary>
	/// Animates every bindable property changed by <paramref name="action"/> using <paramref name="easing"/>.
	/// See <see cref="Animate{T}(T, Action{T}, Action?, double, double, bool, bool, IEnumerable{BindableObject}?)"/>.
	/// </summary>
	public static T Animate<T>(this T view, Easing easing, Action<T> action, Action? completed = null, double duration = .2, double delay = 0, bool repeats = false, bool autoReverses = false, IEnumerable<BindableObject>? alsoCapture = null)
		where T : VisualElement
	{
		ArgumentNullException.ThrowIfNull(view);
		ArgumentNullException.ThrowIfNull(action);
		RunOnUIThread(view, (view, easing, action, completed, duration, delay, repeats, autoReverses, alsoCapture), static work =>
		{
			var (view, easing, action, completed, duration, delay, repeats, autoReverses, alsoCapture) = work;
			var animation = CreateAnimation(view, easing, action, completed, duration, delay, repeats, autoReverses, alsoCapture);
			if (animation is null)
			{
				completed?.Invoke();
				return;
			}
			view.AddAnimation(animation);
		});
		return view;
	}

	/// <summary>
	/// Awaitable version of <see cref="Animate{T}(T, Action{T}, Action?, double, double, bool, bool, IEnumerable{BindableObject}?)"/>:
	/// instead of a <c>completed</c> callback, returns a task that completes when the animation ends.
	/// Uses <see cref="Easing.Linear"/>. Durations and delays are in seconds.
	/// </summary>
	/// <returns>
	/// Like MAUI's <c>FadeToAsync</c>: <see langword="true"/> if the animation was cancelled (by
	/// <paramref name="cancellationToken"/>, a newer animation of the same property, disposal or target collection),
	/// <see langword="false"/> if it ran to completion (or nothing changed).
	/// </returns>
	/// <remarks>
	/// There is no <c>repeats</c> parameter: a repeating animation never completes. Cancelling via
	/// <paramref name="cancellationToken"/> stops the animation where it is (it does not jump to the end values).
	/// While the view is unloaded the animation is paused, so the task stays pending until it is loaded again.
	/// </remarks>
	public static Task<bool> AnimateAsync<T>(this T view, Action<T> action, double duration = .2, double delay = 0, bool autoReverses = false, IEnumerable<BindableObject>? alsoCapture = null, CancellationToken cancellationToken = default)
		where T : VisualElement
		=> view.AnimateAsync(Easing.Linear, action, duration, delay, autoReverses, alsoCapture, cancellationToken);

	/// <summary>
	/// Awaitable version of <see cref="Animate{T}(T, Easing, Action{T}, Action?, double, double, bool, bool, IEnumerable{BindableObject}?)"/>.
	/// See <see cref="AnimateAsync{T}(T, Action{T}, double, double, bool, IEnumerable{BindableObject}?, CancellationToken)"/>.
	/// </summary>
	public static Task<bool> AnimateAsync<T>(this T view, Easing easing, Action<T> action, double duration = .2, double delay = 0, bool autoReverses = false, IEnumerable<BindableObject>? alsoCapture = null, CancellationToken cancellationToken = default)
		where T : VisualElement
	{
		ArgumentNullException.ThrowIfNull(view);
		ArgumentNullException.ThrowIfNull(action);
		if (cancellationToken.IsCancellationRequested)
			return Task.FromResult(true);

		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		RunOnUIThread(view, (view, easing, action, duration, delay, autoReverses, alsoCapture, cancellationToken, tcs), static work =>
		{
			var (view, easing, action, duration, delay, autoReverses, alsoCapture, cancellationToken, tcs) = work;
			if (cancellationToken.IsCancellationRequested)
			{
				tcs.TrySetResult(true);
				return;
			}

			Animation? animation;
			try
			{
				animation = CreateAnimation(view, easing, action, completed: null, duration, delay, repeats: false, autoReverses, alsoCapture);
			}
			catch (Exception ex)
			{
				tcs.TrySetException(ex);
				return;
			}

			if (animation is null)
			{
				tcs.TrySetResult(false);
				return;
			}

			var registration = default(CancellationTokenRegistration);
			animation.Ended = a =>
			{
				registration.Dispose();
				tcs.TrySetResult(a.IsCancelled || !a.HasFinished);
			};
			if (cancellationToken.CanBeCanceled)
				registration = cancellationToken.Register(() => RunOnUIThread(view, animation, static a => a.Dispose()));
			view.AddAnimation(animation);
		});
		return tcs.Task;
	}

	/// <summary>Starts building an <see cref="AnimationSequence{T}"/> (finish with <see cref="AnimationSequence{T}.EndAnimationSequence"/>).</summary>
	public static AnimationSequence<T> BeginAnimationSequence<T>(this T view, Action? completed = null, double delay = 0, bool repeats = false, IEnumerable<BindableObject>? alsoCapture = null)
		where T : VisualElement
	{
		ArgumentNullException.ThrowIfNull(view);
		var sequence = new AnimationSequence<T>(view)
		{
			StartDelay = delay,
			Repeats = repeats,
			Completed = completed,
		};
		if (alsoCapture is not null)
			sequence.AddAlsoCapture(alsoCapture);
		return sequence;
	}

	/// <summary>
	/// Runs <paramref name="action"/>, captures the bindable property changes, restores the old values and returns an
	/// animation (not yet started) from the old to the new values, or <see langword="null"/> when nothing changed.
	/// Must be called on the UI thread.
	/// </summary>
	public static Animation? CreateAnimation<T>(T view, Easing easing, Action<T> action, Action? completed = null, double duration = .2, double delay = 0, bool repeats = false, bool autoReverses = false, IEnumerable<BindableObject>? alsoCapture = null)
		where T : VisualElement
	{
		ArgumentNullException.ThrowIfNull(view);
		ArgumentNullException.ThrowIfNull(action);
		easing ??= Easing.Linear;

		var changes = PropertyCapture.Capture(view, alsoCapture, () => action(view));
		if (changes.Length == 0)
			return null;

		var animations = new List<Animation>(changes.Length);
		foreach (var change in changes)
		{
			var animation = new Animation
			{
				Duration = duration,
				Easing = easing,
				Repeats = repeats,
				StartDelay = delay,
				StartValue = change.OldValue,
				EndValue = change.NewValue,
				Target = change.Target,
				Property = change.Property,
			};

			AnimationTracks.Claim(change.Target, change.Property, animation.TrackToken);

			if (autoReverses)
				animation = animation.CreateAutoReversing();
			animations.Add(animation);
		}

		var result = animations.Count == 1
			? animations[0]
			: new Animation(animations)
			{
				Duration = duration,
				Easing = easing,
				Repeats = repeats,
			};
		result.Completed = completed;
		return result;
	}

	/// <summary>Linearly interpolates between two colors (null is treated as black, like Comet).</summary>
	public static Color Lerp(this Color? color, double progress, Color? endColor)
	{
		color ??= Colors.Black;
		endColor ??= Colors.Black;
		static float L(float start, float end, double progress) => (float)(((end - start) * progress) + start);

		var r = L(color.Red, endColor.Red, progress);
		var g = L(color.Green, endColor.Green, progress);
		var b = L(color.Blue, endColor.Blue, progress);
		var a = L(color.Alpha, endColor.Alpha, progress);
		return new Color(r, g, b, a);
	}

	/// <summary>The animations added to <paramref name="view"/> that have not finished yet.</summary>
	public static IReadOnlyList<Animation> GetAnimations(this VisualElement view)
	{
		ArgumentNullException.ThrowIfNull(view);
		if (!viewAnimations.TryGetValue(view, out var state))
			return Array.Empty<Animation>();
		Prune(view, state);
		return state.Animations.ToArray();
	}

	extension(VisualElement view)
	{
		/// <summary>The animations added to this view that have not finished yet (Comet <c>View.Animations</c>).</summary>
		public IReadOnlyList<Animation> Animations => view.GetAnimations();
	}

	/// <summary>
	/// Associates <paramref name="animation"/> with <paramref name="view"/> and starts it. The animation is paused while
	/// the view is unloaded (<see cref="VisualElement.Unloaded"/>) and resumed when it is loaded again.
	/// Can be called from any thread (the work is dispatched to the view's <see cref="BindableObject.Dispatcher"/>).
	/// </summary>
	public static void AddAnimation(this VisualElement view, Animation animation)
	{
		ArgumentNullException.ThrowIfNull(view);
		ArgumentNullException.ThrowIfNull(animation);
		RunOnUIThread(view, (view, animation), static work =>
		{
			var (view, animation) = work;
			var state = viewAnimations.GetOrCreateValue(view);
			Prune(view, state);
			if (animation.HasFinished || animation.IsDisposed)
				return;
			if (!state.Animations.Contains(animation))
				state.Animations.Add(animation);
			animation.Owner = view;
			AttachLifecycle(view, state);
			AnimationManager.Add(animation);
		});
	}

	/// <summary>Stops <paramref name="animation"/> and dissociates it from <paramref name="view"/>. Can be called from any thread.</summary>
	public static void RemoveAnimation(this VisualElement view, Animation animation)
	{
		ArgumentNullException.ThrowIfNull(view);
		ArgumentNullException.ThrowIfNull(animation);
		RunOnUIThread(view, (view, animation), static work =>
		{
			var (view, animation) = work;
			if (viewAnimations.TryGetValue(view, out var state))
			{
				state.Animations.Remove(animation);
				Prune(view, state);
			}
			if (ReferenceEquals(animation.Owner, view))
				animation.Owner = null;
			AnimationManager.Remove(animation);
		});
	}

	/// <summary>
	/// Pauses the animations of <paramref name="view"/> and its visual descendants. Explicitly paused animations are
	/// not resumed automatically when the view is loaded again; call <see cref="ResumeAnimations"/>.
	/// Can be called from any thread.
	/// </summary>
	public static void PauseAnimations(this VisualElement view)
	{
		ArgumentNullException.ThrowIfNull(view);
		RunOnUIThread(view, view, static root => ForEachInTree(root, static v =>
		{
			if (viewAnimations.TryGetValue(v, out var state))
			{
				foreach (var animation in state.Animations.ToArray())
					animation.Pause();
			}
		}));
	}

	/// <summary>Resumes the animations of <paramref name="view"/> and its visual descendants. Can be called from any thread.</summary>
	public static void ResumeAnimations(this VisualElement view)
	{
		ArgumentNullException.ThrowIfNull(view);
		RunOnUIThread(view, view, static root => ForEachInTree(root, static v =>
		{
			if (viewAnimations.TryGetValue(v, out var state))
			{
				Prune(v, state);
				foreach (var animation in state.Animations.ToArray())
					animation.Resume();
			}
		}));
	}

	/// <summary>Called when an animation owned by <paramref name="view"/> finished, was cancelled or disposed.</summary>
	internal static void OnAnimationEnded(VisualElement view, Animation animation)
	{
		if (!viewAnimations.TryGetValue(view, out var state))
			return;
		state.Animations.Remove(animation);
		Prune(view, state);
	}

	/// <summary>Whether <paramref name="view"/> is subscribed to Loaded/Unloaded (diagnostics/tests).</summary>
	internal static bool IsLifecycleAttached(VisualElement view)
		=> viewAnimations.TryGetValue(view, out var state) && state.LifecycleAttached;

	static void AttachLifecycle(VisualElement view, ViewAnimations state)
	{
		if (state.LifecycleAttached)
			return;
		state.LifecycleAttached = true;
		view.Unloaded += onUnloaded;
		view.Loaded += onLoaded;
	}

	static void DetachLifecycle(VisualElement view, ViewAnimations state)
	{
		if (!state.LifecycleAttached)
			return;
		state.LifecycleAttached = false;
		view.Unloaded -= onUnloaded;
		view.Loaded -= onLoaded;
	}

	static void OnUnloaded(object? sender, EventArgs e)
	{
		if (sender is not VisualElement view || !viewAnimations.TryGetValue(view, out var state))
			return;
		Prune(view, state);
		foreach (var animation in state.Animations.ToArray())
			animation.LifecyclePause();
	}

	static void OnLoaded(object? sender, EventArgs e)
	{
		if (sender is not VisualElement view || !viewAnimations.TryGetValue(view, out var state))
			return;
		Prune(view, state);
		foreach (var animation in state.Animations.ToArray())
			animation.LifecycleResume();
	}

	static void ForEachInTree(VisualElement view, Action<VisualElement> action)
	{
		action(view);
		foreach (var child in ((IVisualTreeElement)view).GetVisualChildren())
		{
			if (child is VisualElement ve)
				ForEachInTree(ve, action);
		}
	}

	static void Prune(VisualElement view, ViewAnimations state)
	{
		state.Animations.RemoveAll(static a => a.HasFinished || a.IsDisposed);
		if (state.Animations.Count == 0)
			DetachLifecycle(view, state);
	}

	static void RunOnUIThread<TState>(BindableObject target, TState state, Action<TState> action)
	{
		var dispatcher = DispatcherHelper.FindDispatcher(target);
		if (dispatcher is { IsDispatchRequired: true })
			Dispatch(dispatcher, state, action);
		else
			action(state);

		static void Dispatch(IDispatcher dispatcher, TState state, Action<TState> action)
		{
			if (!dispatcher.Dispatch(() => action(state)))
				throw new InvalidOperationException("Unable to dispatch animation work to the UI thread.");
		}
	}
}
