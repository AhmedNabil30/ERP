using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Features.Closure;

/// <summary>
/// KAFF-806 over HTTP. A subcontractor retention is its own account on its own clock. Three things are asserted at every
/// stage: the stage (a release the day before the warranty ends is refused), the amount (summed from postings, never
/// typed) and the hold (a separate ledger that does not move).
/// </summary>
/// <remarks>
/// Nothing accrues subcontractor retention yet (KAFF-510 is not built), so each test writes the accrual postings straight
/// to the ledger, three of them, the way the subcontractor extract chain eventually will.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SubcontractorRetentionEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _finance;
    private Guid _financeUnassigned;
    private Guid _technicalOffice;
    private Guid _hr;

    public SubcontractorRetentionEndpointTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Client client = Client.Create(UniqueNames.Code("RT-C"), "عميل الاحتجاز", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = Make("rt-owner", Role.Owner);
        User finance = Make("rt-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User financeUnassigned = Make("rt-fin-un", Role.Finance, WellKnownDepartments.FinanceId);
        User office = Make("rt-to", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = Make("rt-hr", Role.Hr, WellKnownDepartments.HrId);
        context.Clients.Add(client);
        context.Users.AddRange(owner, finance, financeUnassigned, office, hr);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _financeUnassigned = financeUnassigned.Id;
        _technicalOffice = office.Id;
        _hr = hr.Id;

        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public void The_retention_release_route_requires_its_permission_and_the_route_project()
    {
        const string route = "/api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release";
        var found = new List<PermissionRequirement>();
        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (RouteEndpoint endpoint in source.Endpoints.OfType<RouteEndpoint>().Where(e => e.RoutePattern.RawText == route))
            {
                foreach (IAuthorizeData data in endpoint.Metadata.OfType<IAuthorizeData>())
                {
                    if (PermissionRequirement.TryParse(data.Policy ?? string.Empty, out PermissionRequirement? requirement) && requirement is not null)
                    {
                        found.Add(requirement);
                    }
                }
            }
        }

        PermissionRequirement gate = found.Should().ContainSingle().Subject;
        gate.Permission.Should().Be(Permission.SubcontractorRetentionRelease);
        gate.Scope.Should().Be(ProjectScope.FromRoute());
    }

    [Fact]
    public async Task Only_assigned_finance_reaches_the_release_and_a_refused_caller_moves_nothing()
    {
        (Guid project, Guid sub) = await NewProjectAsync(handoverOn: new DateOnly(2025, 5, 1));
        long postings = await PostingCountAsync(project);

        foreach (Guid refused in new[] { _owner, _technicalOffice, _hr, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Post, Route(project, sub), refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await PostingCountAsync(project)).Should().Be(postings);
        (await RetentionAsync(project, sub)).Should().Be(37_500m);
    }

    /// <summary>The stage, asserted: the day before the warranty ends nothing is released and nothing is posted.</summary>
    [Fact]
    public async Task The_release_is_refused_before_the_warranty_ends_and_posts_nothing()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Handover four months less one day ago: the warranty ends tomorrow.
        (Guid project, Guid sub) = await NewProjectAsync(handoverOn: today.AddMonths(-4).AddDays(1));
        long postings = await PostingCountAsync(project);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Route(project, sub), _finance);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("closure.warranty_not_ended");
        (await PostingCountAsync(project)).Should().Be(postings);
        (await RetentionAsync(project, sub)).Should().Be(37_500m);
        (await ReleaseCountAsync(project)).Should().Be(0);
    }

    [Fact]
    public async Task The_release_equals_the_summed_retention_in_one_posting_and_leaves_the_hold_and_the_balance_right()
    {
        (Guid project, Guid sub) = await NewProjectAsync(handoverOn: new DateOnly(2025, 5, 1));
        decimal hold = await BalanceAsync(project, AccountType.Hold);
        hold.Should().Be(200_000m);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Route(project, sub), _finance);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using (JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)))
        {
            decimal.Parse(body.RootElement.GetProperty("released").GetString()!, CultureInfo.InvariantCulture).Should().Be(37_500m);
        }

        (await ReleaseCountAsync(project)).Should().Be(1);
        (await RetentionAsync(project, sub)).Should().Be(0m, "the retention balance reads zero");
        (await BalanceAsync(project, AccountType.Hold)).Should().Be(hold, "the client hold is a different ledger and does not move");

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting release = await reader.Postings.SingleAsync(
            p => p.ProjectId == project && p.Type == PostingType.SubcontractorRetentionRelease, Ct);
        release.Amount.Should().Be(new Money(37_500m));
        release.ReversesId.Should().BeNull();
        release.CreatedByUserId.Should().Be(_finance);
        release.FromAccountId.Should().Be(await AccountIdAsync(project, AccountType.SubcontractorPayable, sub));
        release.ToAccountId.Should().Be(await AccountIdAsync(project, AccountType.SubcontractorRetention, sub));
        (await reader.AuditRecords.AnyAsync(record => record.EntityId == release.Id, Ct)).Should().BeTrue("who and when are audited");

        (await SendAsync(HttpMethod.Post, Route(project, sub), _finance)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a second release finds a zero balance");
        (await ReleaseCountAsync(project)).Should().Be(1);
    }

    [Fact]
    public async Task A_subcontractor_with_nothing_retained_posts_no_release()
    {
        (Guid project, Guid sub) = await NewProjectAsync(handoverOn: new DateOnly(2025, 5, 1), accruals: []);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Route(project, sub), _finance);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("closure.nothing_to_release");
        (await ReleaseCountAsync(project)).Should().Be(0);
    }

    [Fact]
    public async Task A_subcontractor_without_retention_accounts_on_the_project_is_refused()
    {
        (Guid project, Guid _) = await NewProjectAsync(handoverOn: new DateOnly(2025, 5, 1));

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Route(project, Guid.NewGuid()), _finance);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("closure.accounts_missing");
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.HandoverPending)]
    [InlineData(ProjectStatus.Handover)]
    public async Task The_ruled_project_state_table_refuses_the_release_outside_the_warranty(ProjectStatus status)
    {
        (Guid project, Guid sub) = await NewProjectAsync(handoverOn: new DateOnly(2025, 5, 1), finalStatus: status);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Route(project, sub), _finance);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, status.ToString());
        (await ErrorCodeAsync(response)).Should().Be("treasury.posting_not_allowed_in_project_state");
        (await ReleaseCountAsync(project)).Should().Be(0);
        (await RetentionAsync(project, sub)).Should().Be(37_500m);
    }

    // ---- helpers ----

    private static string Route(Guid project, Guid subcontractor) =>
        $"/api/projects/{project}/subcontractors/{subcontractor}/retention-release";

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private async Task<long> PostingCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project, Ct);
    }

    private async Task<long> ReleaseCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(
            posting => posting.ProjectId == project && posting.Type == PostingType.SubcontractorRetentionRelease, Ct);
    }

    private async Task<Guid> AccountIdAsync(Guid project, AccountType type, Guid subcontractor)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Accounts
            .Where(account => account.ProjectId == project && account.Type == type && account.PartyId == subcontractor)
            .Select(account => account.Id)
            .SingleAsync(Ct);
    }

    private async Task<decimal> RetentionAsync(Guid project, Guid subcontractor)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return (await reader.AccountBalances.SingleAsync(
            balance => balance.ProjectId == project && balance.Type == AccountType.SubcontractorRetention && balance.PartyId == subcontractor, Ct))
            .SignedBalance.Amount;
    }

    private async Task<decimal> BalanceAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return (await reader.AccountBalances.SingleAsync(balance => balance.ProjectId == project && balance.Type == type, Ct)).SignedBalance.Amount;
    }

    /// <summary>
    /// A Lump Sum project taken to <paramref name="finalStatus"/> with handover on <paramref name="handoverOn"/>, a hold of
    /// 200,000 accrued while Active, and one subcontractor whose retention account holds the sum of three accruals
    /// (10,000, 12,500 and 15,000 = 37,500). Finance is assigned.
    /// </summary>
    private async Task<(Guid Project, Guid Subcontractor)> NewProjectAsync(
        DateOnly handoverOn, ProjectStatus finalStatus = ProjectStatus.UnderWarranty, decimal[]? accruals = null)
    {
        await using KaffDbContext context = _database.CreateContext();
        Subcontractor subcontractor = Subcontractor.Create(UniqueNames.Code("RT-SUB"), "مقاول احتجاز", UniqueNames.Phone(), Now).Value;
        context.Subcontractors.Add(subcontractor);

        Project project = Project.Create(UniqueNames.Code("RT-P"), UniqueNames.Code("مشروع احتجاز"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2025, 1, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        User finance = await context.Users.SingleAsync(user => user.Id == _finance, Ct);
        context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, finance, AssignmentLevel.Standard, _owner, Now).Value);

        List<Account> accounts = [.. ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), new DateOnly(2025, 1, 1)).Value];
        Account retention = Account.Create(
            AccountType.SubcontractorRetention, UniqueNames.Code("RT-RET"), "احتجاز", "Retention", Currency.Egp, new DateOnly(2025, 1, 1),
            project.Id, PartyType.Subcontractor, subcontractor.Id).Value;
        Account payable = Account.Create(
            AccountType.SubcontractorPayable, UniqueNames.Code("RT-PAY"), "مستحق", "Payable", Currency.Egp, new DateOnly(2025, 1, 1),
            project.Id, PartyType.Subcontractor, subcontractor.Id).Value;
        context.Accounts.AddRange(accounts);
        context.Accounts.AddRange(retention, payable);
        await context.SaveChangesAsync(Ct);

        project.Status.Should().Be(ProjectStatus.Active, "D-230: signing activates the project");
        await context.SaveChangesAsync(Ct);

        Account receivable = accounts.Single(account => account.Type == AccountType.ClientReceivable);
        Account hold = accounts.Single(account => account.Type == AccountType.Hold);
        context.Postings.Add(Posting.Create(
            receivable, hold, new Money(200_000m), PostingType.HoldAccrual,
            new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT"),
            new DateOnly(2025, 2, 1), _owner, Now, project.Id).Value);
        foreach (decimal part in accruals ?? [10_000m, 12_500m, 15_000m])
        {
            context.Postings.Add(Posting.Create(
                retention, payable, new Money(part), PostingType.SubcontractorRetentionAccrual,
                new SourceDocument(SourceDocumentType.SubcontractorExtract, Guid.CreateVersion7(), "SX"),
                new DateOnly(2025, 2, 1), _owner, Now, project.Id).Value);
        }

        await context.SaveChangesAsync(Ct);

        ProjectStatus[] path = finalStatus switch
        {
            ProjectStatus.Active => [],
            ProjectStatus.HandoverPending => [ProjectStatus.HandoverPending],
            ProjectStatus.Handover => [ProjectStatus.HandoverPending, ProjectStatus.Handover],
            _ => [ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty],
        };
        foreach (ProjectStatus step in path)
        {
            project.TransitionTo(step, step == ProjectStatus.Handover ? handoverOn : new DateOnly(2025, 4, 1), "test")
                .IsSuccess.Should().BeTrue($"{step}");
        }

        await context.SaveChangesAsync(Ct);
        return (project.Id, subcontractor.Id);
    }
}
