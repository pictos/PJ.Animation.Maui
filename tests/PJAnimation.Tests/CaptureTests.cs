using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PJAnimation.Tests;

public class TestView : ContentView
{
	int plain;

	/// <summary>A CLR-only property that raises change notifications.</summary>
	public int Plain
	{
		get => plain;
		set
		{
			OnPropertyChanging();
			plain = value;
			OnPropertyChanged();
		}
	}
}

public class CaptureTests : TestBase
{
	[Fact]
	public void CapturesOldAndNewAndRestoresBeforeFirstTick()
	{
		var label = new Label { Opacity = 1, TextColor = Colors.Red };
		var animation = AnimationExtensions.CreateAnimation(label, Easing.Linear, l =>
		{
			l.Opacity = 0;
			l.TextColor = Colors.Blue;
		}, duration: 1)!;

		Assert.Equal(1, label.Opacity);
		Assert.Equal(Colors.Red, label.TextColor);

		var children = animation.Children;
		Assert.Equal(2, children.Count);
		Assert.Equal(1.0, children[0].StartValue);
		Assert.Equal(0.0, children[0].EndValue);
		Assert.Same(label, children[0].Target);
		Assert.Same(VisualElement.OpacityProperty, children[0].Property);
		Assert.Equal(Colors.Red, children[1].StartValue);
		Assert.Equal(Colors.Blue, children[1].EndValue);
		Assert.Same(Label.TextColorProperty, children[1].Property);
	}

	[Fact]
	public void AnimateMovesFromOldToNewValue()
	{
		var label = new Label { Opacity = 1 };
		label.Animate(l => l.Opacity = 0, duration: 1);
		Assert.Equal(1, label.Opacity);
		Run(.5);
		Assert.Equal(.5, label.Opacity, 6);
		Run(.5);
		Assert.Equal(0, label.Opacity);
		Assert.Equal(0, AnimationManager.Count);
		Assert.False(Ticker.IsRunning);
	}

