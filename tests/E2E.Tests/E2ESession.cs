using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>
    /// Asks the Owner's own <c>GET /api/users</c> whether the portal account exists and is active.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The positive control for every assertion that reads a refusal of <see cref="PortalClientUser"/>.</b>
    /// D-065 makes a username nobody holds indistinguishable from an account that may not sign in here —
    /// deliberately — so a test that only observes the refusal is equally satisfied by an empty database.
    /// That is <c>V-33-E</c>'s shape and <c>V-35-K</c> §15.3 found it recurring in
    /// <c>AuditScreenTests</c>. This is the assertion that fails when the account is missing, and it
    /// fails naming the seed script.
    /// </para>
    /// <para>
    /// <b>Moved here from <c>UserScreenTests</c> 2026-09-08 rather than copied</b> — CLAUDE.md's rule
    /// that a thing two features need moves rather than being duplicated. A second copy of an absence
    /// control is a second copy to keep correct, and this one has already been wrong once.
    /// </para>
    /// </remarks>
    public static async Task AssertPortalAccountExistsAsync()
    {
        using HttpClientHandler handler = new() { UseCookies = false };
        using HttpClient client = new(handler) { BaseAddress = new Uri(E2EEnvironment.ApiBaseUrl) };

        string? cookie = await SignInToApiAsync(client, OwnerUser, OwnerPassword);

        cookie.Should().NotBeNull(
            "the Owner must be able to sign in — is this stack seeded by scripts/seed-demo.ps1?");

        using HttpRequestMessage list = new(HttpMethod.Get, "/api/users");
        list.Headers.TryAddWithoutValidation("Cookie", cookie);

        using HttpResponseMessage response = await client.SendAsync(list);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        // ⚠️ Parsed and matched EXACTLY, never `body.Should().Contain(PortalClientUser)`.
        // That is what this assertion said first, and renaming the account to
        // `portal_client_demo_MUTATED` left it green — the substring was still there. An absence
        // control defeated by the mutation it exists to catch is D-116 §3 arriving from inside the
        // control itself.
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        JsonElement portal = document.RootElement.GetProperty("users").EnumerateArray()
            .SingleOrDefault(user =>
                string.Equals(user.GetProperty("userName").GetString(), PortalClientUser, StringComparison.Ordinal));

        portal.ValueKind.Should().Be(
            JsonValueKind.Object,
            $"a refusal proves nothing unless {PortalClientUser} actually exists — a username nobody "
            + "holds is turned away with exactly the same message (D-065), which is why V-33-E called "
            + "the portal half UNDRIVABLE rather than merely untested. Reseed with the version of "
            + "scripts/seed-demo.ps1 that creates it.");

        portal.GetProperty("role").GetString().Should().Be(
            "Client",
            $"{PortalClientUser} must still hold Role.Client — a portal account promoted to a staff role "
            + "would be refused for a different reason entirely, and this test would say nothing true");

        portal.GetProperty("isActive").GetBoolean().Should().BeTrue(
            "a deactivated account is refused for a reason that has nothing to do with spec.md §12");
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
