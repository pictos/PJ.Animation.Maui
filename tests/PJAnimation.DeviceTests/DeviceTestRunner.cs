using System.Diagnostics;
using System.Text;

namespace PJAnimation.DeviceTests;

static class DeviceTestRunner
{
	const string Prefix = "PJANIMATION-DEVICETESTS";

	/// <summary>Android and Mac Catalyst quit after the run (iOS apps cannot quit themselves).</summary>
	public static bool ShouldExit => OperatingSystem.IsAndroid() || OperatingSystem.IsMacCatalyst();

	public static async Task<string> RunAsync(HarnessPage page)
	{
		// RequiresTicking: the test needs frames to run, which is impossible when the OS has animations disabled
		// (e.g. Android animator_duration_scale = 0); such tests are reported as SKIP in that case.
		var tests = new (string Name, bool RequiresTicking, Func<HarnessPage, Task<string?>> Run)[]
		{
			(nameof(Tests.DefaultTickerDrivesFrames), true, Tests.DefaultTickerDrivesFrames),
			(nameof(Tests.TicksRunOnUIThread), true, Tests.TicksRunOnUIThread),
			(nameof(Tests.CaptureAndRestoreLeavesPlatformValueUnchangedBeforeFirstTick), true, Tests.CaptureAndRestoreLeavesPlatformValueUnchangedBeforeFirstTick),
			(nameof(Tests.UnloadedPausesAndLoadedResumes), true, Tests.UnloadedPausesAndLoadedResumes),
			(nameof(Tests.ExplicitPauseSurvivesReload), true, Tests.ExplicitPauseSurvivesReload),
			(nameof(Tests.ReducedMotionJumpsToEndOnPlatformView), false, Tests.ReducedMotionJumpsToEndOnPlatformView),
			(nameof(Tests.BackgroundThreadAnimateIsDispatched), false, Tests.BackgroundThreadAnimateIsDispatched),
		};

		var report = new StringBuilder();
		void Log(string line)
		{
			Console.WriteLine($"{Prefix} {line}");
			report.AppendLine(line);
		}

		Log($"START platform={DeviceInfo.Platform} version={DeviceInfo.VersionString} model={DeviceInfo.Model} systemReducedMotion={SystemReducedMotionProvider.Instance.IsReducedMotionEnabled}");
		var passed = 0;
		var failed = 0;
		var skipped = 0;
		foreach (var (name, requiresTicking, run) in tests)
		{
			// The user's reduced-motion preference must not turn the ticking tests into jump-to-end tests.
			AnimationManager.RespectReducedMotion = !requiresTicking;
			if (requiresTicking && !AnimationManager.Ticker.SystemEnabled)
			{
				skipped++;
				Log($"SKIP {name} - system animations are disabled");
				continue;
			}
			var sw = Stopwatch.StartNew();
			string? detail;
			bool ok;
			try
			{
				detail = await run(page);
				ok = true;
			}
			catch (Exception ex)
			{
				detail = ex.GetType().Name + ": " + ex.Message;
				ok = false;
			}
			finally
			{
				page.Host.Children.Clear();
				AnimationManager.RespectReducedMotion = true;
			}
			if (ok) passed++; else failed++;
			Log($"{(ok ? "PASS" : "FAIL")} {name} ({sw.ElapsedMilliseconds} ms){(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
		}
		Log($"DONE passed={passed} failed={failed} skipped={skipped}");

		var path = Environment.GetEnvironmentVariable("PJANIMATION_RESULTS");
		if (string.IsNullOrEmpty(path))
			path = Path.Combine(FileSystem.AppDataDirectory, "devicetest-results.txt");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, report.ToString());
			Console.WriteLine($"{Prefix} RESULTS {path}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"{Prefix} RESULTS-WRITE-FAILED {ex.Message}");
		}
		return report.ToString();
	}
}
