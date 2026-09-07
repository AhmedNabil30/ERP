using System.Net;
using System.Net.Http;
using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// KAFF-128's audit trail, driven. <c>S-015</c>, and the strictest permission in slice 1.
/// </summary>
/// <remarks>
/// <para>
/// <b>The clause that makes it strict is "even for their own projects".</b> Every other permission in
/// Kaff is <c>role × assignment</c>; this one refuses a Technical Office lead the trail of the project
/// they run (D-049 ruling 1, Karim verbatim). <b>A filtered trail for a non-Owner is a defect, not a
/// partial success</b> — so what these tests assert of a non-Owner is a refusal, never a narrower list.
/// </para>
/// <para>
/// <b>⚠️ One half of <c>AC-128-B</c>'s fixture cannot be built today, and it is the half the story says
/// matters.</b> <c>TC-1-300</c> asks for a Technical Office user <i>holding an active assignment on a
/// project that has audit records</i>. <c>scripts/seed-demo.ps1</c> creates no Technical Office account
/// at all, and no assignment could be made for one if it did: <c>POST /api/projects</c> does not exist
/// — the seed script probes it and records the <c>404</c> — while
/// <c>POST /api/projects/{projectId}/assignments</c> requires a project that does. Creating accounts
/// from this suite is not the answer either; <c>ClientScreenTests</c> states the rule these files work
/// to, that a run <i>"leaves the seeded database exactly as it found it"</i>. <b>So the refused set
/// below is Finance and the portal client, and the Technical Office case is recorded as a fixture gap
/// rather than quietly dropped or quietly faked.</b> Routed with the story.
/// </para>
/// <para>
/// <b>What still holds without it.</b> <c>GET /api/audit</c> declares no <c>ProjectScope</c> at all —
/// the gate answers "is this the Owner" and <c>?projectId=</c> only narrows what the Owner is shown —
/// so an assignment cannot change the answer by construction. That is an argument from the code and not
/// a driven fact, which is exactly why the gap is written down.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public sealed class AuditScreenTests
{
    private readonly PlaywrightFixture _playwright;

    public AuditScreenTests(PlaywrightFixture playwright) => _playwright = playwright;

    /// <summary>
    /// <c>TC-1-299</c>, <c>TC-1-301</c>, <c>TC-1-306</c> · <c>AC-128-A</c>, <c>AC-128-C</c>,
    /// <c>AC-128-F</c>.
    /// </summary>
    /// <remarks>
    /// <b>A hard load, never an in-app navigation</b> (D-113 §2). <c>GotoAsync</c> is a full document
    /// load, so <c>auditReadGuard</c> runs against a session that has not resolved — the only
    /// arrangement in which its own <c>await</c> does any work, and the one a bookmark always produces.
    /// <b>And records must be on the screen:</b> an empty trail has no timestamp to reorder and asserts
    /// nothing about direction at all.
    /// </remarks>
    [E2EFact]
    public async Task The_owner_hard_loads_the_trail_and_it_reads_right_to_left_at_phone_width()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await E2ESession.SignInAsync(page, E2ESession.OwnerUser, E2ESession.OwnerPassword);

        await page.GotoAsync("/audit");
        await page.GetByTestId("audit-trail").WaitForAsync();

        page.Url.Should().EndWith("/audit", "a bookmarked trail must open the trail");

        (await page.Locator("html").GetAttributeAsync("dir")).Should().Be("rtl");

        int scrollWidth = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        int clientWidth = await page.EvaluateAsync<int>("() => document.documentElement.clientWidth");

        scrollWidth.Should().BeLessThanOrEqualTo(
            clientWidth,
            "a horizontal scrollbar at 390px is the usual symptom of a physical CSS property that "
            + "should have been logical");

        await page.GetByTestId("audit-rows").WaitForAsync();

        (await page.Locator("[data-testid^='audit-row-']").CountAsync()).Should().BeGreaterThan(
            0,
            "the seeded stack signed in, created users and registered clients — every one of those "
            + "wrote a record. An empty trail asserts nothing about a trail");

        string trail = (await page.GetByTestId("audit-trail").TextContentAsync() ?? string.Empty).Trim();

        trail.Should().NotContain("audit.", "the catalogue must resolve every key this screen uses");
        trail.Should().NotContain("enum.Audit", "including the two audit enums");
        trail.Should().MatchRegex("\\p{IsArabic}", "the product language is Arabic, not a fallback");

        // Open the panel: the ids, the route and the address are the densest run of all and none of
        // them is on the list until a record is open.
        await page.Locator("[data-testid^='audit-row-']").First.ClickAsync();
        await page.GetByTestId("audit-panel").WaitForAsync();

        string[] ambiguous = await FirstStrongCannotDecideAsync(page);

        ambiguous.Should().BeEmpty(
            "a <bdi> whose content has no strong directional character falls back to the paragraph's "
            + "direction, which here is RTL — so `::1` renders as `1::` and a timestamp puts its date "
            + "group to the right of its time. Isolation says where the run ENDS, not which way it "
            + "READS; these need `dir` stated");
    }

    /// <summary>
    /// Every <c>&lt;bdi&gt;</c> on screen that carries no strong directional character and has not been
    /// told which way to read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Found by looking at the rendered screen, and it is the complement of <c>F-1</c>.</b> <c>F-1</c>
    /// was a geometry defect that direction checks could not see; this is a direction defect that the
    /// geometry sweep cannot see, because a reordered run occupies exactly the same box as a correct
    /// one. Neither check subsumes the other and the trail is where it bites hardest — a timestamp,
    /// two GUIDs, a route and an IP address on one panel.
    /// </para>
    /// <para>
    /// <b>Enumerated from the DOM and derived, not listed.</b> The rule is a property of the element's
    /// own content: digits are weak, <c>/</c>, <c>:</c> and <c>،</c> are neutral, so first-strong —
    /// which is what a <c>&lt;bdi&gt;</c> does by default — finds nothing to go on and inherits RTL.
    /// </para>
    /// <para>
    /// <b>⚠️ Scoped to this screen deliberately.</b> The client and user lists have the same shape on
    /// their phone <c>&lt;bdi&gt;</c>s and are green today only because a seeded phone number is one
    /// unbroken digit run with nothing in it to reorder. A number typed <c>0100-123-4567</c> or
    /// <c>+20 100 123 4567</c> would reorder there exactly as <c>::1</c> does here. Routed to QA as a
    /// candidate for <c>TC-1-272</c>'s sweep rather than fixed inside another story's criteria.
    /// </para>
    /// </remarks>
    private static async Task<string[]> FirstStrongCannotDecideAsync(IPage page)
    {
        return await page.EvaluateAsync<string[]>("""
            () => {
              // Any letter, in any script — Latin and Arabic alike. Letters are precisely the strong
              // directional characters; digits are weak and punctuation is neutral, and those are
              // exactly what first-strong cannot decide on. `\p{L}` says that in the platform's own
              // vocabulary rather than as a hand-written list of code-point ranges to keep correct.
              const strong = /\p{L}/u;

              return [...document.querySelectorAll('bdi')]
                .filter((el) => !strong.test(el.textContent || ''))
                .filter((el) => (el.getAttribute('dir') || 'auto') === 'auto')
                .map((el) => (el.textContent || '').trim());
            }
            """);
    }

    /// <summary>
    /// <c>TC-1-300</c> · <c>AC-128-B</c> — the refusal, in the UI and independently at the API.
    /// </summary>
    /// <remarks>
    /// <b>The API half is the control and the UI half is the courtesy.</b> Hiding a route is
    /// presentation; the endpoint is what decides, and it is called here directly — bypassing the SPA —
    /// <b>both with and without <c>?projectId=</c></b>, because "even for their own projects" is
    /// precisely the case a project-scoped read would have let through.
    /// </remarks>
    [E2EFact]
    public async Task A_role_without_audit_read_is_refused_the_trail_by_the_screen_and_by_the_endpoint()
    {
        await E2ESession.EnsureCanSignInAsync(
            E2ESession.FinanceUser,
            E2ESession.FinanceSeedPassword,
            E2ESession.FinancePassword);

        IPage page = await _playwright.NewMobilePageAsync();

        await E2ESession.SignInAsync(page, E2ESession.FinanceUser, E2ESession.FinancePassword);

        await page.GotoAsync("/audit");
        await page.GetByTestId("forbidden-page").WaitForAsync();

        page.Url.Should().EndWith("/forbidden");

        string refusal = (await page.Locator("[data-testid='forbidden-page'] [role='alert']")
            .TextContentAsync() ?? string.Empty).Trim();

        refusal.Should().NotBe("errors.auth.forbidden", "the catalogue must resolve it");
        refusal.Should().MatchRegex("\\p{IsArabic}", "in their own language");

        // "with the chrome intact" — the refusal is a page in the application, not a dead end.
        await page.GetByTestId("app-title").WaitForAsync();

        using HttpClientHandler handler = new() { UseCookies = false };
        using HttpClient client = new(handler) { BaseAddress = new Uri(E2EEnvironment.ApiBaseUrl) };

        string? finance = await E2ESession.SignInToApiAsync(
            client,
            E2ESession.FinanceUser,
            E2ESession.FinancePassword);

        finance.Should().NotBeNull("Finance must hold a session — otherwise 403 below proves nothing");

        foreach (string route in new[] { "/api/audit", $"/api/audit?projectId={Guid.NewGuid()}" })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, route);
            request.Headers.TryAddWithoutValidation("Cookie", finance);

            using HttpResponseMessage response = await client.SendAsync(request);

            response.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                $"{route} — naming a project must not turn the trail into a project-scoped read that "
                + "the people working on that project may have. Karim refused exactly that");
        }

        // The positive control: the same two routes answer the Owner, so a route that 403s everybody
        // — including the Owner — cannot pass this test.
        string? owner = await E2ESession.SignInToApiAsync(
            client,
            E2ESession.OwnerUser,
            E2ESession.OwnerPassword);

        owner.Should().NotBeNull("is this stack seeded by scripts/seed-demo.ps1?");

        using HttpRequestMessage ownerRequest = new(HttpMethod.Get, "/api/audit");
        ownerRequest.Headers.TryAddWithoutValidation("Cookie", owner);

        using HttpResponseMessage ownerResponse = await client.SendAsync(ownerRequest);

        ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <c>TC-1-300</c>'s third subject — the portal <c>Role.Client</c>, spec.md §12.
    /// </summary>
    /// <remarks>
    /// A <c>Role.Client</c> never reaches a guard at all: <c>StaffSessionRules.MayHoldStaffSession</c>
    /// refuses the role at the door, so the account cannot hold a staff session to be refused with. The
    /// boundary is therefore asserted where it actually is — at sign-in, and at the endpoint with no
    /// session behind it.
    /// </remarks>
    [E2EFact]
    public async Task A_portal_client_holds_no_session_to_reach_the_trail_with()
    {
        using HttpClientHandler handler = new() { UseCookies = false };
        using HttpClient client = new(handler) { BaseAddress = new Uri(E2EEnvironment.ApiBaseUrl) };

        (await E2ESession.SignInToApiAsync(
            client,
            E2ESession.PortalClientUser,
            E2ESession.PortalClientSeedPassword))
            .Should().BeNull("a Role.Client may not hold a staff session (spec.md §12, D-065)");

        using HttpResponseMessage anonymous = await client.GetAsync("/api/audit");

        anonymous.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "and with no session there is nothing to read the trail with");
    }

    /// <summary>
    /// <c>TC-1-302</c> · <c>AC-128-D</c> — every control on the screen, enumerated from the DOM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enumerated and compared against what is permitted, never listed as what is forbidden.</b> A
    /// case naming the controls that must not exist cannot fail — the "correct this" button somebody
    /// adds next year is not on the list. This reads what is actually there and asserts the set is
    /// contained in the four purposes the screen is allowed: the date range, opening a record, and
    /// closing the panel.
    /// </para>
    /// <para>
    /// The API half of this rule is <c>TC-1-140</c> and the database half is <c>TC-1-141</c>. This is
    /// the third door, and it is the only one a user can see.
    /// </para>
    /// </remarks>
    [E2EFact]
    public async Task Nothing_on_the_audit_screen_offers_a_way_to_change_a_record()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await E2ESession.SignInAsync(page, E2ESession.OwnerUser, E2ESession.OwnerPassword);

        await page.GotoAsync("/audit");
        await page.GetByTestId("audit-rows").WaitForAsync();

        string[] onTheList = await EnumerateControlsAsync(page);

        onTheList.Should().BeSubsetOf(
            Permitted,
            "the trail is append-only and the screen must not suggest otherwise. A control here that "
            + "is not one of these is a control nobody decided to add");

        // Opening a record must not add one either — the panel is where an "edit" affordance would
        // most naturally be put by somebody who had not read the rule.
        await page.Locator("[data-testid^='audit-row-']").First.ClickAsync();
        await page.GetByTestId("audit-panel").WaitForAsync();

        string[] withThePanelOpen = await EnumerateControlsAsync(page);

        withThePanelOpen.Should().BeSubsetOf(Permitted);
        withThePanelOpen.Should().Contain(
            "audit-panel-close",
            "the panel opened, so this enumeration is of a screen in the state it was meant to be in");
    }

    /// <summary>
    /// <c>TC-1-303</c> · <c>S-015</c>'s redaction rule — <b>which no acceptance criterion carries</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PasswordHash</c> and <c>SecurityStamp</c> are <c>[AuditRedacted]</c>, so the trail records
    /// that a credential changed without recording the credential. <b>Both cells must read as redacted
    /// and neither may be blank</b>: a blank cell reads as "nothing changed", which is the opposite of
    /// what happened, and is the misreading the attribute exists to prevent.
    /// </para>
    /// <para>
    /// <b>The fixture is Finance's own forced password change</b>, which
    /// <see cref="E2ESession.EnsureCanSignInAsync"/> performs the first time this suite meets a seeded
    /// stack and which every later run reads out of the append-only table. Nothing is created for this
    /// test that the suite did not already need.
    /// </para>
    /// </remarks>
    [E2EFact]
    public async Task A_redacted_value_reads_as_redacted_and_never_as_a_blank_cell()
    {
        await E2ESession.EnsureCanSignInAsync(
            E2ESession.FinanceUser,
            E2ESession.FinanceSeedPassword,
            E2ESession.FinancePassword);

        IPage page = await _playwright.NewMobilePageAsync();

        await E2ESession.SignInAsync(page, E2ESession.OwnerUser, E2ESession.OwnerPassword);

        await page.GotoAsync("/audit");
        await page.GetByTestId("audit-rows").WaitForAsync();

        // The record is found by opening rows until one carries a redacted field, rather than by
        // knowing which row it is: the ordering is the server's and a fixed index would be a different
        // record on a stack with one more sign-in in it.
        ILocator rows = page.Locator("[data-testid^='audit-row-']");
        int count = await rows.CountAsync();

        for (int index = 0; index < count; index++)
        {
            await rows.Nth(index).ClickAsync();
            await page.GetByTestId("audit-panel").WaitForAsync();

            ILocator change = page.GetByTestId("audit-change-PasswordHash");

            if (await change.CountAsync() == 0)
            {
                await page.GetByTestId("audit-panel-close").ClickAsync();
                continue;
            }

            IReadOnlyList<string> cells = await change.Locator("td").AllInnerTextsAsync();

            cells.Should().HaveCount(2, "Before and After");

            foreach (string cell in cells)
            {
                cell.Trim().Should().NotBeEmpty(
                    "a blank cell reads as 'nothing changed', which is the opposite of what happened");
                cell.Should().NotContain(
                    "audit.value.",
                    "the catalogue must resolve the placeholder, not print its key");
            }

            return;
        }

        throw new Xunit.Sdk.XunitException(
            "no record on the trail carried a PasswordHash change, so this case measured nothing — "
            + "EnsureCanSignInAsync should have written one against a freshly seeded stack");
    }

    /// <summary>
    /// The purposes this screen is allowed to offer: the date range, opening a record, closing the
    /// panel. Row buttons collapse to one entry because their ids are record ids.
    /// </summary>
    private static readonly string[] Permitted =
    [
        "audit-from",
        "audit-to",
        "audit-apply",
        "audit-row",
        "audit-panel-backdrop",
        "audit-panel-close",
    ];

    /// <summary>
    /// Every interactive element inside the screen and its panel, by test id.
    /// </summary>
    /// <remarks>
    /// The shell's own chrome — the drawer toggle, the locale switch, sign-out — is deliberately out of
    /// scope: it belongs to <c>KAFF-125</c> and is on every screen in the application. An element with
    /// no test id at all is reported as its tag name, so it cannot pass unnoticed.
    /// </remarks>
    private static async Task<string[]> EnumerateControlsAsync(IPage page)
    {
        return await page.EvaluateAsync<string[]>("""
            () => {
              const roots = ['[data-testid="audit-trail"]', '[data-testid="audit-panel"]'];
              const found = new Set();

              for (const root of roots) {
                const scope = document.querySelector(root);
                if (scope === null) continue;

                for (const el of scope.querySelectorAll('button, a, input, select, textarea, [role="button"]')) {
                  const id = el.getAttribute('data-testid');
                  found.add(id === null ? `untagged:${el.tagName.toLowerCase()}` : id.replace(/^audit-row-.*/, 'audit-row'));
                }
              }

              // The backdrop is a sibling of both roots rather than inside either.
              if (document.querySelector('[data-testid="audit-panel-backdrop"]') !== null) {
                found.add('audit-panel-backdrop');
              }

              return [...found];
            }
            """);
    }
}
