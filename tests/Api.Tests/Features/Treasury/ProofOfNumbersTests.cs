using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Treasury;

/// <summary>
/// <c>GET /api/projects/{projectId}/treasury/proof</c> — the read behind إثبات الأرقام (D-187).
/// </summary>
/// <remarks>
/// <para>
/// <b>The endpoint shipped with no server-side test at all.</b> It is a money read, and
/// <c>CLAUDE.md</c> makes permissions testing priority 2: one test per role asserting what it cannot
/// reach, hitting the endpoint directly rather than through the UI. The web route guard is convenience
/// and proves nothing about the server.
/// </para>
/// <para>
/// The gate is <c>TreasuryReadProject</c> with the route's project, so <b>role and assignment are both
/// required</b> (D-173). That permission is held by Owner, Finance and Site Engineer. The screen is
/// shown only to the Owner and Finance by the web guard — so the Site Engineer reaching the data by
/// calling directly is the deliberate, documented behaviour, and this file pins it rather than
/// pretending the guard is a boundary.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ProofOfNumbersTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _owner;
    private Guid _financeOnProjectA;
    private Guid _financeUnassigned;
    private Guid _siteEngineerOnProjectA;
    private Guid _hr;

    public ProofOfNumbersTests(PostgresDatabase database) => _database = database;

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

    // ---- role × assignment, over HTTP ------------------------------------------------------------

    /// <summary>
    /// The Owner holds the permission company-wide, so no assignment row is needed.
    /// </summary>
    [Fact]
    public async Task The_owner_reads_the_proof()
        => (await GetAsync(_projectA, _owner)).StatusCode.Should().Be(HttpStatusCode.OK);

    /// <summary>
    /// Finance holds it project-scoped, and this one is assigned.
    /// </summary>
    [Fact]
    public async Task Assigned_finance_reads_the_proof()
        => (await GetAsync(_projectA, _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.OK);

    /// <summary>
    /// <b>The half of "permission = role × assignment" that a role check alone would miss.</b> Same
    /// role as the test above, no assignment row, and the project exists.
    /// </summary>
    [Fact]
    public async Task Finance_without_an_assignment_is_refused()
        => (await GetAsync(_projectA, _financeUnassigned)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    /// <summary>
    /// Assigned to A, asking for B. The assignment must be to the project in the route, not to any
    /// project.
    /// </summary>
    [Fact]
    public async Task An_assignment_to_another_project_does_not_carry_over()
        => (await GetAsync(_projectB, _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    /// <summary>
    /// HR does not hold <c>TreasuryReadProject</c> at all, assigned or not.
    /// </summary>
    [Fact]
    public async Task A_role_without_the_permission_is_refused()
        => (await GetAsync(_projectA, _hr)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    /// <summary>
    /// <b>Deliberate, and worth pinning precisely because it surprises.</b> D-173 grants
    /// <c>TreasuryReadProject</c> to the Site Engineer, so an assigned one reaches this money data by
    /// calling the endpoint, even though the web route guard keeps the screen to the Owner and Finance.
    /// If this is not what Nabil wants, the permission catalogue is what changes — not the guard.
    /// </summary>
    [Fact]
    public async Task An_assigned_site_engineer_reaches_the_data_directly_although_the_screen_hides_it()
        => (await GetAsync(_projectA, _siteEngineerOnProjectA)).StatusCode.Should().Be(HttpStatusCode.OK);

    /// <summary>
    /// An unauthenticated call never reaches the handler.
    /// </summary>
    [Fact]
    public async Task An_anonymous_call_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RouteFor(_projectA));

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- the shape the screen depends on ---------------------------------------------------------

    /// <summary>
    /// <b>The expected side is never derived from the actual side.</b> No extract record exists, so the
    /// response says <c>NoExtractsToCompare</c> and carries no expectation. It must not quietly become
    /// <c>Available</c> with figures reconstructed from the postings, which would make every row match
    /// by construction and prove nothing.
    /// </summary>
    [Fact]
    public async Task With_no_extracts_the_expected_side_is_absent_and_says_so()
    {
        HttpResponseMessage response = await GetAsync(_projectA, _owner);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("expectedState").GetString()
            .Should().Be("NoExtractsToCompare");

        foreach (JsonElement proofEvent in body.RootElement.GetProperty("events").EnumerateArray())
        {
            proofEvent.GetProperty("expected").ValueKind.Should().Be(
                JsonValueKind.Null,
                "an event may not carry an expectation while there is nothing to derive one from");
        }
    }

    /// <summary>
    /// The contract-only checks do not need an extract: the hold expectation is the hold rate times the
    /// contract value, and it is live from the moment the project is signed. A project with no signed
    /// BOQ has a null contract value, so the check must report that rather than guess a figure.
    /// </summary>
    [Fact]
    public async Task A_project_with_no_signed_boq_reports_its_checks_without_inventing_a_contract_value()
    {
        HttpResponseMessage response = await GetAsync(_projectA, _owner);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("project").GetProperty("contractValue").ValueKind
            .Should().Be(JsonValueKind.Null, "nothing has been signed, so there is no contract value");

        body.RootElement.GetProperty("checks").EnumerateArray().Should().NotBeEmpty(
            "the checks are the live invariants and are always listed, even when one cannot be evaluated");
    }

    /// <summary>
    /// A project id that names nothing is not an empty proof. An empty reconciliation reads as "this
    /// project is in order", which is a different fact from "there is no such project".
    /// </summary>
    [Fact]
    public async Task An_unknown_project_is_not_answered_with_an_empty_proof()
    {
        HttpResponseMessage response = await GetAsync(Guid.CreateVersion7(), _owner);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static Uri RouteFor(Guid projectId)
        => new($"/api/projects/{projectId}/treasury/proof", UriKind.Relative);

    private async Task<HttpResponseMessage> GetAsync(Guid projectId, Guid actorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RouteFor(projectId));

        (string stamp, Role? role) = await SessionAsync(actorId);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);

        if (role is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, role.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<(string Stamp, Role? Role)> SessionAsync(Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstOrDefaultAsync(Ct);

        return found is null ? ("no-such-user", null) : (found.SecurityStamp, found.Role);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("PF-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("PF-PA"), "مشروع أ", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        Project projectB = Project.Create(
            UniqueNames.Code("PF-PB"), "مشروع ب", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        User owner = User.Create(
            UniqueNames.Code("pf-owner"), "pf-owner", UniqueNames.Phone(), Role.Owner, Now, WellKnownDepartments.FinanceId).Value;

        User financeOnProjectA = User.Create(
            UniqueNames.Code("pf-fin-a"), "pf-fin-a", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User financeUnassigned = User.Create(
            UniqueNames.Code("pf-fin-un"), "pf-fin-un", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User siteEngineerOnProjectA = User.Create(
            UniqueNames.Code("pf-eng-a"), "pf-eng-a", UniqueNames.Phone(), Role.SiteEngineer, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;

        User hr = User.Create(
            UniqueNames.Code("pf-hr"), "pf-hr", UniqueNames.Phone(), Role.Hr, Now, WellKnownDepartments.HrId).Value;

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(owner, financeOnProjectA, financeUnassigned, siteEngineerOnProjectA, hr);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, financeOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);
        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, siteEngineerOnProjectA, AssignmentLevel.Junior, Guid.CreateVersion7(), Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _owner = owner.Id;
        _financeOnProjectA = financeOnProjectA.Id;
        _financeUnassigned = financeUnassigned.Id;
        _siteEngineerOnProjectA = siteEngineerOnProjectA.Id;
        _hr = hr.Id;
    }
}
