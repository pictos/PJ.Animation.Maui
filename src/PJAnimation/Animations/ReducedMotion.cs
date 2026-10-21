using System;

namespace PJAnimation;

/// <summary>Reports whether the user asked the system to reduce motion.</summary>
/// <remarks>Queried on the UI thread when an animation is added and once per frame.</remarks>
public interface IReducedMotionProvider
{
	/// <summary><see langword="true"/> when animations should be skipped (jump to their end state).</summary>
	bool IsReducedMotionEnabled { get; }
}

/// <summary>
/// Reads the platform setting:
/// <list type="bullet">
/// <item>iOS / Mac Catalyst: <c>UIAccessibility.IsReduceMotionEnabled</c> (read on the main thread; other threads get the last value read).</item>
/// <item>Android: <c>!ValueAnimator.AreAnimatorsEnabled()</c> on API 26+ (animator duration scale 0 or battery saver);
/// below API 26, <c>Settings.Global.AnimatorDurationScale == 0</c>.</item>
/// <item>Windows: <c>!UISettings.AnimationsEnabled</c> ("Animation effects" setting).</item>
/// <item>Other targets (<c>net10.0</c>): always <see langword="false"/>.</item>
/// </list>
/// </summary>
public sealed class SystemReducedMotionProvider : IReducedMotionProvider
{
	SystemReducedMotionProvider()
	{
	}

	/// <summary>The shared instance.</summary>
	public static SystemReducedMotionProvider Instance { get; } = new();

	/// <inheritdoc/>
	public bool IsReducedMotionEnabled
	{
		get
		{
			try
			{
				return Read();
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				return false;
			}
		}
	}

#if IOS || MACCATALYST
	static bool cached;

	static bool Read()
	{
		if (Foundation.NSThread.IsMain)
			return cached = UIKit.UIAccessibility.IsReduceMotionEnabled;
		CoreFoundation.DispatchQueue.MainQueue.DispatchAsync(static () => cached = UIKit.UIAccessibility.IsReduceMotionEnabled);
		return cached;
	}
#elif ANDROID
	static bool Read()
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
			return !Android.Animation.ValueAnimator.AreAnimatorsEnabled();
		var resolver = Android.App.Application.Context.ContentResolver;
		return resolver is not null
			&& Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
	}
#elif WINDOWS
	static Windows.UI.ViewManagement.UISettings? settings;

	static bool Read()
	{
		settings ??= new Windows.UI.ViewManagement.UISettings();
		return !settings.AnimationsEnabled;
	}
#else
	static bool Read() => false;
#endif
}
