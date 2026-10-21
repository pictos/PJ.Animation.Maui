namespace PJAnimation.Sample.Pages;

public class HomePage : ContentPage
{
	public HomePage()
	{
		Title = "PJAnimation";
		Content = new VerticalStackLayout
		{
			Padding = 20,
			Spacing = 12,
			Children =
			{
				NavButton("Comet AnimationSample", () => new AnimationSamplePage()),
				NavButton("Stress test", () => new StressPage()),
			},
		};
	}

	Button NavButton(string text, Func<Page> create)
	{
		var button = new Button { Text = text };
		button.Clicked += async (_, _) => await Navigation.PushAsync(create());
		return button;
	}
}
