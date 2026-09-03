using System.Net.Http;
using System.Text.Json;
using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// The slice 0/1 demo script: the application loads, in Arabic, right to left, at phone width, sends
/// an unauthenticated visitor to sign in, and the API it talks to reports its database guards intact.
/// </summary>
/// <remarks>
/// <para>
/// <b>Repaired 2026-09-03, after KAFF-125 (D-104) replaced the status page at <c>/</c> with the
/// role-based landing (D-104, S-004's dispatch).</b> The status page component that these tests used
/// to assert against — <c>data-testid="status-guards"</c>, <c>"status-panel"</c> — is deleted, not
/// merely unrouted: nothing pointed at it since KAFF-125 shipped, its own template referenced
/// <c>status.*</c> i18n keys that were already gone from both catalogues, and no other file imported
/// it. A route with nothing behind it and a component with no route are the same defect from opposite
/// ends; agents.md's "decide, don't leave it undecided a second time" applies to both.
/// </para>
/// <para>
/// <b>Two deliberate choices, both taken rather than one:</b>
/// </para>
/// <list type="number">
/// <item>The database-guards assertion now hits <c>GET /api/health</c> directly — the same endpoint
/// <c>driver.mjs smoke</c> already asserts, and the thing CLAUDE.md actually cares about (D-033's
/// database-enforced safety) has never been a screen's job to prove. A screen that renders the guard
/// state is a convenience for a human, not the assertion's rightful home.</item>
/// <item>The landing route's own surface is asserted too, on what the application does today rather
/// than what the old status page did: an unauthenticated visit to <c>/</c> is sent to
/// <c>/sign-in</c> by <c>sessionGuard</c> (D-104, <c>AC-125-B</c>). That is real, current, and
/// unauthenticated-reachable behaviour — signing a user in to reach the role-based landing itself
/// belongs to a flow script, not this suite (see <c>driver.mjs flow</c> and the demo runbook).</item>
/// </list>
/// <para>
/// Every assertion here can fail: a guard genuinely missing turns the health check red with the real
/// missing-guard names in the message; a broken <c>sessionGuard</c> leaves the page on <c>/</c> or
/// sends it somewhere else and the URL assertion misses; a deleted <c>data-testid="app-title"</c> or
/// a reintroduced physical CSS property both still fail the tests that depend on them. agents.md §3c.
/// </para>
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
    public async Task An_unauthenticated_visit_to_the_landing_route_is_sent_to_sign_in()
    {
        // D-104: `sessionGuard` on the `''` route awaits session resolution and bounces a signed-out
        // visitor to `/sign-in` (AC-125-B) rather than rendering anything at `/` itself. This is what
        // a smoke test should assert about the landing route now that it dispatches by role instead of
        // being a page of its own.
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/");
        await page.WaitForURLAsync("**/sign-in");

        page.Url.Should().EndWith("/sign-in");
    }

    [E2EFact]
    public async Task The_health_endpoint_reports_the_database_guards_are_installed()
    {
        // Asserted against the API directly, not a screen — see the class remarks. KAFF_API mirrors
        // driver.mjs's own variable name and default so the two never point at different hosts.
        using HttpClient client = new();

        using HttpResponseMessage response = await client.GetAsync($"{E2EEnvironment.ApiBaseUrl}/api/health");
        string body = await response.Content.ReadAsStringAsync();

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        root.GetProperty("status").GetString().Should().Be("healthy", body);
        root.GetProperty("guardsInstalled").GetBoolean().Should().BeTrue(body);
        root.GetProperty("missingGuards").GetArrayLength().Should().Be(0, body);
    }

    [E2EFact]
    public async Task The_page_does_not_scroll_sideways_at_phone_width()
    {
        // A horizontal scrollbar at 390px is the usual symptom of a physical CSS property that should
        // have been logical. Waits on the header title — rendered in every session state (D-104's
        // `App` shell), unlike the deleted status page's own panel.
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/");
        await page.GetByTestId("app-title").WaitForAsync();

        int scrollWidth = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        int clientWidth = await page.EvaluateAsync<int>("() => document.documentElement.clientWidth");

        scrollWidth.Should().BeLessThanOrEqualTo(clientWidth);
    }
}
