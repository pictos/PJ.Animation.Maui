using BenchmarkDotNet.Attributes;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PJAnimation;

namespace PJAnimation.Benchmarks;

/// <summary>Cost of one interpolation through the boxed <see cref="Lerp.LerpDelegate"/> (what the tick loop calls).</summary>
[MemoryDiagnoser]
public class LerpBenchmarks
{
	const double Progress = .37;

	readonly object doubleStart = 0.0, doubleEnd = 100.0;
	readonly object intStart = 0, intEnd = 100;
	readonly object boolStart = false, boolEnd = true;
	readonly object colorStart = Colors.Red, colorEnd = Colors.Blue;
	readonly object brushStart = new SolidColorBrush(Colors.Red), brushEnd = new SolidColorBrush(Colors.Blue);
	readonly object thicknessStart = new Thickness(0), thicknessEnd = new Thickness(10, 20, 30, 40);

	Lerp.LerpDelegate doubleLerp = null!, intLerp = null!, boolLerp = null!, colorLerp = null!, brushLerp = null!, thicknessLerp = null!;

	[GlobalSetup]
	public void Setup()
	{
		doubleLerp = Lerp.GetLerp(typeof(double))!.Calculate;
		intLerp = Lerp.GetLerp(typeof(int))!.Calculate;
		boolLerp = Lerp.GetLerp(typeof(bool))!.Calculate;
		colorLerp = Lerp.GetLerp(typeof(Color))!.Calculate;
		brushLerp = Lerp.GetLerp(typeof(SolidColorBrush))!.Calculate;
		thicknessLerp = Lerp.GetLerp(typeof(Thickness))!.Calculate;
	}

	[Benchmark(Baseline = true)] public object? Double() => doubleLerp(doubleStart, doubleEnd, Progress);
	[Benchmark] public object? Int() => intLerp(intStart, intEnd, Progress);
	[Benchmark] public object? Bool() => boolLerp(boolStart, boolEnd, Progress);
	[Benchmark] public object? Color() => colorLerp(colorStart, colorEnd, Progress);
	[Benchmark] public object? SolidColorBrush() => brushLerp(brushStart, brushEnd, Progress);
	[Benchmark] public object? Thickness() => thicknessLerp(thicknessStart, thicknessEnd, Progress);

	[Benchmark] public object? EasingCubicInOutPlusDouble() => doubleLerp(doubleStart, doubleEnd, Easing.CubicInOut.Ease(Progress));
	[Benchmark] public object? EasingSpringOutPlusColor() => colorLerp(colorStart, colorEnd, Easing.SpringOut.Ease(Progress));
}
