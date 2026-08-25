using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-110 — <c>POST /api/users/{userId}/deactivate</c>, and access ending on the next request.
/// </summary>
/// <remarks>
/// <para>
/// <b>The load-bearing shape of this file is that every "next request" is sent with a security stamp
/// captured <i>before</i> the deactivation.</b> A helper that re-read the stamp would silently
/// re-authenticate the caller, and the suite would report the rule as held while the product only
/// held "a token issued after the change works" — which is the opposite claim. <c>StaleSession</c>
/// exists for that reason and nothing here reads a stamp after the act.
/// </para>
/// <para>
/// spec.md §9, decisions.md D-048: the token supplies identity, the database supplies authority, on
/// every request. This story adds no mechanism — it exercises that one.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class DeactivateUserTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _projectC;

    private Guid _owner;
    private Guid _secondOwner;
    private Guid _hr;
    private Guid _finance;
    private Guid _siteEngineer;

    public DeactivateUserTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-110-A · access ends on the next request ---------------------------------------------

    /// <summary>
    /// AC-110-A. A session that was valid a second earlier is refused, with no re-authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Finance user succeeds on a project-scoped request, is deactivated, and <b>repeats the
    /// identical request with the identical session</b> — same user id, same stamp string, captured
    /// once at the top. Nothing between the two calls issues a token, and the endpoint under test
    /// touches no session store, because there is none: <c>PermissionSubjectReader.ReadAsync</c>
    /// filters <c>user.IsActive</c> in the <c>WHERE</c> clause on every authorized request
    /// [Verified: 2026-08-24 @ <c>PermissionSubjectReader.cs</c> -&gt; <c>ReadAsync</c>].
    /// </para>
    /// <para>
    /// "And no state was changed by the attempt" is asserted on the probe route's own write: the
    /// refused request never reaches the handler, so it leaves no audit record.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_next_request_on_the_same_session_is_refused_with_no_re_authentication()
    {
        string session = await StaleSession(_finance);

        Uri write = new($"/probe/projects/{_projectA}/write", UriKind.Relative);

        (await SendAsync(write, _finance, session))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the session is valid a second before the act");

        int recordsBefore = await AuditRecordCountAsync(_finance);

        (await DeactivateAsync(_owner, _finance)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SendAsync(write, _finance, session))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "authority is re-read from the database on every request — the token carries only an id");

        (await AuditRecordCountAsync(_finance)).Should().Be(
            recordsBefore, "a refused request never reaches the handler, so it changes nothing");
    }

    // ---- AC-110-B · including the requests that name no project ---------------------------------

    /// <summary>
    /// AC-110-B. The F-11 path, exercised separately from the project-scoped one.
    /// </summary>
    /// <remarks>
    /// The access policy is consulted only when a request names a project, so before D-048 every
    /// <c>CompanyWide</c> permission was decided from token claims alone and <b>a deactivated Owner
    /// kept <c>UserManage</c></b>. A suite that only exercised <see cref="The_next_request_on_the_same_session_is_refused_with_no_re_authentication"/>
    /// would have stayed green throughout that defect, which is why this is its own test and its own
    /// route.
    /// </remarks>
    [Fact]
    public async Task A_deactivated_owner_is_refused_on_a_company_wide_endpoint_too()
    {
        string session = await StaleSession(_secondOwner);

        Uri companyWide = new("/probe/users", UriKind.Relative);

        (await SendAsync(companyWide, _secondOwner, session))
            .StatusCode.Should().Be(HttpStatusCode.OK, "UserManage is company-wide and the Owner holds it");

        (await DeactivateAsync(_owner, _secondOwner)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SendAsync(companyWide, _secondOwner, session))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "there is no account the rule exempts — the subject read filters IsActive before any role");
    }

    // ---- AC-110-C · every device, not just this one ---------------------------------------------

    /// <summary>
    /// AC-110-C. Two devices, one act, both refused on their next request.
    /// </summary>
    /// <remarks>
    /// Two independent <c>HttpClient</c>s, each holding a session issued before the deactivation and
    /// neither of them the one the Owner is using. The mechanism is stamp rotation —
    /// <c>User.Deactivate</c> rotates <c>SecurityStamp</c>
    /// [Verified: 2026-08-24 @ <c>User.cs</c> -&gt; <c>Deactivate</c>] and the subject read compares
    /// it in the <c>WHERE</c> clause, so revocation is all-or-nothing across every token in existence
    /// (D-051 N5, D-053).
    /// </remarks>
    [Fact]
    public async Task Both_devices_are_refused_on_their_next_request()
    {
        string session = await StaleSession(_siteEngineer);

        using HttpClient phone = _factory.CreateClient();
        using HttpClient laptop = _factory.CreateClient();

        Uri route = new($"/probe/projects/{_projectA}", UriKind.Relative);

        foreach (HttpClient device in new[] { phone, laptop })
        {
            (await SendAsync(route, _siteEngineer, session, device))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await DeactivateAsync(_owner, _siteEngineer)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        foreach (HttpClient device in new[] { phone, laptop })
        {
            (await SendAsync(route, _siteEngineer, session, device))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    "the stamp rotated, so the sessions the user is not holding die too");
        }
    }

    // ---- AC-110-F · their assignments are revoked, and stay on file ------------------------------

    /// <summary>
    /// AC-110-F. Three assignments revoked, three rows still present, four audit records, one
    /// correlation id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The correlation id is taken from the response header the middleware echoes
    /// [Verified: 2026-08-24 @ <c>AuditCorrelationMiddleware.cs</c> -&gt; <c>HeaderName</c>], so the
    /// assertion is that the records share <i>the request's</i> id rather than merely sharing each
    /// other's — four records agreeing on a value none of them got from the request would pass the
    /// weaker form.
    /// </para>
    /// <para>
    /// One record per assignment, not one summary: CLAUDE.md wants what changed, before and after,
    /// and a single record cannot carry three (KAFF-111 rule 5).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_assignments_are_revoked_kept_on_file_and_audited_one_by_one()
    {
        HttpResponseMessage response = await DeactivateAsync(_owner, _siteEngineer);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid correlationId = Guid.Parse(
            response.Headers.GetValues(Kaff.Api.Common.Middleware.AuditCorrelationMiddleware.HeaderName).Single());

        await using KaffDbContext reader = _database.CreateBareContext();

        List<ProjectAssignment> rows = await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == _siteEngineer)
            .ToListAsync(Ct);

        rows.Should().HaveCount(3, "the rows stay on file — the historical team is not lost");
        rows.Select(row => row.ProjectId).Should().BeEquivalentTo(new[] { _projectA, _projectB, _projectC });

        rows.Should().AllSatisfy(row =>
        {
            row.RevokedAt.Should().NotBeNull();
            row.RevokedByUserId.Should().Be(_owner, "the revocations must not read as having no author");
            row.IsActive.Should().BeFalse();
            row.AssignedByUserId.Should().Be(_owner, "and AssignedAt/AssignedByUserId are intact");
        });

        List<AuditRecord> records = await reader.AuditRecords
            .Where(record => record.CorrelationId == correlationId)
            .ToListAsync(Ct);

        records.Should().HaveCount(4, "one User record and one per revoked assignment — one act, one story");

        records.Count(record => record.EntityType == nameof(User)).Should().Be(1);
        records.Count(record => record.EntityType == nameof(ProjectAssignment)).Should().Be(3);

        records.Should().AllSatisfy(record =>
        {
            record.Action.Should().Be(AuditAction.Modified);
            record.ActorUserId.Should().Be(_owner);
            record.ActorRole.Should().Be(Role.Owner);
        });

        records.Where(record => record.EntityType == nameof(ProjectAssignment))
            .Should().AllSatisfy(record => record.ChangedProperties.Should().BeEquivalentTo(
                new[] { nameof(ProjectAssignment.RevokedAt), nameof(ProjectAssignment.RevokedByUserId) }));

        AuditRecord user = records.Single(record => record.EntityType == nameof(User));

        user.ChangedProperties.Should().Contain(nameof(User.IsActive));
        user.ChangedProperties.Should().Contain(nameof(User.DeactivatedAt));

        user.GrantPath.Should().BeNull(
            "UserManage is company-wide: no project, no access policy, no path to name");

        using JsonDocument before = JsonDocument.Parse(user.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(user.AfterJson!);

        before.RootElement.GetProperty(nameof(User.IsActive)).GetBoolean().Should().BeTrue();
        after.RootElement.GetProperty(nameof(User.IsActive)).GetBoolean().Should().BeFalse();

        after.RootElement.GetProperty(nameof(User.SecurityStamp)).GetString()
            .Should().Be(AuditRedactedAttribute.Placeholder, "a stamp is a credential and never lands in the trail");
    }

    /// <summary>KAFF-111 rule 9 — a leaver with no assignments deactivates cleanly.</summary>
    [Fact]
    public async Task A_user_with_no_assignments_deactivates_and_writes_one_record()
    {
        HttpResponseMessage response = await DeactivateAsync(_owner, _hr);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid correlationId = Guid.Parse(
            response.Headers.GetValues(Kaff.Api.Common.Middleware.AuditCorrelationMiddleware.HeaderName).Single());

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.AuditRecords.CountAsync(record => record.CorrelationId == correlationId, Ct))
            .Should().Be(1, "there was nothing to revoke");
    }

    // ---- AC-110-G · the reason is stored when it is given ----------------------------------------

    /// <summary>
    /// AC-110-G. Recorded verbatim when supplied — and Q35 is why the other half is not asserted.
    /// </summary>
    /// <remarks>
    /// Whether the Owner <i>must</i> type one is open (Q35). This asserts only what is cited: that a
    /// supplied reason is stored as typed, on every record the act writes, and that omitting one is
    /// accepted rather than refused. A criterion refusing a blank reason would be inventing the rule
    /// the story deliberately withdrew.
    /// </remarks>
    [Fact]
    public async Task A_supplied_reason_is_stored_verbatim_and_an_absent_one_is_accepted()
    {
        const string Reason = "استقال بتاريخ ١ مايو";

        HttpResponseMessage response = await DeactivateAsync(_owner, _siteEngineer, Reason);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid correlationId = Guid.Parse(
            response.Headers.GetValues(Kaff.Api.Common.Middleware.AuditCorrelationMiddleware.HeaderName).Single());

        await using KaffDbContext reader = _database.CreateBareContext();

        List<AuditRecord> records = await reader.AuditRecords
            .Where(record => record.CorrelationId == correlationId)
            .ToListAsync(Ct);

        records.Should().HaveCount(4);
        records.Should().AllSatisfy(record => record.Reason.Should().Be(Reason, "verbatim, in Arabic, untranslated"));

        // The other half of the same rule: no reason is not a refusal (Q35 stays open).
        (await DeactivateAsync(_owner, _finance)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- AC-110-H · the record survives -----------------------------------------------------------

    /// <summary>
    /// AC-110-H. A user is never deleted, so the trail stays readable.
    /// </summary>
    /// <remarks>
    /// The trail names actors by id (KAFF-110 rule 6, D-049 ruling 5). Deleting the row would make
    /// every record the leaver wrote unattributable, which is the one thing an append-only table
    /// cannot be repaired from.
    /// </remarks>
    [Fact]
    public async Task Everything_the_leaver_did_still_names_them_and_the_row_still_exists()
    {
        string session = await StaleSession(_siteEngineer);
        Uri write = new($"/probe/projects/{_projectA}/write", UriKind.Relative);

        for (int i = 0; i < 12; i++)
        {
            (await SendAsync(write, _siteEngineer, session)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await DeactivateAsync(_owner, _siteEngineer)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.AuditRecords.CountAsync(record => record.ActorUserId == _siteEngineer, Ct))
            .Should().BeGreaterThanOrEqualTo(12, "nothing they did is withdrawn, reversed or hidden");

        (await reader.Users.AnyAsync(user => user.Id == _siteEngineer, Ct))
            .Should().BeTrue("leavers are deactivated, never deleted");
    }

    // ---- AC-110-I · only the Owner may -------------------------------------------------------------

    /// <summary>
    /// AC-110-I. HR and Finance are refused.
    /// </summary>
    /// <remarks>
    /// HR is the one worth naming: it holds <c>ProjectAssignmentManage</c> with global reach and
    /// staffs every project in the company, and it still cannot end an account. Deactivation is
    /// <c>UserManage</c>, Owner only (D-044 ruling 1).
    /// </remarks>
    [Fact]
    public async Task Nobody_but_the_owner_can_deactivate_a_user()
    {
        foreach (Guid caller in new[] { _hr, _finance })
        {
            HttpResponseMessage response = await DeactivateAsync(caller, _siteEngineer);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await MessageKeyAsync(response)).Should().Be(
                "errors.auth.forbidden", "the refusal must be renderable in Arabic");
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        User untouched = await reader.Users.SingleAsync(user => user.Id == _siteEngineer, Ct);

        untouched.IsActive.Should().BeTrue("no refused attempt deactivated anybody");

        (await reader.ProjectAssignments
            .CountAsync(assignment => assignment.UserId == _siteEngineer && assignment.RevokedAt == null, Ct))
            .Should().Be(3, "and no refused attempt revoked anything");
    }

    // ---- AC-110-J · twice is refused ---------------------------------------------------------------

    /// <summary>
    /// AC-110-J. The second deactivation is refused, and touches no assignment a second time.
    /// </summary>
    /// <remarks>
    /// Rule 5 is <b>uncited</b> — sourced to slice-0 code and to nothing Karim said — and is built
    /// under the readiness waiver countersigned by Nabil (decisions.md D-062 §1). <b>Q51 stays
    /// open.</b> The second half of the criterion is what makes the refusal worth having: a handler
    /// that revoked first and refused afterwards would restamp three already-revoked rows with a new
    /// timestamp and quietly rewrite when each person left.
    /// </remarks>
    [Fact]
    public async Task Deactivating_an_already_inactive_user_is_refused_and_touches_nothing()
    {
        (await DeactivateAsync(_owner, _siteEngineer)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        DateTimeOffset?[] firstRevocation = await RevocationTimestampsAsync(_siteEngineer);

        HttpResponseMessage again = await DeactivateAsync(_owner, _siteEngineer);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(again)).Should().Be("errors.identity.user_already_inactive");

        (await RevocationTimestampsAsync(_siteEngineer)).Should().BeEquivalentTo(
            firstRevocation, "no assignment is touched a second time");
    }

    /// <summary>A route naming a user that does not exist is a 404 the client can translate.</summary>
    [Fact]
    public async Task Deactivating_a_user_who_does_not_exist_is_refused()
    {
        HttpResponseMessage response = await DeactivateAsync(_owner, Guid.CreateVersion7());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_not_found");
    }

    // ---- helpers -------------------------------------------------------------------------------------

    /// <summary>
    /// The security stamp a token was issued against, captured <b>before</b> the act under test.
    /// </summary>
    /// <remarks>
    /// Named for what it becomes rather than for what it reads, because the whole point of every
    /// caller is that it is never refreshed. Re-reading it after the deactivation would silently turn
    /// "the next request is refused" into "a request made with a token issued afterwards is refused",
    /// which is a different and much weaker claim — and the test would still be green.
    /// </remarks>
    private async Task<string> StaleSession(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private async Task<HttpResponseMessage> DeactivateAsync(Guid actorId, Guid targetUserId, string? reason = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/users/{targetUserId}/deactivate", UriKind.Relative))
        {
            Content = JsonContent.Create(new { reason }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await StaleSession(actorId));

        // The actor's real role, because a real token carries one and the audit record's ActorRole is
        // read from it rather than from the database [Verified: 2026-08-24 @
        // HttpContextCurrentUser.cs -> Role]. The gate does not consult it — see SendAsync, which
        // omits it on purpose, and that is where every claim about "the next request" is made.
        Role? role = await ActorRoleAsync(actorId);

        if (role is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, role.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<Role?> ActorRoleAsync(Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => (Role?)user.Role)
            .FirstOrDefaultAsync(Ct);
    }

    /// <summary>
    /// Issues a GET as <paramref name="userId"/> on a session captured earlier.
    /// </summary>
    /// <remarks>
    /// The role, department and sub-department headers are deliberately <b>not</b> sent, and the
    /// stamp is a parameter rather than a lookup. Both together are what make the assertion about the
    /// database rather than about the token.
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(
        Uri route,
        Guid userId,
        string securityStamp,
        HttpClient? device = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, securityStamp);

        return await (device ?? _client).SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task<int> AuditRecordCountAsync(Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.AuditRecords.CountAsync(record => record.ActorUserId == actorId, Ct);
    }

    private async Task<DateTimeOffset?[]> RevocationTimestampsAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == userId)
            .OrderBy(assignment => assignment.ProjectId)
            .Select(assignment => assignment.RevokedAt)
            .ToArrayAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("DCT-C1"), "عميل الإيقاف", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("DCT-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("DCT-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;
        Project projectC = Project.Create(
            UniqueNames.Code("DCT-PC"), "مشروع ج", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("dct-owner", Role.Owner);
        User secondOwner = MakeUser("dct-owner-2", Role.Owner);
        User hr = MakeUser("dct-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("dct-finance", Role.Finance, Department.Finance);
        User siteEngineer = MakeUser(
            "dct-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB, projectC);
        context.Users.AddRange(owner, secondOwner, hr, finance, siteEngineer);

        await context.SaveChangesAsync(Ct);

        // The engineer is on all three, which is what AC-110-F counts. Finance is on the first so its
        // refusal after deactivation is about the account and not about a missing row — the whole
        // point of AC-110-A is that the request succeeded a second earlier.
        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(projectA.Id, siteEngineer, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            ProjectAssignment.Create(projectB.Id, siteEngineer, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            ProjectAssignment.Create(projectC.Id, siteEngineer, AssignmentLevel.Junior, owner.Id, Now).Value,
            ProjectAssignment.Create(projectA.Id, finance, AssignmentLevel.Standard, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _projectC = projectC.Id;
        _owner = owner.Id;
        _secondOwner = secondOwner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _siteEngineer = siteEngineer.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
