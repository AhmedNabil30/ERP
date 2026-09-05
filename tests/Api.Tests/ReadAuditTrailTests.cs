using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Auditing;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/audit</c> — the Owner reads the audit trail, and nobody else does. KAFF-117.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission is the story.</b> spec.md §9 is otherwise <c>role × assignment</c>; D-049 ruling 1
/// makes this one refuse a Technical Office lead reading the trail of a project they run —
/// <i>"completely hidden from all other roles, even for their own projects"</i>. A filtered trail for a
/// non-Owner would be a defect, not a partial success, which is why
/// <see cref="An_assigned_technical_office_user_is_refused_the_trail_of_their_own_project"/> exists as
/// its own criterion rather than as a row in the loop.
/// </para>
/// <para>
/// <b>⚠️ This is a read, so there is no audit backstop</b> (decisions.md D-110 §2). Deleting
/// <c>.RequirePermission</c> from a <i>write</i> reddens most of its suite for a second, unrelated
/// reason — nothing calls <c>ActorVerifiedAs</c> and the actor constraint refuses the row. Nothing of
/// the sort happens here: an ungated <c>GET /api/audit</c> returns every state change Kaff has
/// recorded, cheerfully, to anybody. <b>The permission tests below are very nearly the entire
/// control</b>, and their refused set is derived from the <c>Role</c> enum rather than hand-listed,
/// because a hand-written list is exactly what went stale in <c>V-33-A</c> (D-118).
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ReadAuditTrailTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _finance;
    private Guid _hr;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _headOfDesign;
    private Guid _marketing;
    private Guid _portalClient;
    private Guid _portalClientCompany;
    private Guid _subcontractor;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _staffedOnA;
    private Guid _staffedOnB;
    private Guid _leaver;
    private Guid _credentialHolder;

    /// <summary>The record the Technical Office user wrote on their own project, seeded in <see cref="SeedAsync"/>.</summary>
    private Guid _technicalOfficeAssignment;

    public ReadAuditTrailTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();

        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- AC-117-A · the Owner reads it, company-wide -------------------------------------------

    /// <summary>
    /// <c>AC-117-A</c>. With no project filter the Owner is shown project changes from more than one
    /// project <b>and</b> the company-level changes that belong to no project at all.
    /// </summary>
    /// <remarks>
    /// The second half is the half that would be quietly lost. Half of what the Owner checks — a user
    /// created, a client edited, an account deactivated — carries no <c>ProjectId</c>, so a reading
    /// built around a project join would answer plausibly and be missing the company master data
    /// entirely. Asserted as containment rather than as a count: the suite shares one database across
    /// classes, so the trail legitimately holds other tests' work too.
    /// </remarks>
    [Fact]
    public async Task The_owner_reads_every_project_and_every_company_level_change()
    {
        await AssignAsync(_projectA, _staffedOnA);
        await AssignAsync(_projectB, _staffedOnB);
        await DeactivateAsync(_leaver, "ترك العمل بنهاية الشهر");

        JsonElement[] records = await ReadAsOwnerAsync();

        ProjectIdsOf(records).Should().Contain(
            [_projectA, _projectB],
            "'no project filter' means every project, not the first one — AC-117-A");

        records.Where(record => record.GetProperty("projectId").ValueKind == JsonValueKind.Null)
            .Select(record => record.GetProperty("entityType").GetString())
            .Should().Contain(
                nameof(User),
                "a deactivation belongs to no project, and a trail that only shows project rows is "
                + "missing every user and every client Kaff has");
    }

    // ---- AC-117-B · not even on their own project ----------------------------------------------

    /// <summary>
    /// <c>AC-117-B</c>, and it is the criterion that proves the ruling rather than restating it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Technical Office user holds an active assignment on project A and authored a record on it.
    /// Every other permission in this system would admit them. This one does not, on the project, on
    /// their own actions, with any filter.
    /// </para>
    /// <para>
    /// <b>The positive control is the last assertion, not decoration.</b> A 403 against a trail that
    /// happened to be empty proves that nothing was disclosed and nothing about whether there was
    /// anything to disclose (decisions.md D-116 §3). So the same record the Technical Office user is
    /// refused is then read back by the Owner, by id: the thing being withheld exists, and it is
    /// theirs.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_assigned_technical_office_user_is_refused_the_trail_of_their_own_project()
    {
        string[] queries =
        [
            string.Empty,
            $"?projectId={_projectA}",
            $"?actorUserId={_technicalOffice}",
            $"?projectId={_projectA}&actorUserId={_technicalOffice}",
        ];

        foreach (string query in queries)
        {
            HttpResponseMessage refused = await GetAsync(
                query, _technicalOffice, Role.TechnicalOffice, Department.Operations);

            refused.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "D-049 ruling 1: the trail is 'completely hidden from all other roles, EVEN FOR THEIR "
                + $"OWN PROJECTS'. Query '{query}' asked for a project this user is actively assigned "
                + "to, and for their own changes on it");

            (await refused.Content.ReadAsStringAsync(Ct)).Should().NotContain(
                nameof(ProjectAssignment),
                "a refusal must not leak the trail in its body either");
        }

        // The positive control. Without it, every assertion above is satisfied by an endpoint that
        // has nothing to give anybody.
        JsonElement[] records = await ReadAsOwnerAsync($"?projectId={_projectA}");

        records.Select(record => record.GetProperty("id").GetGuid()).Should().Contain(
            _technicalOfficeAssignment,
            "the record the Technical Office user was refused is real, is on the project they run, "
            + "and the Owner reads it — which is the governance point D-049 ruling 1 was put to Karim "
            + "in, and he accepted");
    }

    // ---- AC-117-C · no role but the Owner reaches it -------------------------------------------

    /// <summary>
    /// <c>AC-117-C</c>. Seven roles, each with and without a project id: fourteen refusals.
    /// </summary>
    /// <remarks>
    /// <b>Every refused role, derived from the enum rather than listed.</b> A literal list stays at
    /// seven when a tenth role is added — <c>V-33-A</c> is what that costs: <c>Role.HeadOfDesign</c>
    /// was asserted against no endpoint anywhere, and granting it <c>ClientManage</c> left the whole
    /// suite green (D-118). <see cref="The_refused_list_is_every_role_that_can_sign_in_and_is_not_granted"/>
    /// is what keeps the loop below exhaustive.
    /// </remarks>
    [Fact]
    public async Task Nobody_but_the_owner_reaches_the_trail_with_or_without_a_project_id()
    {
        int refusals = 0;

        foreach ((Guid actorId, Role role, Department? department, Guid? clientId) in RefusedActors())
        {
            foreach (string query in new[] { string.Empty, $"?projectId={_projectA}" })
            {
                HttpResponseMessage refused = await GetAsync(query, actorId, role, department, clientId);

                refused.StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    $"{role} holds no AuditRead. From slice 3 this payload carries every movement of "
                    + "money in Kaff, and on a read there is no audit constraint failing behind the "
                    + "gate to catch its absence (D-110 §2)");

                (await refused.Content.ReadAsStringAsync(Ct)).Should().NotContain(
                    "\"records\"",
                    $"a refusal to {role} must not carry a trail in its body");

                refusals++;
            }
        }

        refusals.Should().Be(14, "seven signing-in roles, each asked twice — AC-117-C");
    }

    /// <summary>
    /// The list above is every signing-in role except the Owner.
    /// </summary>
    /// <remarks>
    /// Asserted rather than trusted: the list is hand-written and the enum is not, so a tenth role
    /// fails here instead of being silently uncovered. <c>Role.Subcontractor</c> is excluded because
    /// spec.md §9 says <i>"record only, no login"</i> — it cannot hold a session to be refused with,
    /// which is <see cref="A_subcontractor_cannot_hold_a_session_to_try_with"/> and its own
    /// catalogue-wide pin, <c>No_permission_is_granted_to_a_subcontractor</c>.
    /// </remarks>
    [Fact]
    public void The_refused_list_is_every_role_that_can_sign_in_and_is_not_granted()
    {
        IEnumerable<Role> covered = RefusedActors().Select(actor => actor.Role);

        IEnumerable<Role> shouldBeRefused = Enum.GetValues<Role>()
            .Except([Role.Owner, Role.Subcontractor]);

        covered.Should().BeEquivalentTo(
            shouldBeRefused,
            "AuditRead is granted to Role.Owner ALONE (D-049 ruling 1) — no second holder, and no "
            + "global Finance/Audit role, which that ruling anticipates and does not create. Every "
            + "other role that can sign in must appear in the loop");
    }

    /// <summary>Every role that must be refused, derived rather than listed. See the remarks above.</summary>
    private IEnumerable<(Guid ActorId, Role Role, Department? Department, Guid? ClientId)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance, null);
        yield return (_hr, Role.Hr, Department.Hr, null);
        yield return (_technicalOffice, Role.TechnicalOffice, Department.Operations, null);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations, null);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations, null);
        yield return (_marketing, Role.MarketingSales, Department.Marketing, null);
        yield return (_portalClient, Role.Client, null, _portalClientCompany);
    }

    // ---- AC-117-D · a subcontractor has no login to try with -----------------------------------

    /// <summary>
    /// <c>AC-117-D</c>, the half that belongs to this endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The door itself is already asserted by <c>SignInTests</c> -&gt;
    /// <c>Five_different_refusals_are_one_answer</c>, which posts a subcontractor's <b>correct</b>
    /// password and gets the same 401 as an unknown username. Re-asserting it here would be two
    /// statements of one rule and the copy is the one nobody updates (D-116 §1).
    /// </para>
    /// <para>
    /// <b>What is new here is the second door.</b> This test hands the endpoint a session stamped
    /// <c>Role.Subcontractor</c> — a token the sign-in path will never mint — and the gate refuses it
    /// anyway, because <c>PermissionEvaluator</c> answers <c>RoleCannotLogIn</c> before it looks at
    /// any grant. The refusal is an indistinguishable 403 (D-071, D-080), so this asserts the status
    /// and not a distinguishing key.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_subcontractor_cannot_hold_a_session_to_try_with()
    {
        HttpResponseMessage refused = await GetAsync(
            string.Empty, _subcontractor, Role.Subcontractor, null);

        refused.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "spec.md §9: 'record only, no login'. A forged session for the role is refused at the "
            + "gate, so the role never reaches the question of what it holds");
    }

    // ---- AC-117-E · redacted fields stay redacted ----------------------------------------------

    /// <summary>
    /// <c>AC-117-E</c>. A password set and later changed leaves no hash and no stamp in any reading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asserted against the whole response body, not against a named field.</b> The snapshots are
    /// the entity's own, so a secret would arrive inside <c>before</c> or <c>after</c> under whatever
    /// property name it happened to have — searching for a field would be searching for the shape of a
    /// mistake somebody already thought of (D-106). The two real secret values are searched for
    /// instead, in both directions.
    /// </para>
    /// <para>
    /// <b>The positive control matters more here than usual.</b> "The response does not contain the
    /// hash" is satisfied completely by a response that contains nothing — a filter that dropped the
    /// record, a query that matched no row, an endpoint that 200s with an empty array. So the same
    /// assertion also requires the record to be present, to name <c>PasswordHash</c> among its
    /// changed properties, and to carry the redaction placeholder where the secret used to be.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task No_reading_of_a_credential_change_carries_the_hash_or_the_stamp()
    {
        const string FirstHash = "hash-set-when-the-account-was-created";
        const string SecondHash = "hash-that-must-never-reach-the-audit-trail";

        string firstStamp = await SetPasswordAsync(_credentialHolder, FirstHash);
        string secondStamp = await SetPasswordAsync(_credentialHolder, SecondHash);

        firstStamp.Should().NotBe(secondStamp, "a password change rotates the security stamp (D-049 ruling 2)");

        HttpResponseMessage response = await GetAsync(
            $"?actorUserId={_credentialHolder}", _owner, Role.Owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync(Ct);

        foreach (string secret in new[] { FirstHash, SecondHash, firstStamp, secondStamp })
        {
            body.Should().NotContain(
                secret,
                "neither the password hash nor the security stamp may surface in any field of any "
                + "reading, before or after — both are [AuditRedacted] and the placeholder is what "
                + "the interceptor stored");
        }

        // The positive control. Everything above is satisfied by a body with nothing in it.
        using JsonDocument parsed = JsonDocument.Parse(body);

        JsonElement[] credentialChanges = [.. parsed.RootElement.GetProperty("records")
            .EnumerateArray()
            .Where(record => record.GetProperty("changedProperties")
                .EnumerateArray()
                .Any(property => property.GetString() == nameof(User.PasswordHash)))];

        credentialChanges.Should().NotBeEmpty(
            "the credential change must be IN the trail — a reading that lost the record would pass "
            + "every assertion above while proving nothing at all (D-116 §3)");

        credentialChanges.Should().AllSatisfy(record =>
            record.GetProperty("after").GetRawText().Should().Contain(
                AuditRedactedAttribute.Placeholder,
                "the hash is absent because it was replaced, not because the record is absent"));
    }

    // ---- AC-117-F · the grant path is shown ----------------------------------------------------

    /// <summary>
    /// <c>AC-117-F</c>. The Owner holds no assignment row on the project, and his own trail says so.
    /// </summary>
    /// <remarks>
    /// KAFF-116's whole reason: global reach leaves no <c>ProjectAssignment</c> to point at, so
    /// without <c>GrantPath</c> the trail that watches the Owner cannot tell "Owner, globally" from
    /// "assigned on 3 June". The assertion is paired deliberately — the Owner's own record says
    /// <c>OwnerGlobal</c> and the Technical Office user's record on the same project says
    /// <c>Assignment</c> — because a reading that hardcoded either value would pass a test that
    /// checked only one.
    /// </remarks>
    [Fact]
    public async Task The_owners_own_global_reach_is_legible_in_his_own_trail()
    {
        await AssignAsync(_projectA, _staffedOnA);

        await OwnerHoldsNoAssignmentOnAsync(_projectA);

        JsonElement[] records = await ReadAsOwnerAsync($"?projectId={_projectA}");

        records
            .Where(record => ActorOf(record) == _owner)
            .Should().NotBeEmpty()
            .And.AllSatisfy(record => record.GetProperty("grantPath").GetString().Should().Be(
                nameof(ProjectAccessPath.OwnerGlobal),
                "the Owner reached this project through no assignment row, and the record he reads "
                + "about himself has to say which authority he used — AC-117-F"));

        records
            .Where(record => record.GetProperty("id").GetGuid() == _technicalOfficeAssignment)
            .Should().AllSatisfy(record => record.GetProperty("grantPath").GetString().Should().Be(
                nameof(ProjectAccessPath.Assignment),
                "the same field on the same project distinguishes the two authorities, or it is "
                + "reporting a constant"));
    }

    // ---- AC-117-G · a rejection shows its reason -----------------------------------------------

    /// <summary>
    /// <c>AC-117-G</c>. spec.md §7: never a silent step-back, never a rejection without a stored
    /// reason — and a stored reason nobody can read is the same thing as no reason.
    /// </summary>
    [Fact]
    public async Task A_recorded_reason_is_read_back_with_the_record()
    {
        const string Reason = "استقال وتم إنهاء التعاقد في ٣٠ سبتمبر";

        await DeactivateAsync(_leaver, Reason);

        JsonElement[] records = await ReadAsOwnerAsync($"?actorUserId={_owner}");

        records
            .Where(record => record.GetProperty("entityId").ValueKind != JsonValueKind.Null
                             && record.GetProperty("entityId").GetGuid() == _leaver)
            .Select(record => record.GetProperty("reason").GetString())
            .Should().Contain(Reason, "the reason is part of the record and is displayed with it");
    }

    // ---- AC-117-H · the trail cannot be edited from the API ------------------------------------

    /// <summary>
    /// <c>AC-117-H</c>, the halves this file owns: no endpoint removes a record, and the database
    /// refuses a direct delete.
    /// </summary>
    /// <remarks>
    /// The <b>update</b> half already lives in <c>AuditMechanismTests</c> -&gt;
    /// <c>An_audit_record_cannot_be_changed_afterwards</c>, and is not restated here (D-116 §1). The
    /// endpoint-absence half is asserted over every route the host actually built, in
    /// <c>EndpointPermissionCoverageTests</c> -&gt; <c>No_endpoint_writes_to_the_audit_trail</c> —
    /// a grep over <c>Endpoint.cs</c> files would only see what somebody wrote, which is the artefact
    /// D-067 showed is not trustworthy. What is left, and what this asserts, is the delete.
    /// </remarks>
    [Fact]
    public async Task A_record_cannot_be_deleted_from_the_database_either()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => reader.Database.ExecuteSqlAsync(
                $"DELETE FROM audit_records WHERE id = {_technicalOfficeAssignment}",
                Ct),
            DatabaseGuard.AppendOnly);

        (await reader.AuditRecords.CountAsync(record => record.Id == _technicalOfficeAssignment, Ct))
            .Should().Be(1, "the row the delete was refused for is still there — otherwise the guard "
                            + "would be reporting a refusal of nothing");
    }

    // ---- the payload, and the filters ----------------------------------------------------------

    /// <summary>
    /// The payload is pinned as a whitelist, because it is the most sensitive payload in the system.
    /// </summary>
    /// <remarks>
    /// D-106: a seven-word blocklist let a <c>decimal RetainedAmount</c> onto the wire past a green
    /// 241/241 suite. Anything added here fails, whatever it is called — which matters most from
    /// slice 3, when the snapshots this type carries start describing postings.
    /// </remarks>
    [Fact]
    public void The_payload_is_the_record_and_nothing_more()
    {
        typeof(Kaff.Api.Features.Audit.ReadAuditTrail.AuditEntry)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [
                    "Id", "OccurredAt", "Action", "EventType", "EntityType", "EntityId",
                    "ActorUserId", "ActorDisplayName", "ActorRole", "Before", "After",
                    "ChangedProperties", "Reason", "CorrelationId", "ProjectId", "GrantPath",
                    "RequestPath", "IpAddress",
                ],
                "a derived field, a joined balance or a second copy of anything added here fails");

        typeof(Kaff.Api.Features.Audit.ReadAuditTrail.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["Records"],
                "a wrapper object, so a total and a page can be added without breaking the shape");
    }

    /// <summary>
    /// The project filter narrows and does not scope, and an inverted date range is refused rather
    /// than answered with an empty trail.
    /// </summary>
    /// <remarks>
    /// <b>The refusal is the interesting half.</b> An inverted range matches no row, so defaulting it
    /// would render exactly like a quiet week — on the one screen whose purpose is to settle whether
    /// something happened. Same reasoning as <c>ClientListFilterParsing</c>'s unknown status: absent is
    /// a default, wrong is a mistake, and they must not produce the same list.
    /// </remarks>
    [Fact]
    public async Task The_filters_narrow_and_an_inverted_range_is_refused()
    {
        await AssignAsync(_projectA, _staffedOnA);
        await AssignAsync(_projectB, _staffedOnB);

        JsonElement[] onA = await ReadAsOwnerAsync($"?projectId={_projectA}");

        ProjectIdsOf(onA).Should().OnlyContain(
            id => id == _projectA,
            "a project filter narrows the Owner's company-wide read — filtering is not scoping, and "
            + "only the Owner does either (story rule 5)");

        ProjectIdsOf(onA).Should().NotBeEmpty("a filter that returns nothing narrows nothing");

        JsonElement[] byActor = await ReadAsOwnerAsync($"?actorUserId={_technicalOffice}");

        byActor.Should().NotBeEmpty()
            .And.AllSatisfy(record => record.GetProperty("actorUserId").GetGuid()
                .Should().Be(_technicalOffice));

        HttpResponseMessage inverted = await GetAsync(
            "?from=2026-09-05T00:00:00Z&to=2026-09-01T00:00:00Z", _owner, Role.Owner, null);

        inverted.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument problem = JsonDocument.Parse(await inverted.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString()
            .Should().Be("errors.audit.date_range_inverted");
    }

    /// <summary>A date window returns what falls inside it and nothing that falls outside it.</summary>
    [Fact]
    public async Task A_date_window_returns_what_falls_inside_it()
    {
        await AssignAsync(_projectA, _staffedOnA);

        DateTimeOffset justBefore = DateTimeOffset.UtcNow.AddMinutes(-5);

        JsonElement[] recent = await ReadAsOwnerAsync(
            $"?from={Uri.EscapeDataString(justBefore.ToString("O"))}");

        recent.Should().NotBeEmpty("the assignment above was written seconds ago");

        JsonElement[] ancient = await ReadAsOwnerAsync(
            $"?to={Uri.EscapeDataString(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O"))}");

        ancient.Should().BeEmpty(
            "nothing in this system was recorded in 2019 — and an upper bound that returned today's "
            + "records would mean the filter is not applied at all");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<JsonElement[]> ReadAsOwnerAsync(string query = "")
    {
        HttpResponseMessage response = await GetAsync(query, _owner, Role.Owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the Owner reads the trail — AC-117-A");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return [.. body.RootElement.GetProperty("records").EnumerateArray().Select(Detach)];
    }

    /// <summary>Copies an element out of the document that owns it, so it survives disposal.</summary>
    private static JsonElement Detach(JsonElement element) => element.Clone();

    /// <summary>
    /// The actor, or null. Null is legal and expected: a change made outside a request — the fixture's
    /// own seeding, a migration — names no user, and <c>ck_audit_records_actor_is_named_completely</c>
    /// then requires it to name no role either.
    /// </summary>
    private static Guid? ActorOf(JsonElement record)
    {
        JsonElement actor = record.GetProperty("actorUserId");

        return actor.ValueKind == JsonValueKind.Null ? null : actor.GetGuid();
    }

    private static IEnumerable<Guid> ProjectIdsOf(IEnumerable<JsonElement> records) =>
        records
            .Select(record => record.GetProperty("projectId"))
            .Where(id => id.ValueKind != JsonValueKind.Null)
            .Select(id => id.GetGuid());

    private async Task<HttpResponseMessage> GetAsync(
        string query,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/audit" + query, UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task AssignAsync(Guid projectId, Guid userId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/projects/{projectId}/assignments", UriKind.Relative))
        {
            Content = JsonContent.Create(new { userId, level = nameof(AssignmentLevel.Standard) }),
        };

        await StampAsync(request, _owner, Role.Owner, null, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "the arrangement has to actually happen, or the assertions describe an empty trail: {0}",
            await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task DeactivateAsync(Guid userId, string reason)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/users/{userId}/deactivate", UriKind.Relative))
        {
            Content = JsonContent.Create(new { reason }),
        };

        await StampAsync(request, _owner, Role.Owner, null, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NoContent, HttpStatusCode.OK, HttpStatusCode.Conflict],
            "already deactivated is acceptable — another test in this class may have got there first; "
            + "anything else means the arrangement failed: {0}",
            await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// Sets a password through the domain, as the holder does it, and returns the security stamp it
    /// rotated to.
    /// </summary>
    /// <remarks>
    /// The actor is the holder rather than the Owner, because that is who <c>SetOwnPassword</c> is
    /// for (D-049 ruling 4: after the forced first change the Owner does not know the credential that
    /// acts as that account). It also makes <c>?actorUserId=</c> the natural way to find the record.
    /// </remarks>
    private async Task<string> SetPasswordAsync(Guid userId, string hash)
    {
        var actor = new StubActor(userId, Role.Finance);

        await using KaffDbContext context = _database.CreateContext(actor, Gated(actor));

        User user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Ct);

        user.SetOwnPassword(hash);

        await context.SaveChangesAsync(Ct);

        return user.SecurityStamp;
    }

    private async Task OwnerHoldsNoAssignmentOnAsync(Guid projectId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.ProjectAssignments.AnyAsync(
            assignment => assignment.ProjectId == projectId && assignment.UserId == _owner, Ct))
            .Should().BeFalse(
                "AC-117-F is about a project the Owner holds no assignment row for — if he had one, "
                + "the grant path could legitimately be Assignment and the test would prove nothing");
    }

    private async Task StampAsync(
        HttpRequestMessage request,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        if (actorClientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, actorClientId.Value.ToString());
        }
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private static AuditContext Gated(StubActor actor)
    {
        var audit = new AuditContext();
        audit.ActorVerifiedAs(new AuditActor(actor.UserId, actor.DisplayName, actor.Role));
        return audit;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("AUD-C1"), "عميل سجل التدقيق", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("AUD-PA"), "مشروع أ", company.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("AUD-PB"), "مشروع ب", company.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("aud-owner", Role.Owner);
        User finance = MakeUser("aud-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("aud-hr", Role.Hr, Department.Hr);
        User technicalOffice = MakeUser(
            "aud-techoffice", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser(
            "aud-siteeng", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "aud-headdesign", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("aud-marketing", Role.MarketingSales, Department.Marketing);
        User subcontractor = MakeUser("aud-subcontractor", Role.Subcontractor);
        User portal = MakeUser("aud-portal", Role.Client, clientId: company.Id);
        // Not site engineers: spec.md §9 attaches Junior/Supervisor to that role alone, and
        // ProjectAssignment.Create refuses AssignmentLevel.Standard for one.
        User staffedOnA = MakeUser("aud-staff-a", Role.Finance, Department.Finance);
        User staffedOnB = MakeUser("aud-staff-b", Role.Finance, Department.Finance);
        User staffedByTechnicalOffice = MakeUser("aud-staff-to", Role.Finance, Department.Finance);
        User leaver = MakeUser("aud-leaver", Role.Finance, Department.Finance);
        User credentialHolder = MakeUser("aud-credentials", Role.Finance, Department.Finance);

        context.Clients.Add(company);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(
            owner, finance, hr, technicalOffice, siteEngineer, headOfDesign, marketing, subcontractor,
            portal, staffedOnA, staffedOnB, staffedByTechnicalOffice, leaver, credentialHolder);

        await context.SaveChangesAsync(Ct);

        // The Technical Office user is on project A, actively. Every other permission in this system
        // would admit them to it; AC-117-B is that this one does not.
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, technicalOffice, AssignmentLevel.Standard, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _technicalOffice = technicalOffice.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
        _subcontractor = subcontractor.Id;
        _portalClient = portal.Id;
        _staffedOnA = staffedOnA.Id;
        _staffedOnB = staffedOnB.Id;
        _leaver = leaver.Id;
        _credentialHolder = credentialHolder.Id;

        _technicalOfficeAssignment = await WriteTechnicalOfficeChangeAsync(
            projectA.Id, staffedByTechnicalOffice);
    }

    /// <summary>
    /// A change made <b>by</b> the Technical Office user, <b>on</b> the project they are assigned to,
    /// reached <b>through</b> that assignment — the exact record AC-117-B says they may not read.
    /// </summary>
    /// <remarks>
    /// Written through the interceptor rather than inserted raw, so the row is one the product could
    /// really produce: real actor, real project tag, real grant path. Slice 1 ships no endpoint a
    /// Technical Office user can write a project row through, which is why the save is made here
    /// rather than over HTTP.
    /// </remarks>
    private async Task<Guid> WriteTechnicalOfficeChangeAsync(Guid projectId, User assignee)
    {
        var actor = new StubActor(_technicalOffice, Role.TechnicalOffice);

        AuditContext audit = Gated(actor);
        audit.GrantedThrough(ProjectAccessPath.Assignment);

        await using (KaffDbContext context = _database.CreateContext(actor, audit))
        {
            context.ProjectAssignments.Add(
                ProjectAssignment.Create(projectId, assignee, AssignmentLevel.Standard, _technicalOffice, Now).Value);

            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.AuditRecords
            .Where(record => record.ActorUserId == _technicalOffice && record.ProjectId == projectId)
            .Select(record => record.Id)
            .SingleAsync(Ct);
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department,
            subDepartment, clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 5, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>An actor for the saves that cannot go through HTTP. See <see cref="WriteTechnicalOfficeChangeAsync"/>.</summary>
    private sealed class StubActor : ICurrentUser
    {
        public StubActor(Guid userId, Role role)
        {
            UserId = userId;
            Role = role;
        }

        public bool IsAuthenticated => true;

        public Guid? UserId { get; }

        public string DisplayName => "audit-trail-test-actor";

        public Role? Role { get; }

        public Department? Department => null;

        public OperationsSubDepartment? OperationsSubDepartment => null;

        public Guid? ClientId => null;

        public string? SecurityStamp => null;
    }
}
