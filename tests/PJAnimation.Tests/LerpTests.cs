namespace PJAnimation.Tests;

// Ported from Comet tests/Comet.Tests/Animations/LerpTests.cs plus PJAnimation additions.
public class LerpTests : TestBase
{
	[Fact]
	public void DoubleLerps()
	{
		double start = 0;
		double end = 1;
		var lerp = Lerp.GetLerp(start.GetType())!;
		foreach (var progress in new[] { .1, .2, .33333, .5, 1 })
		{
			var value = (double)lerp.Calculate(start, end, progress)!;
			Assert.Equal(progress, value);
		}
	}

	[Fact]
	public void DoubleLerpsFromLargeToSmall()
	{
		double start = 1;
		double end = 0;
		var lerp = Lerp.GetLerp(start.GetType())!;
		foreach (var progress in new[] { .1, .2, .33333, .5, 1 })
		{
			var value = (double)lerp.Calculate(start, end, progress)!;
			Assert.Equal(1 - progress, value);
		}
	}

	[Fact]
	public void BoolLerps()
	{
		bool start = false;
		bool end = true;
		var lerp = Lerp.GetLerp(start.GetType())!;
		Assert.False((bool)lerp.Calculate(start, end, .1)!);
		Assert.False((bool)lerp.Calculate(start, end, .2)!);
		Assert.False((bool)lerp.Calculate(start, end, .4999999)!);
		Assert.True((bool)lerp.Calculate(start, end, .5)!);
		Assert.True((bool)lerp.Calculate(start, end, .75)!);
		Assert.True((bool)lerp.Calculate(start, end, 1)!);
	}

	[Fact]
	public void LongLerpsWithoutThrowing()
	{
		var lerp = Lerp.GetLerp(typeof(long))!;
		Assert.Equal(5_000_000_000L, lerp.Calculate(0L, 10_000_000_000L, .5));
		Assert.Equal(25L, lerp.Calculate(50L, 0L, .5));
	}

	[Theory]
	[InlineData(typeof(int))]
	[InlineData(typeof(short))]
	[InlineData(typeof(byte))]
	[InlineData(typeof(sbyte))]
	[InlineData(typeof(ushort))]
	[InlineData(typeof(uint))]
	[InlineData(typeof(ulong))]
	[InlineData(typeof(float))]
	[InlineData(typeof(decimal))]
	public void NumericLerpsKeepTypeInBothDirections(Type type)
	{
		var lerp = Lerp.GetLerp(type)!;
		var ten = Convert.ChangeType(10, type);
		var twenty = Convert.ChangeType(20, type);
		var up = lerp.Calculate(ten, twenty, .5)!;
		var down = lerp.Calculate(twenty, ten, .5)!;
		Assert.IsType(type, up);
		Assert.IsType(type, down);
		Assert.Equal(15.0, Convert.ToDouble(up));
		Assert.Equal(15.0, Convert.ToDouble(down));
	}

	[Fact]
	public void ColorLerps()
	{
		var value = (Color)Lerp.GetLerp(typeof(Color))!.Calculate(new Color(0f, 0f, 0f, 0f), new Color(1f, .5f, 0f, 1f), .5)!;
		Assert.Equal(.5f, value.Red, 4);
		Assert.Equal(.25f, value.Green, 4);
		Assert.Equal(0f, value.Blue, 4);
		Assert.Equal(.5f, value.Alpha, 4);
	}

	[Fact]
	public void NullColorFadesFromTransparentVersionOfOtherColor()
	{
		var value = (Color)Lerp.GetLerp(typeof(Color))!.Calculate(null, Colors.Blue, .5)!;
		Assert.Equal(0f, value.Red, 4);
		Assert.Equal(1f, value.Blue, 4);
		Assert.Equal(.5f, value.Alpha, 4);
	}

	[Fact]
	public void ColorLerpExtensionMatchesComet()
	{
		var value = Colors.Black.Lerp(.5, Colors.White);
		Assert.Equal(.5f, value.Red, 4);
		Assert.Equal(.5f, value.Green, 4);
		Assert.Equal(.5f, value.Blue, 4);
		Assert.Equal(1f, value.Alpha, 4);

		var fromNull = ((Color?)null).Lerp(1, null);
		Assert.Equal(Colors.Black, fromNull);
	}

	[Fact]
	public void MauiStructsLerp()
	{
		Assert.Equal(new Thickness(5, 10, 15, 20), Lerp.GetLerp(typeof(Thickness))!.Calculate(new Thickness(0), new Thickness(10, 20, 30, 40), .5));
		Assert.Equal(new CornerRadius(2, 4, 6, 8), Lerp.GetLerp(typeof(CornerRadius))!.Calculate(new CornerRadius(0), new CornerRadius(4, 8, 12, 16), .5));
		Assert.Equal(new Point(5, -5), Lerp.GetLerp(typeof(Point))!.Calculate(new Point(0, 0), new Point(10, -10), .5));
		Assert.Equal(new Size(50, 25), Lerp.GetLerp(typeof(Size))!.Calculate(new Size(0, 0), new Size(100, 50), .5));
		Assert.Equal(new Rect(1, 2, 3, 4), Lerp.GetLerp(typeof(Rect))!.Calculate(new Rect(0, 0, 0, 0), new Rect(2, 4, 6, 8), .5));
	}

	[Fact]
	public void SolidColorBrushLerps()
	{
		var value = (SolidColorBrush)Lerp.GetLerp(typeof(SolidColorBrush))!.Calculate(
			new SolidColorBrush(Colors.Black), new SolidColorBrush(Colors.White), .5)!;
		Assert.Equal(.5f, value.Color.Red, 4);
	}

	class BaseValue { public double V; }
	sealed class DerivedValue : BaseValue { }
	sealed class Unknown { }

	[Fact]
	public void GetLerpWalksBaseTypesAndSupportsRegistration()
	{
		Assert.Null(Lerp.GetLerp(typeof(DerivedValue)));
		Lerp.Register<BaseValue>((s, e, p) => new BaseValue { V = s.V + ((e.V - s.V) * p) });
		try
		{
			var lerp = Lerp.GetLerp(typeof(DerivedValue));
			Assert.NotNull(lerp);
			var result = (BaseValue)lerp.Calculate(new DerivedValue { V = 0 }, new DerivedValue { V = 10 }, .3)!;
			Assert.Equal(3, result.V, 6);
		}
		finally
		{
			Lerp.Lerps.Remove(typeof(BaseValue));
		}
	}

	[Fact]
	public void UnknownTypesSnapAtEnd()
	{
		Assert.Null(Lerp.GetLerp(typeof(Unknown)));
		var start = new Unknown();
		var end = new Unknown();
		var animation = new Animation { StartValue = start, EndValue = end, Duration = 1 };
		animation.Update(.5);
		Assert.Same(start, animation.CurrentValue);
		animation.Update(.99);
		Assert.Same(start, animation.CurrentValue);
		animation.Update(1);
		Assert.Same(end, animation.CurrentValue);
		Assert.True(animation.HasFinished);

		Assert.Same(start, Lerp.Discrete.Calculate(start, end, .999));
		Assert.Same(end, Lerp.Discrete.Calculate(start, end, 1));
	}
}
