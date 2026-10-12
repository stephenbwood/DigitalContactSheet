using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(DigitalContactSheet.App.Tests.TestAppBuilder))]

namespace DigitalContactSheet.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