	[Fact]
	public void UnchangedValuesAreSkipped()
	{
		var label = new Label { Opacity = .5 };
		var animation = AnimationExtensions.CreateAnimation(label, Easing.Linear, l =>
		{
			l.Opacity = .5;
			l.TranslationX = 10;
			l.TranslationX = 0;
		});
		Assert.Null(animation);

		var completed = 0;
		label.Animate(l => l.Opacity = .5, () => completed++);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void MultipleAssignmentsUseFirstOldAndLastNew()
	{
		var label = new Label { TranslationX = 0 };
		var animation = AnimationExtensions.CreateAnimation(label, Easing.Linear, l =>
		{
			l.TranslationX = 50;
			l.TranslationX = 100;
		})!;
		Assert.Equal(0.0, animation.StartValue);
		Assert.Equal(100.0, animation.EndValue);
	}

	[Fact]
	public void DescendantsAreCaptured()
	{
		var child = new Label { Opacity = 1 };
		var nested = new Label { Opacity = 1 };
		var grid = new Grid { Children = { child, new ContentView { Content = nested } } };
		grid.Animate(_ =>
		{
			child.Opacity = 0;
			nested.Opacity = 0;
		}, duration: 1);
		Assert.Equal(1, child.Opacity);
		Assert.Equal(1, nested.Opacity);
		Run(.5);
		Assert.Equal(.5, child.Opacity, 6);
		Assert.Equal(.5, nested.Opacity, 6);
	}

	[Fact]
	public void ExtraObjectsAreCaptured()
	{
		var label = new Label();
		var button = new Button { Opacity = 1 };
		label.Animate(_ => button.Opacity = 0, duration: 1, alsoCapture: [button]);
		Assert.Equal(1, button.Opacity);
		Run(.5);
		Assert.Equal(.5, button.Opacity, 6);
	}

	[Fact]
	public void OutOfScopeChangesApplyImmediately()
	{
		var label = new Label { Opacity = 1 };
		var other = new Label { Opacity = 1 };
		var completed = 0;
		label.Animate(_ => other.Opacity = 0, () => completed++, duration: 1);
		Assert.Equal(0, other.Opacity);
		Assert.Equal(1, completed);
		Assert.Equal(0, AnimationManager.Count);
	}

	[Fact]
	public void ClrOnlyPropertiesApplyImmediately()
	{
		var view = new TestView { Opacity = 1 };
		view.Animate(v =>
		{
			v.Plain = 42;
			v.Opacity = 0;
		}, duration: 1);
		Assert.Equal(42, view.Plain);
		Assert.Equal(1, view.Opacity);
	}

	[Fact]
	public void NestedCaptureIsRejected()
	{
		var label = new Label();
		var ex = Assert.Throws<InvalidOperationException>(() =>
			label.Animate(l => l.Animate(x => x.Opacity = 0)));
		Assert.Contains("inside another Animate", ex.Message);

		// Capture state is reset after the failure.
		label.Animate(l => l.Opacity = 0, duration: 1);
		Assert.Equal(1, AnimationManager.Count);
	}

	[Fact]
	public void NullBackgroundColorFadesIn()
	{
		var label = new Label();
		Assert.Null(label.BackgroundColor);
		label.Animate(l => l.BackgroundColor = Colors.Blue, duration: 1);
		Assert.Null(label.BackgroundColor);
		Run(.5);
		Assert.Equal(.5f, label.BackgroundColor!.Alpha, 4);
		Assert.Equal(1f, label.BackgroundColor.Blue, 4);
		Run(.5);
		Assert.Equal(Colors.Blue, label.BackgroundColor);
	}

	[Fact]
	public void BrushPropertyAnimates()
	{
		var box = new BoxView { Background = new SolidColorBrush(Colors.Black) };
		box.Animate(b => b.Background = Brush.White, duration: 1);
		Run(.5);
		Assert.Equal(.5f, ((SolidColorBrush)box.Background).Color.Red, 4);
		Run(.5);
		Assert.Equal(Colors.White, ((SolidColorBrush)box.Background).Color);
	}

	[Fact]
	public void NonLerpableValuesSnapAtEnd()
	{
		var label = new Label { Text = "a", HorizontalTextAlignment = TextAlignment.Start };
		label.Animate(l =>
		{
			l.Text = "b";
			l.HorizontalTextAlignment = TextAlignment.End;
		}, duration: 1);
		Run(.9);
		Assert.Equal("a", label.Text);
		Assert.Equal(TextAlignment.Start, label.HorizontalTextAlignment);
		Run(.1);
		Assert.Equal("b", label.Text);
		Assert.Equal(TextAlignment.End, label.HorizontalTextAlignment);
	}

	public sealed class Vm : INotifyPropertyChanged
	{
		double value = .9;

		public event PropertyChangedEventHandler? PropertyChanged;

		public double Value
		{
			get => value;
			set
			{
				this.value = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
			}
		}
	}

	[Fact]
	public void OneWayBindingIsClearedLikeDirectAssignment()
	{
		var direct = new Label();
		var directVm = new Vm();
		direct.BindingContext = directVm;
		direct.SetBinding(VisualElement.OpacityProperty, static (Vm vm) => vm.Value, BindingMode.OneWay);
		Assert.Equal(.9, direct.Opacity);
		direct.Opacity = .2;
		directVm.Value = .7;
		Assert.Equal(.2, direct.Opacity);

		var animated = new Label();
		var vm = new Vm();
		animated.BindingContext = vm;
		animated.SetBinding(VisualElement.OpacityProperty, static (Vm vm) => vm.Value, BindingMode.OneWay);
		animated.Animate(l => l.Opacity = .2, duration: 1);
		Assert.Equal(.9, animated.Opacity);
		Run(1);
		Assert.Equal(.2, animated.Opacity);
		vm.Value = .7;
		Assert.Equal(.2, animated.Opacity);
	}

	[Fact]
	public void DynamicResourceIsClearedLikeDirectAssignment()
	{
		var label = new Label { Resources = new ResourceDictionary { ["Fade"] = .8 } };
		label.SetDynamicResource(VisualElement.OpacityProperty, "Fade");
		Assert.Equal(.8, label.Opacity);
		label.Animate(l => l.Opacity = .1, duration: 1);
		Run(1);
		Assert.Equal(.1, label.Opacity);
		label.Resources["Fade"] = .3;
		Assert.Equal(.1, label.Opacity);
	}

	[Fact]
	public void AnimateFromBackgroundThreadIsDispatched()
	{
		var label = new Label { Opacity = 1 };
		TestDispatcher.Instance.RequireDispatch = true;
		label.Animate(l => l.Opacity = 0, duration: 1);
		Assert.Equal(1, label.Opacity);
		Assert.Equal(0, AnimationManager.Count);

		TestDispatcher.Instance.RequireDispatch = false;
		TestDispatcher.Instance.Pump();
		Assert.Equal(1, AnimationManager.Count);
		Run(1);
		Assert.Equal(0, label.Opacity);
	}

	[Fact]
	public void AnimatableRegistrationResolvesNonConventionalNames()
	{
		var property = BindableProperty.Create("Odd", typeof(double), typeof(OddView), 0.0);
		AnimatableProperties.Register(property);
		var view = new OddView();
		view.Animate(v => v.SetValue(property, 10.0), duration: 1);
		Run(.5);
		Assert.Equal(5.0, (double)view.GetValue(property), 6);
	}

	public class OddView : ContentView { }
}
