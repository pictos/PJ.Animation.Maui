using PJAnimation.Sample.Pages;

namespace PJAnimation.Sample;

public class App : Application
{
	protected override Window CreateWindow(IActivationState? activationState)
		=> new(new NavigationPage(new HomePage())) { Title = "PJAnimation Sample" };
}
