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
/// <b>Not run by this session — the Scrum Master runs Api.Tests.</b> Q90 is answered — decisions.md
/// D-173 — and <c>PermissionCatalogue.cs</c> carries the grants: <c>TreasuryReadProject</c> is Owner,
/// Finance, Site Engineer (project-scoped, assignment required); <c>TreasuryReadCompany</c> is Owner
/// and Finance only. AC-306-C/D (derivation, no netted field) are proven directly against
/// <c>Kaff.Domain.Treasury.BalanceQueries</c> already, by KAFF-304's own <c>BalanceQueriesTests</c> —
/// not re-proven here. This file covers AC-306-E — the permission×assignment matrix over HTTP — and
/// the wire-format/no-Total shape rules.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class GetBalancesTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _owner;
    private Guid _financeOnProjectA;
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

    // ---- AC-306-E — D-173: Owner, Finance, assigned Site Engineer read TreasuryReadProject; ------
    // ---- Owner and Finance only read TreasuryReadCompany; HR reaches neither -----------------------

    [Fact]
    public async Task A_site_engineer_assigned_to_project_A_reads_project_As_own_balances()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/projects/{_projectA}/treasury/balances", UriKind.Relative), _siteEngineerOnProjectA);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, "D-173: an assigned Site Engineer holds TreasuryReadProject on their own project");
    }

    [Fact]
    public async Task The_same_site_engineer_is_refused_project_Bs_balances()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/projects/{_projectB}/treasury/balances", UriKind.Relative), _siteEngineerOnProjectA);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "D-173: TreasuryReadProject is project-scoped — role alone is insufficient without an assignment to project B");
    }

    [Fact]
    public async Task The_owner_reads_both_projects_ledgers()
    {
        HttpResponseMessage responseA = await GetAsync(
            new Uri($"/api/projects/{_projectA}/treasury/balances", UriKind.Relative), _owner);
        HttpResponseMessage responseB = await GetAsync(
            new Uri($"/api/projects/{_projectB}/treasury/balances", UriKind.Relative), _owner);

        responseA.StatusCode.Should().Be(HttpStatusCode.OK, "D-173: Owner holds TreasuryReadProject, globally scoped");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK, "D-173: Owner holds TreasuryReadProject, globally scoped");
    }

    [Fact]
    public async Task Finance_assigned_to_project_A_reads_project_As_ledgers_but_not_project_Bs()
    {
        HttpResponseMessage responseA = await GetAsync(
            new Uri($"/api/projects/{_projectA}/treasury/balances", UriKind.Relative), _financeOnProjectA);
        HttpResponseMessage responseB = await GetAsync(
            new Uri($"/api/projects/{_projectB}/treasury/balances", UriKind.Relative), _financeOnProjectA);

        responseA.StatusCode.Should().Be(HttpStatusCode.OK, "D-173: Finance holds TreasuryReadProject");
        responseB.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "Finance is project-scoped too — not assigned to project B");
    }

    [Fact]
    public async Task Hr_is_refused_project_As_ledgers_with_no_financial_field_in_the_payload()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/projects/{_projectA}/treasury/balances", UriKind.Relative), _hr);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "D-051 Q32: HR is on neither treasury grant list");

        string body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().NotContain("clientAdvance", "HR must never see a financial field, even refused");
    }

    [Fact]
    public async Task The_owner_and_finance_read_the_company_account_balance()
    {
        HttpResponseMessage ownerResponse = await GetAsync(
            new Uri($"/api/treasury/accounts/{_accountId}/balance", UriKind.Relative), _owner);
        HttpResponseMessage financeResponse = await GetAsync(
            new Uri($"/api/treasury/accounts/{_accountId}/balance", UriKind.Relative), _financeOnProjectA);

        ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK, "D-173: Owner holds TreasuryReadCompany");
        financeResponse.StatusCode.Should().Be(
            HttpStatusCode.OK, "D-173: Finance holds TreasuryReadCompany — company-wide, no assignment needed");
    }

    [Fact]
    public async Task A_site_engineer_is_refused_the_company_account_balance()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/treasury/accounts/{_accountId}/balance", UriKind.Relative), _siteEngineerOnProjectA);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "D-173: TreasuryReadCompany is Owner and Finance only, no Site Engineer row");
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

        User owner = User.Create(
            UniqueNames.Code("gb-owner"), "gb-owner", UniqueNames.Phone(), Role.Owner, Now, WellKnownDepartments.FinanceId).Value;

        User financeOnProjectA = User.Create(
            UniqueNames.Code("gb-fin-a"), "gb-fin-a", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User siteEngineerOnProjectA = User.Create(
            UniqueNames.Code("gb-eng-a"), "gb-eng-a", UniqueNames.Phone(), Role.SiteEngineer, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;

        User hr = User.Create(
            UniqueNames.Code("gb-hr"), "gb-hr", UniqueNames.Phone(), Role.Hr, Now, WellKnownDepartments.HrId).Value;

        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("GB-SAFE"), "خزينة", "Safe", Currency.Egp, Today).Value;

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(owner, financeOnProjectA, siteEngineerOnProjectA, hr);
        context.Accounts.Add(safe);

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
        _siteEngineerOnProjectA = siteEngineerOnProjectA.Id;
        _hr = hr.Id;
        _accountId = safe.Id;
    }
}
