using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// The seeded accounts, and getting a page signed in as one of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Extracted rather than copied a third time.</b> <c>ClientScreenTests</c> and
/// <c>UserScreenTests</c> each carry their own private copy of this, written before there was a second
/// file that needed it. Those two are left alone — they are verified files and churning them buys
/// nothing — but the copy stops here: the two suites added on 2026-09-07 use this one.
/// </para>
/// <para>
/// <b>These accounts come from <c>scripts/seed-demo.ps1</c></b>, whose users and passwords are listed
/// in <c>deploy/DEMO.md</c> §4. Every helper here fails rather than skips when an account is missing:
/// a suite that quietly passes against an unseeded database reports a safety it does not have.
/// </para>
/// </remarks>
internal static class E2ESession
{
    /// <summary>The bootstrap Owner. The one seeded account that is not forced to change its password.</summary>
    public const string OwnerUser = "owner_demo";
    public const string OwnerPassword = "Demo#Owner1";

    /// <summary>Finance — a real role holding neither <c>ClientManage</c> nor <c>AuditRead</c>.</summary>
    public const string FinanceUser = "sara_finance_demo";
    public const string FinanceSeedPassword = "Demo#Fin123";

    /// <summary>
    /// What the suites move Finance to, once. Signing in with the seed password lands on
    /// <c>/change-password</c> — <c>mustChangePasswordGuard</c> runs before every feature guard —
    /// which would make a refusal criterion unobservable.
    /// </summary>
    /// <remarks>
    /// The same value <c>ClientScreenTests</c> and <c>UserScreenTests</c> use, deliberately: three
    /// suites moving one account to three different passwords would leave whichever ran last holding
    /// the only working credential.
    /// </remarks>
    public const string FinancePassword = "Demo#Fin456";

    /// <summary>The client-portal account, scoped to the seeded corporate client.</summary>
    public const string PortalClientUser = "portal_client_demo";
    public const string PortalClientSeedPassword = "Demo#Portal1";
    public const string PortalClientPassword = "Demo#Portal2";

    /// <summary>Signs in through the screen, and waits until the router has left it.</summary>
    /// <remarks>
    /// Located by <c>autocomplete</c> rather than by a test id, because the sign-in screen carries
    /// none and adding one to a shipped screen for the benefit of a test is a change to the product.
    /// These attributes are load-bearing for password managers, so they are not going anywhere quietly.
    /// </remarks>
    public static async Task SignInAsync(IPage page, string userName, string password)
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
    /// Clears a seeded account's forced password change, once, through the API.
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
    public static async Task EnsureCanSignInAsync(string userName, string seedPassword, string password)
    {
        using HttpClientHandler handler = new() { UseCookies = false };
        using HttpClient client = new(handler) { BaseAddress = new Uri(E2EEnvironment.ApiBaseUrl) };

        if (await SignInToApiAsync(client, userName, password) is not null)
        {
            return;
        }

        string? cookie = await SignInToApiAsync(client, userName, seedPassword);

        cookie.Should().NotBeNull(
            $"{userName} answered neither password — is this stack seeded by scripts/seed-demo.ps1?");

        using HttpRequestMessage change = new(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = seedPassword,
                newPassword = password,
            }),
        };
        change.Headers.TryAddWithoutValidation("Cookie", cookie);

        using HttpResponseMessage response = await client.SendAsync(change);

        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());
    }

    /// <summary>The session cookie when the password is the right one, null when it is not.</summary>
    public static async Task<string?> SignInToApiAsync(HttpClient client, string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { userName, password });

        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            return null;
        }

        return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.First().Split(';')[0]
            : null;
    }
}
