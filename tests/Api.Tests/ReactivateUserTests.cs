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
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-112 — <c>POST /api/users/{userId}/reactivate</c>. A returning employee is the same person in
/// the record and none of their old access, per D-049 ruling 5: "a returning employee gets a new
/// password and zero project assignments — nothing is restored automatically."
/// </summary>
/// <remarks>
/// <para>
/// <b>AC-112-B's messageKey is asserted as <c>errors.auth.forbidden</c>, not the story's
/// <c>errors.auth.not_assigned_to_project</c></b> — decisions.md D-080, which answered D-078's
/// question for this exact criterion by name and ruled the blanket key correct. The shipped gate has
/// one 403 key for every gate refusal today
/// [Verified: 2026-08-25 @ <c>Program.cs</c> -&gt; <c>AddProblemDetails</c>], the same key
/// <c>DeactivateUserTests</c> and <c>RevokeProjectAssignmentTests</c> already assert for the identical
/// reason.
/// </para>
/// <para>
/// <b>AC-112-D and AC-112-F are not exercised end to end here.</b> Both name a sign-in
/// (<c>AC-112-D</c>: "attempt to sign in with the old password") or a must-change-password session
/// gate (<c>AC-112-F</c>, which the criterion itself sources to <c>AC-103-B</c>) that no endpoint in
/// this codebase implements yet — KAFF-101a (sign-in) and KAFF-103 (change password, whose gate is
/// D-072 §2's open question about how far a <c>mustChangePassword</c> session reaches) are both
/// unbuilt [Verified: 2026-08-25 — no <c>SignIn</c> or <c>ChangePassword</c> folder under
/// <c>src/Api/Features</c>, no <c>Verify</c> method on <c>PasswordHasher</c>]. AC-112-D is exercised
/// here at the one layer this story can reach: the stored hash changes and <c>MustChangePassword</c>
/// flips, which is what makes the old credential unable to verify once KAFF-101a exists to ask. Both
/// criteria are named, not silently dropped — see the two tests below and the report this story ships
/// with.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ReactivateUserTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _projectC;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _leaver;

    public ReactivateUserTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-112-A · a returning user is the same user -----------------------------------------

    /// <summary>
    /// AC-112-A. Twelve audit records naming the leaver, written before deactivation, still resolve
    /// to the same user id after reactivation — the row was never replaced.
    /// </summary>
    [Fact]
    public async Task Twelve_audit_records_written_before_leaving_still_name_the_reactivated_user()
    {
        string session = await StaleSession(_leaver);
        Uri write = new($"/probe/projects/{_projectA}/write", UriKind.Relative);

        for (int i = 0; i < 12; i++)
        {
            (await SendAsync(write, _leaver, session)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        User reactivated = await reader.Users.SingleAsync(user => user.Id == _leaver, Ct);
        reactivated.Id.Should().Be(_leaver, "it is the same row — reactivation never creates a second account");
        reactivated.IsActive.Should().BeTrue();
        reactivated.DeactivatedAt.Should().BeNull();

        (await reader.AuditRecords.CountAsync(record => record.ActorUserId == _leaver, Ct))
            .Should().BeGreaterThanOrEqualTo(12, "nothing the leaver did before leaving loses its actor");
    }

    // ---- AC-112-B · zero access to any project, AC-112-C · the revoked rows are not resurrected --

    /// <summary>
    /// AC-112-B and AC-112-C together — reactivation touches no <c>ProjectAssignment</c> row at all.
    /// </summary>
    [Fact]
    public async Task Reactivation_restores_no_assignment_and_leaves_the_revoked_rows_exactly_as_they_were()
    {
        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (DateTimeOffset? RevokedAt, Guid? RevokedByUserId)[] beforeReactivation = await RevocationStateAsync(_leaver);
        beforeReactivation.Should().HaveCount(3).And.AllSatisfy(row =>
        {
            row.RevokedAt.Should().NotBeNull();
            row.RevokedByUserId.Should().Be(_owner);
        });

        (await ReactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.ProjectAssignments
                .CountAsync(assignment => assignment.UserId == _leaver && assignment.RevokedAt == null, Ct))
            .Should().Be(0, "AC-112-B — zero active assignments, nothing is restored automatically");

        (await RevocationStateAsync(_leaver)).Should().BeEquivalentTo(
            beforeReactivation,
            "AC-112-C — reactivation did not clear, delete or duplicate the revoked rows");

        // The second half of AC-112-B: a request against one of the three old projects, on a session
        // issued after reactivation, is refused — there is no assignment left to grant it.
        string sessionAfterReactivation = await StaleSession(_leaver);

        HttpResponseMessage refused = await SendAsync(
            new Uri($"/probe/projects/{_projectA}/write", UriKind.Relative), _leaver, sessionAfterReactivation);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(refused)).Should().Be(
            "errors.auth.forbidden",
            "the shipped gate has one blanket 403 key today — decisions.md D-080 answered this exact criterion");
    }

    // ---- AC-112-E · an old token does not come back to life with them ----------------------------

    /// <summary>
    /// AC-112-E. Proven independent of AC-112-B: the leaver is re-assigned to the same project after
    /// reactivation, so a fresh, valid session succeeds — and the pre-deactivation token still fails,
    /// which isolates the refusal to the rotated stamp rather than to the empty assignment table.
    /// </summary>
    /// <remarks>
    /// decisions.md D-051 (N5) and KAFF-112 rule 9a: <c>User.Reactivate</c> now rotates
    /// <c>SecurityStamp</c> on its own, so a token minted before the deactivation cannot become valid
    /// again "the moment the account comes back" even once the person is legitimately back on the team.
    /// </remarks>
    [Fact]
    public async Task A_token_minted_before_deactivation_is_still_refused_even_after_reassignment()
    {
        string staleToken = await StaleSession(_leaver);

        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AssignAsync(_hr, _projectA, _leaver, AssignmentLevel.Junior))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        string currentToken = await StaleSession(_leaver);
        Uri write = new($"/probe/projects/{_projectA}/write", UriKind.Relative);

        (await SendAsync(write, _leaver, currentToken)).StatusCode.Should().Be(
            HttpStatusCode.OK, "the fresh assignment is real — this is the control that proves the refusal below is about the token, not the row");

        (await SendAsync(write, _leaver, staleToken)).StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the same request, the same fresh assignment, an older stamp — reactivation rotated it (D-051 N5)");
    }

    // ---- AC-112-D · the old password is dead ------------------------------------------------------

    /// <summary>
    /// AC-112-D, at the layer this story can reach. No sign-in endpoint exists yet to attempt with the
    /// old plaintext (see this file's remarks) — asserted instead is the fact that makes a later
    /// sign-in attempt fail: the stored hash is a different value, produced from a fresh salt, once
    /// reactivation issues a new temporary password.
    /// </summary>
    [Fact]
    public async Task The_stored_credential_changes_when_a_temporary_password_is_issued_on_reactivation()
    {
        string? hashBeforeLeaving = await PasswordHashAsync(_leaver);
        hashBeforeLeaving.Should().NotBeNullOrEmpty("the seed gave the leaver a real password before they left");

        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReactivateAsync(_owner, _leaver, temporaryPassword: "brand-new-temp-pw"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        User reactivated = await reader.Users.SingleAsync(user => user.Id == _leaver, Ct);

        reactivated.PasswordHash.Should().NotBe(
            hashBeforeLeaving, "rule 3 clears the stored credential before rule 4 issues a new one — the old hash never survives");
        reactivated.MustChangePassword.Should().BeTrue("rule 4 — a credential the Owner chose must be replaced");
    }

    /// <summary>
    /// The other half of rule 3 — reactivating with no temporary password still clears the old one.
    /// The account comes back able to authenticate with nothing until somebody issues a credential.
    /// </summary>
    [Fact]
    public async Task Reactivating_with_no_temporary_password_still_clears_the_old_credential()
    {
        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await PasswordHashAsync(_leaver)).Should().BeNull(
            "rule 3 is unconditional — the old credential does not come back just because nobody issued a new one");
    }

    // ---- AC-112-G · reactivating an active user is refused -----------------------------------------

    [Fact]
    public async Task Reactivating_an_active_user_is_refused()
    {
        HttpResponseMessage response = await ReactivateAsync(_owner, _finance);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_already_active");

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.Users.SingleAsync(user => user.Id == _finance, Ct))
            .IsActive.Should().BeTrue("the refused attempt changed nothing");
    }

    // ---- AC-112-H · only the Owner may ----------------------------------------------------------

    /// <summary>
    /// AC-112-H. Rule 1, D-044 ruling 1 — the same <c>UserManage</c> row <c>DeactivateUser</c> and
    /// <c>CreateUser</c> use, checked here rather than assumed to still be the right row.
    /// </summary>
    [Fact]
    public async Task Nobody_but_the_owner_can_reactivate_a_user()
    {
        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        foreach (Guid caller in new[] { _hr, _finance })
        {
            HttpResponseMessage response = await ReactivateAsync(caller, _leaver);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await MessageKeyAsync(response)).Should().Be(
                "errors.auth.forbidden", "the refusal must be renderable in Arabic");
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.Users.SingleAsync(user => user.Id == _leaver, Ct))
            .IsActive.Should().BeFalse("no refused attempt reactivated anybody");
    }

    // ---- AC-112-I · putting them back on a project is a deliberate act with a named author --------

    [Fact]
    public async Task Assigning_a_reactivated_user_to_their_old_project_creates_a_new_row_with_a_fresh_author()
    {
        (await DeactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext beforeReader = _database.CreateBareContext();
        ProjectAssignment oldRow = await beforeReader.ProjectAssignments
            .SingleAsync(a => a.UserId == _leaver && a.ProjectId == _projectA, Ct);

        (await ReactivateAsync(_owner, _leaver)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        DateTimeOffset before = DateTimeOffset.UtcNow;

        HttpResponseMessage assigned = await AssignAsync(_hr, _projectA, _leaver, AssignmentLevel.Junior);
        assigned.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext afterReader = _database.CreateBareContext();

        List<ProjectAssignment> rows = await afterReader.ProjectAssignments
            .Where(a => a.UserId == _leaver && a.ProjectId == _projectA)
            .ToListAsync(Ct);

        rows.Should().HaveCount(2, "the old revoked row stays, and this is a new row, not a resurrection");

        ProjectAssignment freshRow = rows.Single(row => row.Id != oldRow.Id);
        freshRow.AssignedByUserId.Should().Be(_hr, "HR performed this act, not the Owner who reactivated the person");
        freshRow.AssignedAt.Should().BeOnOrAfter(before);
        freshRow.RevokedAt.Should().BeNull();

        ProjectAssignment stillOldRow = rows.Single(row => row.Id == oldRow.Id);
        stillOldRow.RevokedAt.Should().Be(oldRow.RevokedAt, "the new assignment does not touch the old row");
    }

    /// <summary>A route naming a user that does not exist is a 404 the client can translate.</summary>
    [Fact]
    public async Task Reactivating_a_user_who_does_not_exist_is_refused()
    {
        HttpResponseMessage response = await ReactivateAsync(_owner, Guid.CreateVersion7());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_not_found");
    }

    // ---- helpers -------------------------------------------------------------------------------------

    private async Task<string> StaleSession(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private async Task<string?> PasswordHashAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.PasswordHash)
            .SingleAsync(Ct);
    }

    private async Task<(DateTimeOffset? RevokedAt, Guid? RevokedByUserId)[]> RevocationStateAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == userId)
            .OrderBy(assignment => assignment.ProjectId)
            .Select(assignment => new ValueTuple<DateTimeOffset?, Guid?>(assignment.RevokedAt, assignment.RevokedByUserId))
            .ToArrayAsync(Ct);
    }

    private async Task<HttpResponseMessage> DeactivateAsync(Guid actorId, Guid targetUserId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/users/{targetUserId}/deactivate", UriKind.Relative))
        {
            Content = JsonContent.Create(new { reason = (string?)null }),
        };

        await AttachActorHeadersAsync(request, actorId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> ReactivateAsync(
        Guid actorId, Guid targetUserId, string? temporaryPassword = null, string? reason = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/users/{targetUserId}/reactivate", UriKind.Relative))
        {
            Content = JsonContent.Create(new { temporaryPassword, reason }),
        };

        await AttachActorHeadersAsync(request, actorId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> AssignAsync(
        Guid actorId, Guid projectId, Guid targetUserId, AssignmentLevel level)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/projects/{projectId}/assignments", UriKind.Relative))
        {
            Content = JsonContent.Create(new { userId = targetUserId, level = level.ToString() }),
        };

        await AttachActorHeadersAsync(request, actorId);

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>
    /// The actor's real role, because a real token carries one and the audit record's <c>ActorRole</c>
    /// is read from it rather than from the database — the gate itself does not consult it (see
    /// <see cref="SendAsync"/>, which omits it deliberately).
    /// </summary>
    private async Task AttachActorHeadersAsync(HttpRequestMessage request, Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        var actor = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .SingleAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, actor.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, actor.Role.ToString());
    }

    /// <summary>
    /// Issues a GET as <paramref name="userId"/> on a session stamp captured earlier. Role and
    /// department are deliberately not sent — this is what makes the assertion about the database
    /// rather than about the token.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(Uri route, Guid userId, string securityStamp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, securityStamp);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("RCT-C1"), "عميل العودة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("RCT-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("RCT-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;
        Project projectC = Project.Create(
            UniqueNames.Code("RCT-PC"), "مشروع ج", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("rct-owner", Role.Owner);
        User hr = MakeUser("rct-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("rct-finance", Role.Finance, Department.Finance);
        User leaver = MakeUser(
            "rct-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);

        // The leaver has a real credential before they leave, so AC-112-D has a hash to compare
        // against. Not exercised by any endpoint yet — set directly, the way KAFF-106's Owner-issued
        // temporary password would have been, hashed the same way KAFF-101a will verify it.
        leaver.SetOwnPassword(PasswordHasher.Hash("old-password-1")).IsSuccess.Should().BeTrue();

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB, projectC);
        context.Users.AddRange(owner, hr, finance, leaver);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(projectA.Id, leaver, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            ProjectAssignment.Create(projectB.Id, leaver, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            ProjectAssignment.Create(projectC.Id, leaver, AssignmentLevel.Junior, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _projectC = projectC.Id;
        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _leaver = leaver.Id;
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
