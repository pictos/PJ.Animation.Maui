using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

namespace PJAnimation;

/// <summary>
/// Tracks which animation owns each (target, property) pair. A newer animation claiming a track
/// makes the previous owner cancel itself on its next tick (conflict policy: newest wins).
/// </summary>
static class AnimationTracks
{
	static readonly ConditionalWeakTable<BindableObject, Dictionary<BindableProperty, object>> tracks = new();

	/// <summary>Unconditionally takes ownership of a track.</summary>
	public static void Claim(BindableObject target, BindableProperty property, object token)
		=> tracks.GetOrCreateValue(target)[property] = token;

	/// <summary>Takes ownership if the track is free or already owned by <paramref name="token"/>.</summary>
	public static bool TryClaim(BindableObject target, BindableProperty property, object token)
	{
		var map = tracks.GetOrCreateValue(target);
		if (map.TryGetValue(property, out var owner))
			return ReferenceEquals(owner, token);
		map[property] = token;
		return true;
	}

	public static void Release(BindableObject target, BindableProperty property, object token)
	{
		if (tracks.TryGetValue(target, out var map) && map.TryGetValue(property, out var owner) && ReferenceEquals(owner, token))
			map.Remove(property);
	}

	public static bool IsOwned(BindableObject target, BindableProperty property)
		=> tracks.TryGetValue(target, out var map) && map.ContainsKey(property);
}
