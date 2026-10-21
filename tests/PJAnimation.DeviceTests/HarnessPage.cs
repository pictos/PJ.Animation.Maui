namespace PJAnimation.DeviceTests;

/// <summary>Hosts the views under test and shows the results. Tests start once the page is loaded.</summary>
public class HarnessPage : ContentPage
{
	readonly Label status = new() { Text = "Running…", FontSize = 14 };
	bool started;

	public HarnessPage()
	{
		Host = new VerticalStackLayout { Spacing = 8, Padding = 16 };
		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Children = { status, Host },
			},
		};
		Loaded += OnLoaded;
	}

	/// <summary>Container where tests add the views they animate.</summary>
	public VerticalStackLayout Host { get; }

	async void OnLoaded(object? sender, EventArgs e)
	{
		if (started)
			return;
		started = true;
		var results = await DeviceTestRunner.RunAsync(this);
		status.Text = results;
		if (DeviceTestRunner.ShouldExit)
		{
			await Task.Delay(250);
			Application.Current?.Quit();
		}
	}
}
