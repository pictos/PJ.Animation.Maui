using System;
using System.Collections.Generic;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace PJAnimation;

/// <summary>
/// Linear interpolation ("lerp") registry. Maps a value type to a function that computes the
/// value between a start and an end value for a given (already eased) progress.
/// </summary>
/// <remarks>
/// The registry is not thread-safe; register custom lerps at startup or on the UI thread.
/// Types without a registered lerp are animated with <see cref="Discrete"/> (snap to the end value
/// when the animation completes).
/// </remarks>
public class Lerp
{
	/// <summary>Computes an interpolated value.</summary>
	/// <param name="start">Start value.</param>
	/// <param name="end">End value.</param>
	/// <param name="progress">Eased progress, usually in [0, 1] (may overshoot for elastic easings).</param>
	public delegate object? LerpDelegate(object? start, object? end, double progress);

	/// <summary>The registered lerps, keyed by value type.</summary>
	public static readonly Dictionary<Type, Lerp> Lerps = new()
	{
		[typeof(int)] = Create<int>((s, e, p) => (int)((e - s) * p) + s),
		[typeof(short)] = Create<short>((s, e, p) => (short)((int)((e - s) * p) + s)),
		[typeof(byte)] = Create<byte>((s, e, p) => (byte)((int)((e - s) * p) + s)),
		[typeof(sbyte)] = Create<sbyte>((s, e, p) => (sbyte)((int)((e - s) * p) + s)),
		[typeof(ushort)] = Create<ushort>((s, e, p) => (ushort)((int)((e - s) * p) + s)),
		[typeof(uint)] = Create<uint>((s, e, p) => (uint)((long)(((long)e - s) * p) + s)),
		[typeof(long)] = Create<long>((s, e, p) => (long)((e - s) * p) + s),
		[typeof(ulong)] = Create<ulong>((s, e, p) => e >= s ? s + (ulong)((e - s) * p) : s - (ulong)((s - e) * p)),
		[typeof(float)] = Create<float>((s, e, p) => (float)((e - s) * p) + s),
		[typeof(double)] = Create<double>((s, e, p) => ((e - s) * p) + s),
		[typeof(decimal)] = Create<decimal>((s, e, p) => ((e - s) * (decimal)p) + s),
		[typeof(bool)] = new Lerp { Calculate = static (s, e, p) => p < .5 ? s : e },
		[typeof(Color)] = new Lerp { Calculate = (s, e, p) => LerpColor(s as Color, e as Color, p) },
		[typeof(SolidColorBrush)] = new Lerp
		{
			Calculate = (s, e, p) => new SolidColorBrush(LerpColor((s as SolidColorBrush)?.Color, (e as SolidColorBrush)?.Color, p)),
		},
		[typeof(Thickness)] = Create<Thickness>((s, e, p) => new Thickness(
			L(s.Left, e.Left, p), L(s.Top, e.Top, p), L(s.Right, e.Right, p), L(s.Bottom, e.Bottom, p))),
		[typeof(CornerRadius)] = Create<CornerRadius>((s, e, p) => new CornerRadius(
			L(s.TopLeft, e.TopLeft, p), L(s.TopRight, e.TopRight, p), L(s.BottomLeft, e.BottomLeft, p), L(s.BottomRight, e.BottomRight, p))),
		[typeof(Point)] = Create<Point>((s, e, p) => new Point(L(s.X, e.X, p), L(s.Y, e.Y, p))),
		[typeof(Size)] = Create<Size>((s, e, p) => new Size(L(s.Width, e.Width, p), L(s.Height, e.Height, p))),
		[typeof(Rect)] = Create<Rect>((s, e, p) => new Rect(L(s.X, e.X, p), L(s.Y, e.Y, p), L(s.Width, e.Width, p), L(s.Height, e.Height, p))),
	};

	/// <summary>
	/// A lerp that keeps the start value until the animation completes and then snaps to the end value.
	/// Used for types without a registered lerp.
	/// </summary>
	public static Lerp Discrete { get; } = new Lerp();

	/// <summary>
	/// Gets the lerp registered for <paramref name="type"/> or the nearest registered base type,
	/// or <see langword="null"/> when none is registered.
	/// </summary>
	public static Lerp? GetLerp(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		for (Type? t = type; t is not null; t = t.BaseType)
		{
			if (Lerps.TryGetValue(t, out var lerp))
				return lerp;
		}
		return null;
	}

	/// <summary>Registers (or replaces) the lerp for <paramref name="type"/>.</summary>
	public static void Register(Type type, LerpDelegate calculate)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(calculate);
		Lerps[type] = new Lerp { Calculate = calculate };
	}

	/// <summary>Registers (or replaces) a strongly typed lerp for <typeparamref name="T"/>.</summary>
	public static void Register<T>(Func<T, T, double, T> calculate)
	{
		ArgumentNullException.ThrowIfNull(calculate);
		Lerps[typeof(T)] = Create(calculate);
	}

	/// <summary>The interpolation function. Defaults to the <see cref="Discrete"/> behavior.</summary>
	public LerpDelegate Calculate { get; set; } = static (start, end, progress) => progress >= 1 ? end : start;

	static Lerp Create<T>(Func<T, T, double, T> calculate)
		=> new() { Calculate = (s, e, p) => calculate((T)s!, (T)e!, p) };

	static double L(double start, double end, double progress) => ((end - start) * progress) + start;

	static Color LerpColor(Color? start, Color? end, double progress)
	{
		start ??= end?.WithAlpha(0) ?? Colors.Transparent;
		end ??= start.WithAlpha(0);
		return start.Lerp(progress, end);
	}
}
