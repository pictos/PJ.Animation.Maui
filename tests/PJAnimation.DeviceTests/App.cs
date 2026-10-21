namespace PJAnimation.DeviceTests;

public class App : Application
{
	protected override Window CreateWindow(IActivationState? activationState)
		=> new(new HarnessPage()) { Title = "PJAnimation Device Tests" };
}
