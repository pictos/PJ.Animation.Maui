using System.Threading;
using System.Runtime.CompilerServices;

namespace PJAnimation.Tests;

// On net10.0 (no platform), MAUI raises VisualElement.Loaded/Unloaded when the element's Window is set/cleared.
public class LifecycleTests : TestBase
{
	static (Window Window, ContentPage Page, Label Label) CreateLoadedPage()
	{
		var label = new Label();
		var page = new ContentPage { Content = new VerticalStackLayout { label } };
		var window = new Window(page);
		return (window, page, label);
	}

	[Fact]
	public void UnloadedPausesAndLoadedResumes()
	{
		var (window, page, label) = CreateLoadedPage();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Assert.True(label.IsLoaded);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);

		window.Page = new ContentPage();
		Assert.False(label.IsLoaded);
		var animation = Assert.Single(label.Animations);
		Assert.True(animation.IsPaused);
		Assert.Equal(0, AnimationManager.Count);
		Run(5);
		Assert.Equal(50, label.TranslationX, 6);

		window.Page = page;
		Assert.False(animation.IsPaused);
		Run(.25);
		Assert.Equal(75, label.TranslationX, 6);
		Run(1);
		Assert.Equal(100, label.TranslationX, 6);
		Assert.Empty(label.Animations);
	}

	[Fact]
	public void ExplicitPauseIsNotResumedByLoaded()
	{
		var (window, page, label) = CreateLoadedPage();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.5);
		page.PauseAnimations();

		window.Page = new ContentPage();
		window.Page = page;
		var animation = Assert.Single(label.Animations);
		Assert.True(animation.IsPaused);
		Run(1);
		Assert.Equal(50, label.TranslationX, 6);

		page.ResumeAnimations();
		Run(1);
		Assert.Equal(100, label.TranslationX, 6);
	}

	[Fact]
	public void PauseWhileUnloadedStaysPausedAfterLoad()
	{
		var (window, page, label) = CreateLoadedPage();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		window.Page = new ContentPage();
		label.PauseAnimations();
		window.Page = page;
		Assert.True(Assert.Single(label.Animations).IsPaused);
	}

	[Fact]
	public void ExplicitResumeWhileUnloadedWins()
	{
		var (window, _, label) = CreateLoadedPage();
		label.Animate(l => l.TranslationX = 100, duration: 1);
		window.Page = new ContentPage();
		label.ResumeAnimations();
		Run(1.1);
		Assert.Equal(100, label.TranslationX, 6);
	}

	[Fact]
	public void AnimationOnViewThatWasNeverLoadedStartsImmediately()
	{
		var label = new Label();
		Assert.False(label.IsLoaded);
		label.Animate(l => l.TranslationX = 100, duration: 1);
		Run(.5);
		Assert.Equal(50, label.TranslationX, 6);
	}

	[Fact]
	public void LifecycleSubscriptionIsRemovedWhenAnimationsEnd()
	{
		var (_, _, label) = CreateLoadedPage();
		label.Animate(l => l.TranslationX = 100, duration: .2);
		Assert.True(AnimationExtensions.IsLifecycleAttached(label));
		Run(.5);
		Assert.False(AnimationExtensions.IsLifecycleAttached(label));
	}

	[Fact]
	public void LifecycleSubscriptionIsRemovedOnRemoveAndDispose()
	{
		var label = new Label();
		var a = new Animation { Duration = 1 };
		var b = new Animation { Duration = 1 };
		label.AddAnimation(a);
		label.AddAnimation(b);
		Assert.True(AnimationExtensions.IsLifecycleAttached(label));
		label.RemoveAnimation(a);
		Assert.True(AnimationExtensions.IsLifecycleAttached(label));
		b.Dispose();
		Assert.False(AnimationExtensions.IsLifecycleAttached(label));
		Assert.Empty(label.Animations);
	}

	[Fact]
	public void RepeatingSequenceKeepsSubscriptionAndSurvivesUnloadReload()
	{
		var (window, page, label) = CreateLoadedPage();
		label.BeginAnimationSequence(repeats: true)
			.Animate(l => l.Opacity = 0, duration: 1)
			.Animate(l => l.Opacity = 1, duration: 1)
			.EndAnimationSequence();
		Run(.5);
		Assert.Equal(.5, label.Opacity, 6);
		window.Page = new ContentPage();
		Run(3);
		Assert.Equal(.5, label.Opacity, 6);
		window.Page = page;
		Run(.5);
		Assert.Equal(0, label.Opacity, 6);
		Assert.True(AnimationExtensions.IsLifecycleAttached(label));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	static (WeakReference Page, Window Window) StartSequenceCapturingPageInClosure()
	{
		var button = new Button();
		var label = new Label();
		var page = new ContentPage { Content = new VerticalStackLayout { label } };
		var window = new Window(page);
		// The closure captures `button` and `page` strongly (the documented leak risk).
		label.BeginAnimationSequence(repeats: true, alsoCapture: [button])
			.Animate(l => { l.Opacity = 0; button.Opacity = 0; page.Title = "a"; }, duration: 1)
			.Animate(l => { l.Opacity = 1; button.Opacity = 1; page.Title = "b"; }, duration: 1)
			.EndAnimationSequence();
		return (new WeakReference(page), window);
	}

	[Fact]
	public void UnloadReleasesRepeatingSequenceSoClosuresDoNotLeakThePage()
	{
		var (weakPage, window) = StartSequenceCapturingPageInClosure();
		Run(.5);
		Assert.Equal(1, AnimationManager.Count);

		window.Page = new ContentPage();
		Assert.Equal(0, AnimationManager.Count);

		for (var i = 0; i < 5 && weakPage.IsAlive; i++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
		Assert.False(weakPage.IsAlive);
		GC.KeepAlive(window);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	static (WeakReference Extra, AnimationSequence<Label> Sequence) CreateSequenceWithAlsoCapture(Label label)
	{
		var button = new Button();
		var sequence = label.BeginAnimationSequence(alsoCapture: [button]);
		return (new WeakReference(button), sequence);
	}

	[Fact]
	public void AlsoCaptureIsHeldWeakly()
	{
		var label = new Label();
		var (weak, sequence) = CreateSequenceWithAlsoCapture(label);
		for (var i = 0; i < 5 && weak.IsAlive; i++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
		Assert.False(weak.IsAlive);
		Assert.Empty(sequence.AlsoCapture);
	}

	[Fact]
	public void DisposingASequenceReleasesItsSteps()
	{
		var label = new Label();
		var sequence = label.BeginAnimationSequence(repeats: true)
			.Animate(l => l.Opacity = 0, duration: 1);
		sequence.EndAnimationSequence();
		Assert.Single(sequence.Steps);
		sequence.Dispose();
		Assert.Empty(sequence.Steps);
		Assert.Empty(label.Animations);
	}
}

public class ThreadingTests : TestBase
{
	static void OnBackgroundThread(Action action)
	{
		Exception? error = null;
		var thread = new Thread(() =>
		{
			try { action(); }
			catch (Exception ex) { error = ex; }
		});
		thread.Start();
		thread.Join();
		if (error is not null)
			throw error;
	}

	[Fact]
	public void AnimateFromBackgroundThreadIsDispatched()
	{
		var label = new Label();
		TestDispatcher.Instance.RequireDispatch = true;
		Label? result = null;
		OnBackgroundThread(() => result = label.Animate(l => l.TranslationX = 100, duration: 1));
		Assert.Same(label, result);
		Assert.Equal(0, label.TranslationX);
		Assert.Empty(label.Animations);
		Assert.False(TestDispatcher.Instance.Queue.IsEmpty);

		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.Pump();
		Assert.Single(label.Animations);
		Run(1.1);
		Assert.Equal(100, label.TranslationX, 6);
	}

	[Fact]
	public void SequenceAndControlCallsFromBackgroundThreadAreDispatched()
	{
		var label = new Label();
		TestDispatcher.Instance.RequireDispatch = true;
		OnBackgroundThread(() => label.BeginAnimationSequence()
			.Animate(l => l.TranslationX = 100, duration: 1)
			.EndAnimationSequence());
		Assert.Empty(label.Animations);
		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.Pump();
		Assert.Single(label.Animations);

		TestDispatcher.Instance.RequireDispatch = true;
		OnBackgroundThread(label.PauseAnimations);
		Assert.False(label.Animations[0].IsPaused);
		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.Pump();
		Assert.True(label.Animations[0].IsPaused);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void ManagerAddFromBackgroundThreadIsDispatched()
	{
		var label = new Label();
		var animation = new Animation { Target = label, Property = VisualElement.OpacityProperty, StartValue = 1.0, EndValue = 0.0, Duration = 1 };
		TestDispatcher.Instance.RequireDispatch = true;
		OnBackgroundThread(() => AnimationManager.Add(animation));
		Assert.Equal(0, AnimationManager.Count);
		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.Pump();
		Assert.Equal(1, AnimationManager.Count);
	}
}
