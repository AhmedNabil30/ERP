using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// KAFF-309r over HTTP. The تشوينات read: what was issued, what an extract recovered, what is outstanding, summed from
/// postings, and who may see it (the supervising engineer at the QC step, never the junior).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MaterialAdvanceReadTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 11, 20);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _finance;
    private Guid _junior;
    private Guid _supervisor;
    private Guid _hr;

    public MaterialAdvanceReadTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Client client = Client.Create(UniqueNames.Code("MA-C"), "عميل تشوينات", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = Make("ma-owner", Role.Owner);
        User finance = Make("ma-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User junior = Make("ma-jun", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("ma-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = Make("ma-hr", Role.Hr, WellKnownDepartments.HrId);
        context.Clients.Add(client);
        context.Users.AddRange(owner, finance, junior, supervisor, hr);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _junior = junior.Id;
        _supervisor = supervisor.Id;
        _hr = hr.Id;

        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task The_outstanding_is_issued_less_recovered_summed_from_postings()
    {
        Guid project = await NewProjectAsync(issued: 100_000m, recovered: 45_000m);

        JsonElement read = await ReadAsync(project, _supervisor);

        Parse(read, "issued").Should().Be(100_000m);
        Parse(read, "recovered").Should().Be(45_000m);
        Parse(read, "outstanding").Should().Be(55_000m);
        read.GetProperty("reconciled").GetBoolean().Should().BeFalse();
        read.GetProperty("openExtracts").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task A_project_whose_issue_is_fully_recovered_reads_reconciled()
    {
        Guid project = await NewProjectAsync(issued: 75_000m, recovered: 75_000m);

        JsonElement read = await ReadAsync(project, _finance);

        Parse(read, "outstanding").Should().Be(0m);
        read.GetProperty("reconciled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_junior_hr_and_an_unassigned_owner_style_caller_are_refused()
    {
        Guid project = await NewProjectAsync(issued: 10_000m, recovered: 0m);

        foreach (Guid refused in new[] { _junior, _hr })
        {
            (await SendAsync(project, refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(project, _owner)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers ----

    private static decimal Parse(JsonElement read, string name) =>
        decimal.Parse(read.GetProperty(name).GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private async Task<HttpResponseMessage> SendAsync(Guid project, Guid actorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/projects/{project}/material-advance", UriKind.Relative));

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

    private async Task<JsonElement> ReadAsync(Guid project, Guid actorId)
    {
        HttpResponseMessage response = await SendAsync(project, actorId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// An Active project with owner, finance, junior and supervisor assigned, one تشوينات issue, one recovery,.
    /// </summary>
    private async Task<Guid> NewProjectAsync(decimal issued, decimal recovered)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("MA-P"), UniqueNames.Code("مشروع تشوينات"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2025, 1, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        foreach ((Guid id, AssignmentLevel level) in new[]
        {
            (_owner, AssignmentLevel.Standard), (_finance, AssignmentLevel.Standard),
            (_junior, AssignmentLevel.Junior), (_supervisor, AssignmentLevel.Supervisor),
        })
        {
            User user = await context.Users.SingleAsync(found => found.Id == id, Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, level, _owner, Now).Value);
        }

        List<Account> accounts = [.. ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), new DateOnly(2025, 1, 1)).Value];
        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync(Ct);

        Account material = accounts.Single(account => account.Type == AccountType.MaterialAdvance);
        Account receivable = accounts.Single(account => account.Type == AccountType.ClientReceivable);
        var source = new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT");

        Posting issue = Posting.Create(material, receivable, new Money(issued), PostingType.MaterialAdvanceIssue, source, Day, _owner, Now, project.Id).Value;
        context.Postings.Add(issue);
        if (recovered > 0m)
        {
            Posting recovery = Posting.Create(receivable, material, new Money(recovered), PostingType.MaterialAdvanceRecovery, source, Day, _owner, Now, project.Id).Value;
            context.Postings.Add(recovery);
        }

        await context.SaveChangesAsync(Ct);
        return project.Id;
    }
}
