using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// A Chromium instance shared across the end-to-end tests.
/// </summary>
/// <remarks>
/// The viewport is a phone by default. CLAUDE.md: "RTL is the primary direction, not a mirror …
/// Test at mobile width. The daily log is designed mobile-first." Testing at desktop width first
/// would let a broken mobile layout ship.
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser? Browser { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (!E2EEnvironment.IsConfigured)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();

        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    /// <summary>Opens a page at mobile width, in Arabic.</summary>
    public async Task<IPage> NewMobilePageAsync()
    {
        if (Browser is null)
        {
            throw new InvalidOperationException("The browser is not running. Check E2EEnvironment.IsConfigured first.");
        }

        IBrowserContext context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
            IsMobile = false,
            Locale = "ar-EG",
            BaseURL = E2EEnvironment.BaseUrl,
        });

        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "playwright";
}
