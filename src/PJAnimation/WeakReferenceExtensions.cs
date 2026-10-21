using System;

namespace PJAnimation;

/// <summary>Helpers for <see cref="WeakReference{T}"/>.</summary>
static class WeakReferenceExtensions
{
	/// <summary>Returns the referenced object, or <see langword="null"/> if it has been collected.</summary>
	public static T? GetValueOrDefault<T>(this WeakReference<T>? weak) where T : class
		=> weak is not null && weak.TryGetTarget(out var value) ? value : null;
}
