using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// The slice 0 demo script: the application loads, in Arabic, right to left, at phone width, and
/// reports that the API and its database guards are healthy.
/// </summary>
/// <remarks>
/// The per-slice demo scripts CLAUDE.md asks for arrive with their slices, written by the Verifier.
/// This one proves the harness: Playwright runs, the browser reaches the app, and the assertions fire.
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public sealed class SmokeTests
{
    private readonly PlaywrightFixture _playwright;

    public SmokeTests(PlaywrightFixture playwright) => _playwright = playwright;

    [E2EFact]
    public async Task The_application_opens_in_arabic_right_to_left()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/");

        ILocator html = page.Locator("html");

        (await html.GetAttributeAsync("dir")).Should().Be("rtl");
        (await html.GetAttributeAsync("lang")).Should().Be("ar");
    }

    [E2EFact]
    public async Task The_shell_renders_its_arabic_title_from_the_translation_catalogue()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/");

        ILocator title = page.GetByTestId("app-title");
        await title.WaitForAsync();

        string text = (await title.TextContentAsync() ?? string.Empty).Trim();

        // "app.name" would mean the catalogue failed to load and the key fell through.
        text.Should().NotBe("app.name");
        text.Should().Be("كف");
    }

    [E2EFact]
    public async Task The_status_page_reports_the_database_guards_are_installed()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/");

        ILocator guards = page.GetByTestId("status-guards");
        await guards.WaitForAsync();

        (await guards.TextContentAsync())?.Trim().Should().Be("مفعّلة");
    }

    [E2EFact]
    public async Task The_page_does_not_scroll_sideways_at_phone_width()
    {
        // A horizontal scrollbar at 390px is the usual symptom of a physical CSS property that should
        // have been logical.
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/");
        await page.GetByTestId("status-panel").WaitForAsync();

        int scrollWidth = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        int clientWidth = await page.EvaluateAsync<int>("() => document.documentElement.clientWidth");

        scrollWidth.Should().BeLessThanOrEqualTo(clientWidth);
    }
}
