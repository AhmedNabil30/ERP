using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
/// KAFF-306 — <c>GET /api/treasury/postings</c> and
/// <c>GET /api/projects/{projectId}/treasury/postings</c>.
/// </summary>
/// <remarks>
/// <b>Not run by this session — the Scrum Master runs Api.Tests.</b> Written and left for that gate.
/// <para>
/// <b>Scope note.</b> Both routes are gated on <see cref="Permission.TreasuryReadProject"/> /
/// <see cref="Permission.TreasuryReadCompany"/>, which questions-for-karim.md Q90 leaves UNRESOLVED
/// with an empty grant list (see <c>PermissionCatalogue.cs</c>). <c>PermissionEvaluator</c> refuses a
/// permission with no matching grant to everyone, Owner included, so no role can reach either route
/// today. The scenario tests this story's own AC-306-B/C/D require (running balance across a
/// reversal, no netted field) are therefore proven at the Domain layer instead —
/// <c>tests/Domain.Tests/PostingStatementQueriesTests.cs</c> — and only the permission-blocked
/// behaviour and the wire-format rule are exercised here. Once Q90 is answered and a grant is added,
/// this file is where the HTTP-level scenario cases belong (same shape as <c>PostMovementTests</c>).
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ListPostingsTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _owner;
    private Guid _financeOnProjectA;
    private Guid _hr;
    private Guid _accountId;

    public ListPostingsTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-306-F · every money field is a JSON string, no float/double in the contract ----------

    [Fact]
    public void No_response_member_is_a_float_or_double_and_every_Money_member_is_present()
    {
        foreach (Type contract in new[]
                 {
                     typeof(Kaff.Api.Features.Treasury.ListPostings.PostingRow),
                     typeof(Kaff.Api.Features.Treasury.ListPostings.Response),
                 })
        {
            foreach (PropertyInfo property in contract.GetProperties())
            {
                property.PropertyType.Should().NotBe<float>(
                    $"{contract.Name}.{property.Name} must never be a float — D-044 §6");
                property.PropertyType.Should().NotBe<double>(
                    $"{contract.Name}.{property.Name} must never be a double — D-044 §6");
            }
        }

        // D-135: Money crosses the wire as a JSON string. KaffJson's Money converter is what makes
        // this true regardless of the property type below; this pins that the statement row still
        // uses Money and not a bare decimal, which is the property CLAUDE.md's own rule is about.
        typeof(Kaff.Api.Features.Treasury.ListPostings.PostingRow)
            .GetProperties()
            .Where(property => property.Name is nameof(Kaff.Api.Features.Treasury.ListPostings.PostingRow.Inflow)
                or nameof(Kaff.Api.Features.Treasury.ListPostings.PostingRow.Outflow)
                or nameof(Kaff.Api.Features.Treasury.ListPostings.PostingRow.RunningBalance))
            .Should().OnlyContain(property => property.PropertyType == typeof(Kaff.Domain.Common.Money));
    }

    // ---- Q90 — nobody may reach either route until it is answered ---------------------------------

    [Fact]
    public async Task An_owner_is_refused_the_company_statement_because_TreasuryReadCompany_holds_no_grant_yet()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/treasury/postings?accountId={_accountId}", UriKind.Relative), _owner);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "questions-for-karim.md Q90 is unanswered — TreasuryReadCompany's grant list is empty, so " +
            "PermissionEvaluator refuses even the Owner (RoleNotGranted)");
    }

    [Fact]
    public async Task A_finance_user_assigned_to_the_project_is_refused_the_project_statement_for_the_same_reason()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/projects/{_projectA}/treasury/postings?accountId={_accountId}", UriKind.Relative),
            _financeOnProjectA);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TreasuryReadProject's grant list is empty — Q90");
    }

    [Fact]
    public async Task Hr_is_refused_the_company_statement_too_with_no_financial_field_in_any_payload()
    {
        HttpResponseMessage response = await GetAsync(
            new Uri($"/api/treasury/postings?accountId={_accountId}", UriKind.Relative), _hr);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        string body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().NotContain(
            "runningBalance", "AC-306-E: HR must never see a financial field, and a 403 body carries none");
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
            UniqueNames.Code("LP-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("LP-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;

        User owner = User.Create(
            UniqueNames.Code("lp-owner"), "lp-owner", UniqueNames.Phone(), Role.Owner, Now, WellKnownDepartments.FinanceId).Value;

        User financeOnProjectA = User.Create(
            UniqueNames.Code("lp-fin-a"), "lp-fin-a", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User hr = User.Create(
            UniqueNames.Code("lp-hr"), "lp-hr", UniqueNames.Phone(), Role.Hr, Now, WellKnownDepartments.HrId).Value;

        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("LP-SAFE"), "خزينة", "Safe", Currency.Egp, Today).Value;

        context.Clients.Add(client);
        context.Projects.Add(projectA);
        context.Users.AddRange(owner, financeOnProjectA, hr);
        context.Accounts.Add(safe);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, financeOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _owner = owner.Id;
        _financeOnProjectA = financeOnProjectA.Id;
        _hr = hr.Id;
        _accountId = safe.Id;
    }
}
