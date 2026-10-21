using Foundation;

namespace PJAnimation.DeviceTests;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
