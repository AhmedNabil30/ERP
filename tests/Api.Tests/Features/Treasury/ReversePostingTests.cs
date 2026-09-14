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
/// KAFF-303 — <c>POST /api/treasury/postings/{id}/reverse</c> and
/// <c>POST /api/projects/{projectId}/treasury/postings/{id}/reverse</c>.
/// </summary>
/// <remarks>
/// One test per <c>AC-303-A</c>…<c>F</c>. Every test goes through the HTTP endpoint against a real
/// PostgreSQL, same pattern as <c>PostMovementTests</c> — the database guards themselves are already
/// covered end to end by <c>TreasuryGuardTests</c>.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ReversePostingTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _financeOnProjectA;
    private Guid _financeCompanyWide;
    private Guid _financeUnassigned;

    public ReversePostingTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-303-A · reversing a posting creates a mirror with accounts swapped --------------------

    [Fact]
    public async Task AC_303_A_Reversing_a_posting_creates_a_mirror_with_accounts_swapped()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);
        Guid originalId = await CreatePostingAsync(safe.Id, expense.Id, 1_500m, PostingType.CompanyExpensePayment);

        HttpResponseMessage response = await ReverseCompanyAsync(_financeCompanyWide, originalId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement root = body.RootElement;

        root.GetProperty("fromAccountId").GetGuid().Should().Be(expense.Id, "accounts are swapped");
        root.GetProperty("toAccountId").GetGuid().Should().Be(safe.Id, "accounts are swapped");
        root.GetProperty("type").GetString().Should().Be(nameof(PostingType.CompanyExpensePayment));
        root.GetProperty("reversesId").GetGuid().Should().Be(originalId);
        AmountOf(root).Should().Be(1_500m, "a reversal is a full mirror — same amount, rule 2");

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting stored = await reader.Postings.SingleAsync(posting => posting.ReversesId == originalId, Ct);

        stored.FromAccountId.Should().Be(expense.Id);
        stored.ToAccountId.Should().Be(safe.Id);
        stored.Amount.Should().Be(new Money(1_500m));
        stored.Type.Should().Be(PostingType.CompanyExpensePayment);
        stored.IsReversal.Should().BeTrue();
    }

    // ---- AC-303-B · a posting already reversed cannot be reversed again ----------------------------

    [Fact]
    public async Task AC_303_B_A_posting_already_reversed_cannot_be_reversed_again()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);
        Guid originalId = await CreatePostingAsync(safe.Id, expense.Id, 1_000m, PostingType.CompanyExpensePayment);

        (await ReverseCompanyAsync(_financeCompanyWide, originalId)).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage second = await ReverseCompanyAsync(_financeCompanyWide, originalId);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(second)).Should().Be("errors.treasury.posting_already_reversed");

        (await ReversalCountAsync(originalId)).Should().Be(1, "the second attempt wrote no row");
    }

    // ---- AC-303-C · a reversal cannot itself be reversed --------------------------------------------

    [Fact]
    public async Task AC_303_C_A_reversal_cannot_itself_be_reversed()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);
        Guid originalId = await CreatePostingAsync(safe.Id, expense.Id, 1_000m, PostingType.CompanyExpensePayment);

        HttpResponseMessage firstReversal = await ReverseCompanyAsync(_financeCompanyWide, originalId);
        firstReversal.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid reversalId = await IdOfAsync(firstReversal);

        HttpResponseMessage secondReversal = await ReverseCompanyAsync(_financeCompanyWide, reversalId);

        secondReversal.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(secondReversal)).Should().Be("errors.treasury.reversal_of_reversal");

        (await ReversalCountAsync(reversalId)).Should().Be(0, "a reversal of a reversal wrote no row");
    }

    // ---- AC-303-D · the hold ledger accepts a reversal that debits it -------------------------------

    [Fact]
    public async Task AC_303_D_The_hold_ledger_accepts_a_reversal_that_debits_it()
    {
        Guid clientId = Guid.CreateVersion7();
        Account receivable = await AddAccountAsync(AccountType.ClientReceivable, _projectA, PartyType.Client, clientId);
        Account hold = await AddAccountAsync(AccountType.Hold, _projectA, PartyType.Client, clientId);

        HttpResponseMessage accrual = await PostProjectAsync(
            _financeOnProjectA, _projectA, receivable.Id, hold.Id, 200_000m, PostingType.HoldAccrual);
        accrual.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid accrualId = await IdOfAsync(accrual);

        // A normal debit off Hold is refused outright (rule 5, covered by PostMovementTests
        // TC_3_027). The reversal of the very accrual that made the mistake must succeed, even though
        // it moves value out of Hold before handover.
        HttpResponseMessage reversal = await ReverseProjectAsync(_financeOnProjectA, _projectA, accrualId);

        reversal.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "a reversal corrects an accrual that should never have existed — it is not a withdrawal, rule 5");

        using JsonDocument body = JsonDocument.Parse(await reversal.Content.ReadAsStringAsync(Ct));
        JsonElement root = body.RootElement;
        root.GetProperty("fromAccountId").GetGuid().Should().Be(hold.Id);
        root.GetProperty("toAccountId").GetGuid().Should().Be(receivable.Id);
    }

    // ---- AC-303-E · a reversal dated into a closed period is refused --------------------------------

    [Fact]
    public async Task AC_303_E_A_reversal_dated_into_a_closed_period_is_refused()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);
        Guid originalId = await CreatePostingAsync(safe.Id, expense.Id, 500m, PostingType.CompanyExpensePayment);

        var period = AccountingPeriod.Create(2027, 6).Value;
        period.Close(Guid.CreateVersion7(), Now).IsSuccess.Should().BeTrue();

        await using (KaffDbContext setup = _database.CreateContext())
        {
            setup.AccountingPeriods.Add(period);
            await setup.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await ReverseCompanyAsync(
            _financeCompanyWide, originalId, postingDate: new DateOnly(2027, 6, 15));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.closed_period");

        (await ReversalCountAsync(originalId)).Should().Be(0, "the back-dated attempt wrote nothing");
    }

    // ---- AC-303-F · role without project assignment cannot reverse a project-tagged posting --------

    [Fact]
    public async Task AC_303_F_Role_without_project_assignment_cannot_reverse_a_project_tagged_posting()
    {
        Account revenueA = await AddAccountAsync(AccountType.ContractRevenue, _projectA, null, null);
        Account costA = await AddAccountAsync(AccountType.ProjectCost, _projectA, null, null);

        HttpResponseMessage created = await PostProjectAsync(
            _financeOnProjectA, _projectA, revenueA.Id, costA.Id, 1_000m, PostingType.Adjustment);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid originalId = await IdOfAsync(created);

        HttpResponseMessage response = await ReverseProjectAsync(_financeUnassigned, _projectA, originalId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(response)).Should().Be(
            "errors.auth.forbidden", "role alone is not enough — CLAUDE.md: \"role and assignment... server-side, always\"");

        (await ReversalCountAsync(originalId)).Should().Be(
            0, "the refusal must land before the domain or the database ever sees the request");
    }

    /// <summary>
    /// A company-wide grant does not let a caller reverse a project-tagged posting by using the
    /// company route instead of the project one — <c>Posting.Reverse</c> takes no <c>projectId</c> of
    /// its own, so the handler is the only place this scope mismatch is caught.
    /// </summary>
    [Fact]
    public async Task Company_wide_grant_cannot_reverse_a_project_tagged_posting_via_the_company_route()
    {
        Account revenueA = await AddAccountAsync(AccountType.ContractRevenue, _projectA, null, null);
        Account costA = await AddAccountAsync(AccountType.ProjectCost, _projectA, null, null);

        HttpResponseMessage created = await PostProjectAsync(
            _financeOnProjectA, _projectA, revenueA.Id, costA.Id, 1_000m, PostingType.Adjustment);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid originalId = await IdOfAsync(created);

        HttpResponseMessage response = await ReverseCompanyAsync(_financeCompanyWide, originalId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.reversal_target_not_found");

        (await ReversalCountAsync(originalId)).Should().Be(0);
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static decimal AmountOf(JsonElement root)
    {
        JsonElement amount = root.GetProperty("amount");

        return amount.ValueKind == JsonValueKind.String
            ? decimal.Parse(amount.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : amount.GetDecimal();
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<(Account Safe, Account Expense)> CreateFundedSafeAndExpenseAsync(decimal fundAmount)
    {
        Account safe = await AddAccountAsync(AccountType.Safe, null, null, null);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense, null, null, null);
        Account capital = await AddAccountAsync(AccountType.PaidInCapital, null, null, null);

        await using KaffDbContext context = _database.CreateContext();

        Posting funding = Posting.Create(
            capital,
            safe,
            new Money(fundAmount),
            PostingType.OpeningBalance,
            new SourceDocument(SourceDocumentType.OpeningBalance, Guid.CreateVersion7(), null),
            Today,
            Guid.CreateVersion7(),
            Now).Value;

        context.Postings.Add(funding);
        await context.SaveChangesAsync(Ct);

        return (safe, expense);
    }

    private async Task<Guid> CreatePostingAsync(Guid fromAccountId, Guid toAccountId, decimal amount, PostingType type)
    {
        HttpResponseMessage response = await PostCompanyAsync(_financeCompanyWide, fromAccountId, toAccountId, amount, type);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await IdOfAsync(response);
    }

    private async Task<Account> AddAccountAsync(
        AccountType type, Guid? projectId, PartyType? partyType, Guid? partyId)
    {
        Result<Account> created = Account.Create(
            type,
            UniqueNames.Code($"RP{(int)type}"),
            "حساب اختبار",
            "Test account",
            Currency.Egp,
            new DateOnly(2026, 1, 1),
            projectId,
            partyType,
            partyType is null ? null : (partyId ?? Guid.CreateVersion7()));

        created.IsSuccess.Should().BeTrue();

        await using KaffDbContext context = _database.CreateContext();
        context.Accounts.Add(created.Value);
        await context.SaveChangesAsync(Ct);

        return created.Value;
    }

    private async Task<int> ReversalCountAsync(Guid originalId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.CountAsync(posting => posting.ReversesId == originalId, Ct);
    }

    private Task<HttpResponseMessage> PostCompanyAsync(
        Guid actorId, Guid fromAccountId, Guid toAccountId, decimal amount, PostingType type)
        => SendPostingAsync(
            new Uri("/api/treasury/postings", UriKind.Relative), actorId, fromAccountId, toAccountId, amount, type);

    private Task<HttpResponseMessage> PostProjectAsync(
        Guid actorId, Guid projectId, Guid fromAccountId, Guid toAccountId, decimal amount, PostingType type)
        => SendPostingAsync(
            new Uri($"/api/projects/{projectId}/treasury/postings", UriKind.Relative),
            actorId, fromAccountId, toAccountId, amount, type);

    private async Task<HttpResponseMessage> SendPostingAsync(
        Uri route, Guid actorId, Guid fromAccountId, Guid toAccountId, decimal amount, PostingType type)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new
            {
                fromAccountId,
                toAccountId,
                amount,
                type = type.ToString(),
                postingDate = Today,
                sourceDocumentType = nameof(SourceDocumentType.Adjustment),
                sourceDocumentId = Guid.CreateVersion7(),
                sourceDocumentReference = (string?)null,
            }),
        };

        return await AuthorizedSendAsync(request, actorId);
    }

    private Task<HttpResponseMessage> ReverseCompanyAsync(Guid actorId, Guid postingId, DateOnly? postingDate = null)
        => SendReverseAsync(new Uri($"/api/treasury/postings/{postingId}/reverse", UriKind.Relative), actorId, postingDate);

    private Task<HttpResponseMessage> ReverseProjectAsync(
        Guid actorId, Guid projectId, Guid postingId, DateOnly? postingDate = null)
        => SendReverseAsync(
            new Uri($"/api/projects/{projectId}/treasury/postings/{postingId}/reverse", UriKind.Relative),
            actorId,
            postingDate);

    private async Task<HttpResponseMessage> SendReverseAsync(Uri route, Guid actorId, DateOnly? postingDate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new { postingDate = postingDate ?? Today }),
        };

        return await AuthorizedSendAsync(request, actorId);
    }

    private async Task<HttpResponseMessage> AuthorizedSendAsync(HttpRequestMessage request, Guid actorId)
    {
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
            UniqueNames.Code("RP-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("RP-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;

        User financeOnProjectA = User.Create(
            UniqueNames.Code("rp-fin-a"), "rp-fin-a", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User financeCompanyWide = User.Create(
            UniqueNames.Code("rp-fin-co"), "rp-fin-co", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User financeUnassigned = User.Create(
            UniqueNames.Code("rp-fin-un"), "rp-fin-un", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        context.Clients.Add(client);
        context.Projects.Add(projectA);
        context.Users.AddRange(financeOnProjectA, financeCompanyWide, financeUnassigned);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, financeOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _financeOnProjectA = financeOnProjectA.Id;
        _financeCompanyWide = financeCompanyWide.Id;
        _financeUnassigned = financeUnassigned.Id;
    }
}
