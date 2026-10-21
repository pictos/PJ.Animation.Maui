using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace PJAnimation;

/// <summary>A captured bindable property change.</summary>
readonly record struct CapturedChange(BindableObject Target, BindableProperty Property, object? OldValue, object? NewValue);

/// <summary>
/// Emulates Comet's change monitoring with public <see cref="BindableObject"/> APIs: while the user action runs,
/// <c>PropertyChanging</c> (old value) and <c>PropertyChanged</c> (new value) are observed on every object in scope.
/// Afterwards the old values are restored so nothing visible changes before the first animation frame.
/// </summary>
sealed class PropertyCapture
{
	[ThreadStatic]
	static bool isCapturing;

	readonly Dictionary<(BindableObject, BindableProperty), int> index = new();
	readonly List<CapturedChange> changes = new();

	PropertyCapture()
	{
	}

	/// <summary>
	/// Runs <paramref name="action"/> capturing bindable property changes made to <paramref name="root"/>, its visual
	/// descendants and <paramref name="alsoCapture"/>, restores the old values and returns the effective changes
	/// (in order of first change; changes whose final value equals the old value are skipped).
	/// The returned array is exactly sized; it's empty when nothing changed.
	/// </summary>
	public static CapturedChange[] Capture(BindableObject root, IEnumerable<BindableObject>? alsoCapture, Action action)
	{
		ArgumentNullException.ThrowIfNull(root);
		ArgumentNullException.ThrowIfNull(action);
		if (isCapturing)
			throw new InvalidOperationException("Animate cannot be called from inside another Animate action.");

		var scope = BuildScope(root, alsoCapture);
		var capture = new PropertyCapture();
		Microsoft.Maui.Controls.PropertyChangingEventHandler changing = capture.OnPropertyChanging;
		PropertyChangedEventHandler changed = capture.OnPropertyChanged;

		isCapturing = true;
		foreach (var obj in scope)
		{
			obj.PropertyChanging += changing;
			obj.PropertyChanged += changed;
		}
		try
		{
			action();
		}
		finally
		{
			foreach (var obj in scope)
			{
				obj.PropertyChanging -= changing;
				obj.PropertyChanged -= changed;
			}
			isCapturing = false;
		}

		var captured = CollectionsMarshal.AsSpan(capture.changes);
		var count = 0;
		foreach (ref readonly var c in captured)
		{
			if (!Equals(c.OldValue, c.NewValue))
				count++;
		}
		if (count == 0)
			return [];

		var result = new CapturedChange[count];
		var span = result.AsSpan();
		var n = 0;
		foreach (ref readonly var c in captured)
		{
			if (!Equals(c.OldValue, c.NewValue))
				span[n++] = c;
		}

		for (var i = span.Length - 1; i >= 0; i--)
		{
			ref readonly var c = ref span[i];
			c.Target.SetValue(c.Property, c.OldValue);
		}

		return result;
	}

	static List<BindableObject> BuildScope(BindableObject root, IEnumerable<BindableObject>? alsoCapture)
	{
		var seen = new HashSet<BindableObject>(ReferenceEqualityComparer.Instance);
		var scope = new List<BindableObject>();
		AddTree(root, seen, scope);
		if (alsoCapture is not null)
		{
			foreach (var extra in alsoCapture)
			{
				if (extra is not null && seen.Add(extra))
					scope.Add(extra);
			}
		}
		return scope;

		static void AddTree(BindableObject obj, HashSet<BindableObject> seen, List<BindableObject> scope)
		{
			if (!seen.Add(obj))
				return;
			scope.Add(obj);
			if (obj is IVisualTreeElement element)
			{
				foreach (var child in element.GetVisualChildren())
				{
					if (child is BindableObject bo)
						AddTree(bo, seen, scope);
				}
			}
		}
	}

	void OnPropertyChanging(object? sender, Microsoft.Maui.Controls.PropertyChangingEventArgs e)
	{
		if (sender is not BindableObject obj)
			return;
		var bp = BindablePropertyResolver.Resolve(obj.GetType(), e.PropertyName);
		if (bp is null || bp.IsReadOnly)
			return;
		var key = (obj, bp);
		if (index.ContainsKey(key))
			return;
		var old = obj.GetValue(bp);
		index[key] = changes.Count;
		changes.Add(new CapturedChange(obj, bp, old, old));
	}

	void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not BindableObject obj)
			return;
		var bp = BindablePropertyResolver.Resolve(obj.GetType(), e.PropertyName);
		if (bp is null || !index.TryGetValue((obj, bp), out var i))
			return;
		ref var c = ref CollectionsMarshal.AsSpan(changes)[i];
		c = c with { NewValue = obj.GetValue(bp) };
	}
}
