using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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

namespace Kaff.Api.Tests.Features.Treasury;

/// <summary>
/// The posting-type × project-state table, enforced on the real endpoint: Finance could post to a
/// Stopped, Closed or Terminated project before it existed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PostingProjectStateTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _finance;

    public PostingProjectStateTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("PS-C"), "عميل", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User finance = User.Create(
            UniqueNames.Code("ps-fin"), "ps-fin", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        context.Clients.Add(client);
        context.Users.Add(finance);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _finance = finance.Id;

        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task A_hold_accrual_on_a_stopped_project_is_refused_and_posts_nothing()
    {
        (Guid project, Account receivable, Account hold) = await NewProjectAsync(ProjectStatus.Stopped);

        HttpResponseMessage response = await PostAsync(project, receivable, hold, PostingType.HoldAccrual);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.posting_not_allowed_in_project_state");
        (await PostingCountAsync(project)).Should().Be(0);
    }

    [Fact]
    public async Task The_same_hold_accrual_on_an_active_project_is_accepted()
    {
        (Guid project, Account receivable, Account hold) = await NewProjectAsync(ProjectStatus.Active);

        HttpResponseMessage response = await PostAsync(project, receivable, hold, PostingType.HoldAccrual);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostingCountAsync(project)).Should().Be(1);
    }

    [Fact]
    public async Task A_hold_release_before_handover_is_refused_by_the_state_table()
    {
        (Guid project, Account receivable, Account hold) = await NewProjectAsync(ProjectStatus.Active);

        // Release is the only type that may leave the hold; the project is not handed over yet.
        HttpResponseMessage response = await PostAsync(project, hold, receivable, PostingType.HoldRelease);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.posting_not_allowed_in_project_state");
    }

    [Fact]
    public async Task A_posting_through_a_project_the_caller_is_not_staffed_on_is_still_refused_at_the_gate()
    {
        (Guid _, Account receivable, Account hold) = await NewProjectAsync(ProjectStatus.Active);

        // The state table is an addition to the role-and-assignment check, never a replacement for it.
        HttpResponseMessage response = await PostAsync(Guid.NewGuid(), receivable, hold, PostingType.HoldAccrual);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    private async Task<(Guid Project, Account Receivable, Account Hold)> NewProjectAsync(ProjectStatus status)
    {
        await using KaffDbContext context = _database.CreateContext();

        Project project = Project.Create(UniqueNames.Code("PS-P"), UniqueNames.Code("مشروع"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();

        if (status != ProjectStatus.Setup)
        {
            project.TransitionTo(ProjectStatus.Active, Today).IsSuccess.Should().BeTrue();
        }

        if (status == ProjectStatus.Stopped)
        {
            project.TransitionTo(ProjectStatus.Stopped, Today, "stopped by the site authority").IsSuccess.Should().BeTrue();
        }

        IReadOnlyList<Account> accounts = ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), Today).Value;

        context.Projects.Add(project);
        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync(Ct);

        User financeUser = await context.Users.SingleAsync(user => user.Id == _finance, Ct);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(project.Id, financeUser, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);
        await context.SaveChangesAsync(Ct);

        return (
            project.Id,
            accounts.Single(account => account.Type == AccountType.ClientReceivable),
            accounts.Single(account => account.Type == AccountType.Hold));
    }

    private async Task<int> PostingCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.CountAsync(posting => posting.ProjectId == project, Ct);
    }

    private async Task<HttpResponseMessage> PostAsync(Guid project, Account from, Account to, PostingType type)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/projects/{project}/treasury/postings", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = from.Id,
                toAccountId = to.Id,
                amount = "1000.0000",
                type = type.ToString(),
                postingDate = Today,
                sourceDocumentType = nameof(SourceDocumentType.Extract),
                sourceDocumentId = Guid.CreateVersion7(),
                sourceDocumentReference = (string?)null,
            }),
        };

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == _finance)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, _finance.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
