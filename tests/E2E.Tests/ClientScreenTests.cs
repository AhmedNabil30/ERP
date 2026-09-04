using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// KAFF-126's client screens, driven. <c>AC-126-A</c>, <c>AC-126-L</c>, and the guard defect D-113 §2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists.</b> KAFF-126 shipped on 2026-09-04 with its criteria checked by hand in a
/// browser and no test behind any of them — D-113 §3 recorded that as owed in as many words:
/// <i>"everything above is evidence from this session, not a check that runs tomorrow."</i> Two of
/// those checks are the ones a future change is most likely to break silently, so they are the ones
/// written down here.
/// </para>
/// <para>
/// <b>The first is a regression test for a bug that a unit test could not have caught.</b>
/// <c>clientManageGuard</c> read <c>AuthService.current()</c> directly and relied on
/// <c>sessionGuard</c>'s position in the <c>canActivate</c> array. In-app navigation worked;
/// <c>/clients/new</c> typed, bookmarked or refreshed found a null session, bounced to <c>/</c>, and
/// the landing then redirected to <c>/clients</c> — <b>the operator asked for a form and silently got
/// a list</b>, with no error anywhere. What was wrong was <i>when</i> the guard ran, so only a real
/// hard load can see it. <see cref="A_bookmarked_client_form_url_loads_the_form_and_not_the_list"/>
/// is that hard load.
/// </para>
/// <para>
/// <b>These tests need a seeded stack</b> — <c>scripts/seed-demo.ps1</c>, whose users and passwords
/// are listed in <c>deploy/DEMO.md</c> §4. They fail rather than skip when the users are missing:
/// a suite that quietly passes against an unseeded database would report a safety it does not have.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public sealed class ClientScreenTests
{
    /// <summary>The bootstrap Owner. The one seeded account that is not forced to change its password.</summary>
    private const string OwnerUser = "owner_demo";
    private const string OwnerPassword = "Demo#Owner1";

    /// <summary>Finance — a real role holding no <c>ClientManage</c>. The refused party in AC-126-L.</summary>
    private const string FinanceUser = "sara_finance_demo";

    /// <summary>What the seed creates it with, <c>mustChangePassword</c> set.</summary>
    private const string FinanceSeedPassword = "Demo#Fin123";

    /// <summary>
    /// What this suite moves it to, once, so the forced-change screen stops standing between Finance
    /// and the route under test. Signing in with the seed password lands on
    /// <c>/change-password</c> — <c>mustChangePasswordGuard</c> runs before
    /// <c>clientManageGuard</c> — which would make AC-126-L unobservable.
    /// </summary>
    private const string FinancePassword = "Demo#Fin456";

    private readonly PlaywrightFixture _playwright;

    public ClientScreenTests(PlaywrightFixture playwright) => _playwright = playwright;

    [E2EFact]
    public async Task A_signed_out_visitor_asking_for_a_client_form_is_sent_to_sign_in()
    {
        // No credentials needed, so this one runs against any stack. `sessionGuard` awaits resolution
        // before deciding — a guard that read the session synchronously would send a signed-in user
        // here too (session.guard.ts documents exactly that).
        IPage page = await _playwright.NewMobilePageAsync();

        await page.GotoAsync("/clients/new");
        await page.WaitForURLAsync("**/sign-in");

        page.Url.Should().EndWith("/sign-in");
    }

    [E2EFact]
    public async Task A_bookmarked_client_form_url_loads_the_form_and_not_the_list()
    {
        // decisions.md D-113 §2. `GotoAsync` is a full document load, not an in-app navigation, which
        // is the whole point: the defect was invisible to router navigation and to any unit test of
        // the guard, because what was wrong was when the guard ran rather than what it decided.
        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, OwnerUser, OwnerPassword);

        await page.GotoAsync("/clients/new");
        await page.GetByTestId("client-form").WaitForAsync();

        page.Url.Should().EndWith(
            "/clients/new",
            "a bookmarked create form must open the form; bouncing to /clients answers a question "
            + "the operator did not ask and says nothing about having done so");

        await page.GetByTestId("client-phone").WaitForAsync();
    }

    [E2EFact]
    public async Task The_client_list_renders_right_to_left_and_does_not_scroll_sideways_at_phone_width()
    {
        // AC-126-A. A horizontal scrollbar at 390px is the usual symptom of a physical CSS property
        // that should have been logical, and Arabic is the product language rather than a translation
        // laid over an English layout.
        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, OwnerUser, OwnerPassword);

        await page.GotoAsync("/clients");
        await page.GetByTestId("client-list").WaitForAsync();

        (await page.Locator("html").GetAttributeAsync("dir")).Should().Be("rtl");

        int scrollWidth = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        int clientWidth = await page.EvaluateAsync<int>("() => document.documentElement.clientWidth");

        scrollWidth.Should().BeLessThanOrEqualTo(clientWidth);
    }

    [E2EFact]
    public async Task A_role_without_client_manage_is_refused_visibly_rather_than_sent_to_its_landing()
    {
        // AC-126-L. `ux/navigation.md`: a refusal "must not render as a crash, a blank page, or a
        // redirect that hides what happened" — and until this test was written the guard returned
        // `parseUrl('/')`, which is the third of those. Finance typed /clients and arrived at their
        // own landing page with nothing said, indistinguishable from a mistyped address.
        await EnsureFinanceCanSignInAsync();

        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, FinanceUser, FinancePassword);

        await page.GotoAsync("/clients");
        await page.GetByTestId("forbidden-page").WaitForAsync();

        page.Url.Should().EndWith("/forbidden");

        string refusal = (await page.Locator("[data-testid='forbidden-page'] [role='alert']")
            .TextContentAsync() ?? string.Empty).Trim();

        // The key itself on screen would mean the catalogue never resolved it — an Arabic-speaking
        // user reading "errors.auth.forbidden" (KAFF-120 rule 8, and AC-126-L's "in their language").
        refusal.Should().NotBe("errors.auth.forbidden");
        refusal.Should().NotBeEmpty();

        await page.GetByTestId("app-title").WaitForAsync();
    }

    [E2EFact]
    public async Task A_server_refusal_on_the_client_form_renders_as_arabic_and_not_as_a_key()
    {
        // KAFF-120 rule 8 — "a key that reaches the screen unresolved is an Arabic-speaking user
        // reading errors.master.…". `AC-120-B` states that for §6.7's refusal specifically, and
        // **that one is unreachable from this form by construction**: the tax field is hidden when
        // the kind is Individual and `payload()` sends `taxRegistrationNumber: null` for one, so the
        // illegal pair AC-120-A refuses cannot be assembled here at all (D-109 §1, AC-126-H). What
        // can be driven is the mechanism both criteria actually depend on — a server refusal reaching
        // `refusalKey()` and being resolved through the catalogue — and this drives it with the one
        // refusal this screen produces from a URL alone.
        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, OwnerUser, OwnerPassword);

        await page.GotoAsync($"/clients/{Guid.NewGuid()}");

        ILocator refusal = page.GetByTestId("client-form-refusal");
        await refusal.WaitForAsync();

        string text = (await refusal.TextContentAsync() ?? string.Empty).Trim();

        text.Should().NotBe("errors.master.client_not_found", "the catalogue must resolve it");
        text.Should().NotBeEmpty();
        text.Should().MatchRegex("\\p{IsArabic}", "the product language is Arabic, not a fallback");
    }

    /// <summary>Signs in through the screen, and waits until the router has left it.</summary>
    /// <remarks>
    /// Located by <c>autocomplete</c> rather than by a test id, because the sign-in screen carries
    /// none and adding one to a shipped screen to satisfy a test is a change to the product for the
    /// benefit of the suite. These attributes are load-bearing for password managers, so they are not
    /// going anywhere quietly.
    /// </remarks>
    private static async Task SignInAsync(IPage page, string userName, string password)
    {
        await page.GotoAsync("/sign-in");

        await page.Locator("input[autocomplete='username']").FillAsync(userName);
        await page.Locator("input[autocomplete='current-password']").FillAsync(password);
        await page.Locator("button[type='submit']").ClickAsync();

        await page.WaitForURLAsync(url => !url.Contains("/sign-in", StringComparison.Ordinal));

        page.Url.Should().NotContain(
            "/sign-in",
            $"{userName} could not sign in — is this stack seeded by scripts/seed-demo.ps1?");
    }

    /// <summary>
    /// Clears Finance's forced password change, once, through the API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotent by trying the changed password first, so the suite survives its own second run —
    /// the failure mode a driver script hit on 2026-09-04 for exactly this reason.
    /// </para>
    /// <para>
    /// <b>Cookies are replayed by hand.</b> The auth cookie is <c>Secure</c> (D-050) and .NET's
    /// <c>CookieContainer</c> refuses to attach a Secure cookie to a plain <c>http://</c> request even
    /// to localhost — a real browser exempts localhost from that rule and a scripted
    /// <c>HttpClient</c> does not. <c>scripts/seed-demo.ps1</c> documents the same trap.
    /// </para>
    /// </remarks>
    private static async Task EnsureFinanceCanSignInAsync()
    {
        using HttpClientHandler handler = new() { UseCookies = false };
        using HttpClient client = new(handler) { BaseAddress = new Uri(E2EEnvironment.ApiBaseUrl) };

        if (await SignInToApiAsync(client, FinancePassword) is not null)
        {
            return;
        }

        string? cookie = await SignInToApiAsync(client, FinanceSeedPassword);

        cookie.Should().NotBeNull(
            $"{FinanceUser} answered neither password — is this stack seeded by scripts/seed-demo.ps1?");

        using HttpRequestMessage change = new(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = FinanceSeedPassword,
                newPassword = FinancePassword,
            }),
        };
        change.Headers.TryAddWithoutValidation("Cookie", cookie);

        using HttpResponseMessage response = await client.SendAsync(change);

        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());
    }

    /// <summary>The session cookie when the password is the right one, null when it is not.</summary>
    private static async Task<string?> SignInToApiAsync(HttpClient client, string password)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { userName = FinanceUser, password });

        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            return null;
        }

        return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.First().Split(';')[0]
            : null;
    }
}
