using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Maui.Controls;

namespace PJAnimation;

/// <summary>
/// Lets apps make <see cref="BindableProperty"/> instances discoverable for animation capture without
/// relying on reflection (useful for trimmed/NativeAOT apps or properties not exposed as public static
/// <c>XxxProperty</c> fields).
/// </summary>
public static class AnimatableProperties
{
	/// <summary>Registers <paramref name="property"/> for its <see cref="BindableProperty.DeclaringType"/> and derived types.</summary>
	public static void Register(BindableProperty property)
	{
		ArgumentNullException.ThrowIfNull(property);
		BindablePropertyResolver.Register(property);
	}
}

/// <summary>Resolves the <see cref="BindableProperty"/> behind a property-change notification name.</summary>
static class BindablePropertyResolver
{
	static readonly ConcurrentDictionary<(Type Type, string Name), BindableProperty> registered = new();
	static readonly ConcurrentDictionary<(Type Type, string Name), BindableProperty?> cache = new();

	public static void Register(BindableProperty property)
	{
		registered[(property.DeclaringType, property.PropertyName)] = property;
		cache.Clear();
	}

	public static BindableProperty? Resolve(Type type, string? propertyName)
	{
		if (string.IsNullOrEmpty(propertyName))
			return null;
		return cache.GetOrAdd((type, propertyName), static key => ResolveCore(key.Type, key.Name));
	}

	static BindableProperty? ResolveCore(Type type, string name)
	{
		for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
		{
			if (registered.TryGetValue((t, name), out var bp))
				return bp;
		}

		for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
		{
			if (FindField(t, name) is { } bp)
				return bp;
		}

		return null;
	}

	[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Missing fields degrade gracefully: the change is applied without animation.")]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Missing fields degrade gracefully: the change is applied without animation.")]
	static BindableProperty? FindField(Type type, string name)
	{
		var field = type.GetField(name + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
		if (field is not null && typeof(BindableProperty).IsAssignableFrom(field.FieldType)
			&& field.GetValue(null) is BindableProperty bp && bp.PropertyName == name)
			return bp;
		return null;
	}
}
