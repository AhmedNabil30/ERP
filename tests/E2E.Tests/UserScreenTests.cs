using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// KAFF-127's user-management screens, driven. <c>AC-127-A</c>, <c>AC-127-B</c>, <c>AC-127-G</c>,
/// <c>AC-127-I</c> — and <c>V-33-E</c>'s portal half, which nothing in this repository could drive.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>AC-127-I</c> exists because KAFF-126 shipped without an E2E test and it had to be paid back
/// the next day</b> (D-114 §4). The story writes the criterion down so that is not rediscovered a
/// third time: <i>"the evidence a build session produces is not a check that runs tomorrow."</i>
/// </para>
/// <para>
/// <b>These tests need a seeded stack</b> — <c>scripts/seed-demo.ps1</c>, whose users and passwords
/// are listed in <c>deploy/DEMO.md</c> §4. They fail rather than skip when the users are missing: a
/// suite that quietly passed against an unseeded database would report a safety it does not have.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public sealed class UserScreenTests
{
    /// <summary>The bootstrap Owner. The one seeded account that is not forced to change its password.</summary>
    private const string OwnerUser = "owner_demo";
    private const string OwnerPassword = "Demo#Owner1";

    /// <summary>Finance — a real role holding no <c>UserManage</c>. The refused party in AC-127-G.</summary>
    private const string FinanceUser = "sara_finance_demo";
    private const string FinanceSeedPassword = "Demo#Fin123";

    /// <summary>
    /// What <c>ClientScreenTests</c> moves Finance to, once, and what this suite must therefore use.
    /// </summary>
    /// <remarks>
    /// Both suites share one seeded stack and one Finance account, so the password is shared too.
    /// <see cref="EnsureFinanceCanSignInAsync"/> tries the changed one first and falls back to the
    /// seed password, which makes each suite survive the other running first, or second, or alone.
    /// </remarks>
    private const string FinancePassword = "Demo#Fin456";

    /// <summary>
    /// The portal client seeded for <c>V-33-E</c> — <c>Role.Client</c>, scoped to <c>C-10001</c>.
    /// </summary>
    /// <remarks>
    /// Until 2026-09-05 <c>scripts/seed-demo.ps1</c> created no <c>Role.Client</c> user at all, so
    /// spec.md §12's boundary had no UI-level evidence anywhere in this repository. The account exists
    /// to be <b>refused</b>, and the credentials below are the correct ones — which is the whole point
    /// of <see cref="A_portal_client_is_refused_the_staff_host_indistinguishably_from_a_wrong_password"/>.
    /// </remarks>
    private const string PortalUser = "portal_client_demo";
    private const string PortalPassword = "Demo#Portal1";

    private readonly PlaywrightFixture _playwright;

    public UserScreenTests(PlaywrightFixture playwright) => _playwright = playwright;

    // ---- AC-127-A · the list renders Arabic RTL at 390px with no sideways scroll ---------------

    [E2EFact]
    public async Task The_user_list_renders_right_to_left_and_does_not_scroll_sideways_at_phone_width()
    {
        // AC-127-A, and AC-106-J's nineteen-day-old "Arabic, RTL, at mobile width" arriving at last.
        // A horizontal scrollbar at 390px is the usual symptom of a physical CSS property that should
        // have been logical, and Arabic is the product language rather than a translation laid over
        // an English layout.
        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, OwnerUser, OwnerPassword);

        await page.GotoAsync("/users");
        await page.GetByTestId("user-list").WaitForAsync();

        (await page.Locator("html").GetAttributeAsync("dir")).Should().Be("rtl");

        await AssertNoSidewaysScrollAsync(page);

        // The roles resolve from the catalogue. A raw key on screen is an Arabic-speaking user reading
        // "enum.Role.Owner" — KAFF-120 rule 8, and AC-127-A's "resolve from the catalogue".
        string rows = (await page.GetByTestId("user-rows").TextContentAsync() ?? string.Empty).Trim();

        rows.Should().NotContain("enum.Role.", "the catalogue must resolve every role name");
        rows.Should().NotContain("users.", "and every label on the row");
        rows.Should().MatchRegex("\\p{IsArabic}", "the product language is Arabic, not a fallback");
    }

    // ---- AC-127-B · the create form renders Arabic RTL at 390px --------------------------------

    /// <summary>
    /// <c>AC-127-B</c> and <c>AC-127-I</c>'s bookmarked-deep-URL half, in one hard load.
    /// </summary>
    /// <remarks>
    /// <c>GotoAsync</c> is a full document load, not an in-app navigation, which is the whole point:
    /// D-113 §2's defect was invisible to router navigation and to any unit test of the guard, because
    /// what was wrong was <i>when</i> the guard ran rather than what it decided.
    /// </remarks>
    [E2EFact]
    public async Task A_bookmarked_user_form_url_loads_the_form_in_arabic_and_does_not_scroll_sideways()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, OwnerUser, OwnerPassword);

        await page.GotoAsync("/users/new");
        await page.GetByTestId("user-form").WaitForAsync();

        page.Url.Should().EndWith(
            "/users/new",
            "a bookmarked create form must open the form; bouncing to /users answers a question the "
            + "operator did not ask and says nothing about having done so");

        (await page.Locator("html").GetAttributeAsync("dir")).Should().Be("rtl");

        await page.GetByTestId("user-full-name").WaitForAsync();
        await page.GetByTestId("user-temporary-password").WaitForAsync();

        await AssertNoSidewaysScrollAsync(page);

        string labels = (await page.GetByTestId("user-form").TextContentAsync() ?? string.Empty).Trim();

        labels.Should().NotContain("users.field.", "every label resolves from the catalogue");
        labels.Should().NotContain("users.hint.", "and so does every hint");
        labels.Should().MatchRegex("\\p{IsArabic}");
    }

    // ---- AC-127-C · the HR pair is kept legal on the way in ------------------------------------

    /// <summary>
    /// Rule 6 and <c>AC-127-C</c>: the form does not offer the combination the server refuses.
    /// </summary>
    /// <remarks>
    /// S-007 is explicit that HR's department is <i>"a fixed, disabled value with a hint, not a select
    /// with one option"</i>. The assertion is therefore about the department <b>select being gone</b>,
    /// not about which option it holds — an HR user placed in Operations/Administrative would inherit
    /// <c>SiteExpenseConfirm</c> through a department-only grant, which is the piggyback D-044 exists
    /// to prevent arriving from the other direction.
    /// </remarks>
    [E2EFact]
    public async Task Choosing_the_hr_role_pins_the_department_and_removes_the_illegal_choices()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, OwnerUser, OwnerPassword);

        await page.GotoAsync("/users/new");
        await page.GetByTestId("user-form").WaitForAsync();

        // Before: a real department select, offering Operations among others.
        await page.GetByTestId("user-department").WaitForAsync();

        await page.GetByTestId("user-role").SelectOptionAsync("Hr");

        await page.GetByTestId("user-department-fixed").WaitForAsync();

        (await page.GetByTestId("user-department").CountAsync()).Should().Be(
            0,
            "the form must not offer the combination the server refuses — a select that still lists "
            + "Operations is an invitation to assemble errors.identity.hr_role_requires_hr_department");

        string fixedValue =
            (await page.GetByTestId("user-department-fixed").TextContentAsync() ?? string.Empty).Trim();

        fixedValue.Should().NotContain("enum.Department.", "the pinned department resolves from the catalogue");
        fixedValue.Should().NotContain("users.hint.", "and so does the hint that explains why it is pinned");
    }

    // ---- AC-127-G · a role without the permission reaches nothing -------------------------------

    [E2EFact]
    public async Task A_role_without_user_manage_is_refused_visibly_rather_than_sent_to_its_landing()
    {
        // AC-127-G. `ux/navigation.md`: a refusal "must not render as a crash, a blank page, or a
        // redirect that hides what happened" — and `parseUrl('/')` is the third of those, which is
        // the defect D-114 §3 recorded against this guard's sibling one day after it shipped.
        await EnsureFinanceCanSignInAsync();

        IPage page = await _playwright.NewMobilePageAsync();

        await SignInAsync(page, FinanceUser, FinancePassword);

        foreach (string route in new[] { "/users", "/users/new" })
        {
            await page.GotoAsync(route);
            await page.GetByTestId("forbidden-page").WaitForAsync();

            page.Url.Should().EndWith("/forbidden", "the address bar must say which refusal happened");

            string refusal = (await page.Locator("[data-testid='forbidden-page'] [role='alert']")
                .TextContentAsync() ?? string.Empty).Trim();

            // The key itself on screen would mean the catalogue never resolved it — an Arabic-speaking
            // user reading "errors.auth.forbidden" (KAFF-120 rule 8, AC-127-G's "in their language").
            refusal.Should().NotBe("errors.auth.forbidden");
            refusal.Should().MatchRegex("\\p{IsArabic}");

            // "with the app chrome intact" — the refusal is a page in the application, not a dead end.
            await page.GetByTestId("app-title").WaitForAsync();
        }
    }

    /// <summary>
    /// <c>V-33-E</c>, and <c>AC-127-G</c>'s second half — the portal <c>Role.Client</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The portal boundary of spec.md §12 had no UI-level evidence anywhere in this repository</b>
    /// because <c>scripts/seed-demo.ps1</c> created no <c>Role.Client</c> account at all. It does now,
    /// and this is what that account proves: a portal client presenting its <b>correct</b> credentials
    /// on the staff host is turned away, because <c>StaffSessionRules.MayHoldStaffSession</c> refuses
    /// the role before any route is reached. It never gets far enough to be refused by a guard.
    /// </para>
    /// <para>
    /// <b>And the refusal is indistinguishable from a wrong password</b>, which is D-065's ruling
    /// rather than an accident of this screen: a message that said "this account cannot sign in here"
    /// would confirm to an attacker that the username exists. Asserting the two texts are equal is
    /// what makes that property checkable — asserting only that "some refusal appeared" would pass
    /// against a screen that named the role.
    /// </para>
    /// </remarks>
    [E2EFact]
    public async Task A_portal_client_is_refused_the_staff_host_indistinguishably_from_a_wrong_password()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        string withCorrectPassword = await FailedSignInTextAsync(page, PortalUser, PortalPassword);

        page.Url.Should().Contain(
            "/sign-in",
            $"a Role.Client must not hold a staff session — is this stack seeded by the version of "
            + $"scripts/seed-demo.ps1 that creates {PortalUser}? (V-33-E)");

        withCorrectPassword.Should().MatchRegex("\\p{IsArabic}", "the refusal is read by a person");
        withCorrectPassword.Should().NotBe("errors.auth.invalid_credentials", "the catalogue must resolve it");
        withCorrectPassword.Should().NotContain(
            "Client",
            "naming the role would tell an attacker this username exists and what it is");

        string withWrongPassword = await FailedSignInTextAsync(page, PortalUser, PortalPassword + "x");

        withCorrectPassword.Should().Be(
            withWrongPassword,
            "D-065 — the correct credentials of an account that may not sign in here must read exactly "
            + "as a wrong password does, or the screen has confirmed the account exists");

        // No staff chrome, not one frame. `ux/navigation.md`: the shell "mounts no staff chrome — not
        // one frame, not empty" for a role that holds none.
        (await page.GetByTestId("side-nav").CountAsync()).Should().Be(0);

        // And the user-administration routes are unreachable, by the front door this time.
        await page.GotoAsync("/users");
        await page.WaitForURLAsync("**/sign-in");
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>
    /// The page body must never scroll horizontally at 390px.
    /// </summary>
    /// <remarks>
    /// A wide table scrolls inside its own container (`ux/components.md` §8); the <b>body</b> does
    /// not. This is the assertion `ux/rtl-and-i18n.md` §9's checklist ends with.
    /// </remarks>
    private static async Task AssertNoSidewaysScrollAsync(IPage page)
    {
        int scrollWidth = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        int clientWidth = await page.EvaluateAsync<int>("() => document.documentElement.clientWidth");

        scrollWidth.Should().BeLessThanOrEqualTo(
            clientWidth,
            "a horizontal scrollbar at 390px is the usual symptom of a physical CSS property that "
            + "should have been logical");
    }

    /// <summary>Attempts a sign-in expected to fail, and returns the refusal the screen rendered.</summary>
    private static async Task<string> FailedSignInTextAsync(IPage page, string userName, string password)
    {
        await page.GotoAsync("/sign-in");

        await page.Locator("input[autocomplete='username']").FillAsync(userName);
        await page.Locator("input[autocomplete='current-password']").FillAsync(password);
        await page.Locator("button[type='submit']").ClickAsync();

        ILocator refusal = page.Locator("form [role='alert']");
        await refusal.WaitForAsync();

        return (await refusal.TextContentAsync() ?? string.Empty).Trim();
    }

    /// <summary>Signs in through the screen, and waits until the router has left it.</summary>
    /// <remarks>
    /// Located by <c>autocomplete</c> rather than by a test id, because the sign-in screen carries
    /// none and adding one to a shipped screen to satisfy a test is a change to the product for the
    /// benefit of the suite. These attributes are load-bearing for password managers.
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

    /// <summary>Clears Finance's forced password change, once, through the API.</summary>
    /// <remarks>
    /// <para>
    /// Idempotent by trying the changed password first, so the suite survives its own second run and
    /// <c>ClientScreenTests</c> having gone first — both suites share one seeded Finance account.
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

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
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
