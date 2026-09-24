using System.Net;
using System.Net.Http;
using System.Reflection;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Treasury;

/// <summary>
/// KAFF-306 — <c>GET /api/projects/{projectId}/treasury/balances</c> and
/// <c>GET /api/treasury/accounts/{accountId}/balance</c>.
/// </summary>
/// <remarks>
/// <b>Not run by this session — the Scrum Master runs Api.Tests.</b> Same Q90 scope note as
/// <c>ListPostingsTests</c>: both routes are gated on permissions with an empty grant list today, so
/// nobody can reach them over HTTP. AC-306-C/D (derivation, no netted field) are proven directly
/// against <c>Kaff.Domain.Treasury.BalanceQueries</c> already, by KAFF-304's own
/// <c>BalanceQueriesTests</c> — not re-proven here. This file covers the permission-blocked state and
/// the wire-format/no-Total shape rules only.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class GetBalancesTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _siteEngineerOnProjectA;
    private Guid _hr;
    private Guid _accountId;

    public GetBalancesTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-306-D · no field anywhere sums or nets two of the five ledgers ------------------------

    [Fact]
    public void The_ledger_response_carries_no_field_whose_name_suggests_a_total()
    {
        typeof(Kaff.Api.Features.Treasury.GetBalances.LedgerBalancesResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(
                name => name.Contains("Total", StringComparison.OrdinalIgnoreCase),
                "CLAUDE.md: the five ledgers never net against each other, and the response must not " +
                "contain a field that invites it");
    }

    [Fact]
    public void No_response_member_is_a_float_or_double()
    {
        foreach (Type contract in new[]
                 {
                     typeof(Kaff.Api.Features.Treasury.GetBalances.LedgerBalancesResponse),
                     typeof(Kaff.Api.Features.Treasury.GetBalances.AccountBalanceResponse),
                 })
        {
            foreach (PropertyInfo property in contract.GetProperties())
            {
                property.PropertyType.Should().NotBe<float>($"{contract.Name}.{property.Name} — D-044 §6");
                property.PropertyType.Should().NotBe<double>($"{contract.Name}.{property.Name} — D-044 §6");
            }
        }
    }

    // ---- Q90 — nobody may reach either route until it is answered; AC-306-E's project-mismatch ----
    // ---- half and the HR half both collapse into the same refusal meanwhile -----------------------

    [Fact]
    public async Task A_site_engineer_assigned_to_project_A_is_refused_project_Bs_balances()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/projects/{_projectB}/treasury/balances", UriKind.Relative), _siteEngineerOnProjectA);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "not assigned to project B — and TreasuryReadProject's grant list is empty regardless (Q90)");
    }

    [Fact]
    public async Task A_site_engineer_assigned_to_project_A_is_refused_project_As_own_balances_too_pending_Q90()
    {
        // This is the half AC-306-E cannot yet demonstrate as a POSITIVE grant: with an empty grant
        // list, project A is refused exactly like project B, because nobody holds the permission yet.
        // Kept as its own case so the day Q90 is answered and a grant is added, this one flips to
        // Forbidden/Ok and the change is visible in a single failing assertion rather than a silent gap.
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/projects/{_projectA}/treasury/balances", UriKind.Relative), _siteEngineerOnProjectA);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TreasuryReadProject holds no grant yet — Q90");
    }

    [Fact]
    public async Task Hr_is_refused_the_account_balance_with_no_financial_field_in_the_payload()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/treasury/accounts/{_accountId}/balance", UriKind.Relative), _hr);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        string body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().NotContain("signedBalance", "HR must never see a financial field, even refused");
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private async Task<HttpResponseMessage> GetAsync(Uri route, Guid actorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

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
            UniqueNames.Code("GB-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("GB-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;

        Project projectB = Project.Create(
            UniqueNames.Code("GB-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;

        User siteEngineerOnProjectA = User.Create(
            UniqueNames.Code("gb-eng-a"), "gb-eng-a", UniqueNames.Phone(), Role.SiteEngineer, Now, WellKnownDepartments.OperationsId).Value;

        User hr = User.Create(
            UniqueNames.Code("gb-hr"), "gb-hr", UniqueNames.Phone(), Role.Hr, Now, WellKnownDepartments.HrId).Value;

        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("GB-SAFE"), "خزينة", "Safe", Currency.Egp, Today).Value;

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(siteEngineerOnProjectA, hr);
        context.Accounts.Add(safe);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, siteEngineerOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _siteEngineerOnProjectA = siteEngineerOnProjectA.Id;
        _hr = hr.Id;
        _accountId = safe.Id;
    }
}
