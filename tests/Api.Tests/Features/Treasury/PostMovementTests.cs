using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
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
/// KAFF-301 — <c>POST /api/treasury/postings</c> and
/// <c>POST /api/projects/{projectId}/treasury/postings</c>.
/// </summary>
/// <remarks>
/// One test per <c>qa/slice-3/test-cases.md</c> <c>TC-3-023</c>…<c>TC-3-031</c> (<c>AC-301-A</c>…<c>I</c>).
/// <c>TC-3-032</c>/<c>AC-301-J</c> stays HELD, cross-cutting <c>KAFF-319</c> — not cased here.
/// Every test goes through the HTTP endpoint against a real PostgreSQL, per the story's own
/// evidence table; the database guards themselves are already covered end to end by
/// <c>TreasuryGuardTests</c>.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class PostMovementTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _financeOnProjectA;
    private Guid _financeCompanyWide;
    private Guid _financeUnassigned;

    public PostMovementTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-301-A · a valid posting is created and is retrievable by its id --------------------

    [Fact]
    public async Task TC_3_023_A_valid_posting_is_created_and_retrievable()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);

        HttpResponseMessage response = await PostCompanyAsync(
            _financeCompanyWide, safe.Id, expense.Id, 1_500m, PostingType.CompanyExpensePayment);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement root = body.RootElement;

        Guid id = root.GetProperty("id").GetGuid();
        id.Should().NotBe(Guid.Empty);
        root.GetProperty("fromAccountId").GetGuid().Should().Be(safe.Id);
        root.GetProperty("toAccountId").GetGuid().Should().Be(expense.Id);
        root.GetProperty("type").GetString().Should().Be(nameof(PostingType.CompanyExpensePayment));
        root.GetProperty("isReversal").GetBoolean().Should().BeFalse();
        AmountOf(root).Should().Be(1_500m);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting stored = await reader.Postings.SingleAsync(posting => posting.Id == id, Ct);

        stored.FromAccountId.Should().Be(safe.Id);
        stored.ToAccountId.Should().Be(expense.Id);
        stored.Amount.Should().Be(new Money(1_500m));
        stored.IsReversal.Should().BeFalse("this endpoint only ever creates originals — KAFF-303 reverses");
    }

    // ---- AC-301-B · four decimals round-trip, no float/double in the contract -------------------

    [Fact]
    public async Task TC_3_024_The_amount_round_trips_at_four_decimal_places_with_no_floating_point_type()
    {
        foreach (Type contract in new[] { typeof(Kaff.Api.Features.Treasury.PostMovement.Request), typeof(Kaff.Api.Features.Treasury.PostMovement.Response) })
        {
            foreach (PropertyInfo property in contract.GetProperties())
            {
                property.PropertyType.Should().NotBe<float>(
                    $"{contract.Name}.{property.Name} must never be a float — D-044 §6");
                property.PropertyType.Should().NotBe<double>(
                    $"{contract.Name}.{property.Name} must never be a double — D-044 §6");
            }
        }

        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);

        HttpResponseMessage response = await PostCompanyAsync(
            _financeCompanyWide, safe.Id, expense.Id, 1234.5678m, PostingType.CompanyExpensePayment);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        AmountOf(body.RootElement).Should().Be(
            1234.5678m, "the stored value must round-trip exactly, never rounded through a double");
    }

    // ---- AC-301-C · zero or negative amount is refused, no row is written ------------------------

    [Fact]
    public async Task TC_3_025_A_zero_or_negative_amount_is_refused_and_no_row_is_written()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);

        foreach (decimal amount in new[] { 0m, -100m })
        {
            HttpResponseMessage response = await PostCompanyAsync(
                _financeCompanyWide, safe.Id, expense.Id, amount, PostingType.CompanyExpensePayment);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.treasury.amount_must_be_positive");
        }

        (await PostingCountAsync(safe.Id, expense.Id)).Should().Be(
            0, "neither refused attempt wrote a row — the funding posting runs capital-to-safe, not safe-to-expense");
    }

    // ---- AC-301-D · netting two of the five ledgers is refused -----------------------------------

    [Fact]
    public async Task TC_3_026_A_posting_that_would_net_two_ledgers_is_refused()
    {
        Guid clientId = Guid.CreateVersion7();
        Account clientAdvance = await AddAccountAsync(
            AccountType.ClientAdvance, _projectA, PartyType.Client, clientId);
        Account hold = await AddAccountAsync(AccountType.Hold, _projectA, PartyType.Client, clientId);

        HttpResponseMessage response = await PostProjectAsync(
            _financeOnProjectA, _projectA, clientAdvance.Id, hold.Id, 10_000m, PostingType.Adjustment);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.ledgers_must_not_net");

        (await PostingCountAsync(clientAdvance.Id, hold.Id)).Should().Be(0);
    }

    // ---- AC-301-E · nothing but HoldRelease moves value out of Hold before handover ---------------

    [Fact]
    public async Task TC_3_027_A_non_hold_release_posting_cannot_move_value_out_of_hold_before_handover()
    {
        Guid clientId = Guid.CreateVersion7();
        Account receivable = await AddAccountAsync(
            AccountType.ClientReceivable, _projectA, PartyType.Client, clientId);
        Account hold = await AddAccountAsync(AccountType.Hold, _projectA, PartyType.Client, clientId);

        // Accrue into the hold first, so it has a balance a debit could otherwise draw down.
        (await PostProjectAsync(
                _financeOnProjectA, _projectA, receivable.Id, hold.Id, 200_000m, PostingType.HoldAccrual))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await PostProjectAsync(
            _financeOnProjectA, _projectA, hold.Id, receivable.Id, 5_000m, PostingType.DebitNote);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be(
            "errors.treasury.hold_only_grows",
            "CLAUDE.md: \"If you write code that debits the hold ledger before handover, you have misread the spec.\"");

        (await PostingCountAsync(hold.Id, receivable.Id)).Should().Be(0);
    }

    // ---- AC-301-F · a payment breaching the Safe's floor is refused, not clamped -----------------

    [Fact]
    public async Task TC_3_028_A_payment_that_would_breach_the_safes_floor_is_refused()
    {
        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(100m);

        HttpResponseMessage response = await PostCompanyAsync(
            _financeCompanyWide, safe.Id, expense.Id, 5_000m, PostingType.CompanyExpensePayment);

        response.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "a breach must be refused outright, not silently clamped to zero or left negative — D-044 §8");
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.negative_balance");

        (await PostingCountAsync(safe.Id, expense.Id)).Should().Be(
            0, "the refused payment wrote nothing — the funding posting runs capital-to-safe, not safe-to-expense");
    }

    // ---- AC-301-G · a project-tag mismatch is refused ---------------------------------------------

    [Fact]
    public async Task TC_3_029_A_posting_whose_project_tag_mismatches_its_accounts_is_refused()
    {
        Guid clientId = Guid.CreateVersion7();
        Account revenueA = await AddAccountAsync(AccountType.ContractRevenue, _projectA, null, null);
        Account costA = await AddAccountAsync(AccountType.ProjectCost, _projectA, null, null);

        // Both accounts belong to project A; the route names project B.
        HttpResponseMessage mismatched = await PostProjectAsync(
            _financeOnProjectA, _projectB, revenueA.Id, costA.Id, 1_000m, PostingType.Adjustment);

        mismatched.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(mismatched)).Should().Be("errors.treasury.project_tag_required");

        // Both accounts are project-scoped; the company route carries no project at all.
        HttpResponseMessage untagged = await PostCompanyAsync(
            _financeCompanyWide, revenueA.Id, costA.Id, 1_000m, PostingType.Adjustment);

        untagged.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(untagged)).Should().Be("errors.treasury.project_tag_required");

        // Both accounts are company-level; the route names a real project.
        Account safe = await AddAccountAsync(AccountType.Safe, null, null, null);
        Account companyExpense = await AddAccountAsync(AccountType.CompanyExpense, null, null, null);

        HttpResponseMessage forbidden = await PostProjectAsync(
            _financeOnProjectA, _projectA, safe.Id, companyExpense.Id, 1_000m, PostingType.CompanyExpensePayment);

        forbidden.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(forbidden)).Should().Be("errors.treasury.project_tag_forbidden");

        _ = clientId;
        (await PostingCountAsync(revenueA.Id, costA.Id)).Should().Be(0);
        (await PostingCountAsync(safe.Id, companyExpense.Id)).Should().Be(0);
    }

    // ---- AC-301-H · a posting dated inside a closed period is refused -----------------------------

    [Fact]
    public async Task TC_3_030_A_posting_dated_inside_a_closed_period_is_refused()
    {
        var period = AccountingPeriod.Create(2027, 3).Value;
        period.Close(Guid.CreateVersion7(), Now).IsSuccess.Should().BeTrue();

        await using (KaffDbContext setup = _database.CreateContext())
        {
            setup.AccountingPeriods.Add(period);
            await setup.SaveChangesAsync(Ct);
        }

        (Account safe, Account expense) = await CreateFundedSafeAndExpenseAsync(10_000m);

        HttpResponseMessage response = await PostCompanyAsync(
            _financeCompanyWide,
            safe.Id,
            expense.Id,
            10m,
            PostingType.CompanyExpensePayment,
            postingDate: new DateOnly(2027, 3, 15));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.closed_period");

        (await PostingCountAsync(safe.Id, expense.Id)).Should().Be(
            0, "the back-dated attempt wrote nothing — the funding posting runs capital-to-safe, not safe-to-expense");
    }

    // ---- AC-301-I · role without project assignment is refused before any domain check runs -------

    [Fact]
    public async Task TC_3_031_Role_without_project_assignment_is_refused_with_403_before_any_domain_check_runs()
    {
        Account revenueA = await AddAccountAsync(AccountType.ContractRevenue, _projectA, null, null);
        Account costA = await AddAccountAsync(AccountType.ProjectCost, _projectA, null, null);

        HttpResponseMessage response = await PostProjectAsync(
            _financeUnassigned, _projectA, revenueA.Id, costA.Id, 1_000m, PostingType.Adjustment);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(response)).Should().Be(
            "errors.auth.forbidden", "role alone is not enough — CLAUDE.md: \"role and assignment... server-side, always\"");

        (await PostingCountAsync(revenueA.Id, costA.Id)).Should().Be(
            0, "the refusal must land before the domain or the database ever sees the request");
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static decimal AmountOf(JsonElement root)
    {
        JsonElement amount = root.GetProperty("amount");

        // D-135: money is written on the wire as a string. A test that called GetDecimal() here
        // would pass today and stop meaning anything the day a caller sends a JSON number instead.
        return amount.ValueKind == JsonValueKind.String
            ? decimal.Parse(amount.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : amount.GetDecimal();
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

    private async Task<Account> AddAccountAsync(
        AccountType type, Guid? projectId, PartyType? partyType, Guid? partyId)
    {
        Result<Account> created = Account.Create(
            type,
            UniqueNames.Code($"T{(int)type}"),
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

    private async Task<int> PostingCountAsync(Guid fromAccountId, Guid toAccountId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Postings.CountAsync(
            posting => posting.FromAccountId == fromAccountId && posting.ToAccountId == toAccountId, Ct);
    }

    private Task<HttpResponseMessage> PostCompanyAsync(
        Guid actorId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        PostingType type,
        DateOnly? postingDate = null)
        => SendAsync(
            new Uri("/api/treasury/postings", UriKind.Relative),
            actorId,
            fromAccountId,
            toAccountId,
            amount,
            type,
            postingDate);

    private Task<HttpResponseMessage> PostProjectAsync(
        Guid actorId,
        Guid projectId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        PostingType type,
        DateOnly? postingDate = null)
        => SendAsync(
            new Uri($"/api/projects/{projectId}/treasury/postings", UriKind.Relative),
            actorId,
            fromAccountId,
            toAccountId,
            amount,
            type,
            postingDate);

    private async Task<HttpResponseMessage> SendAsync(
        Uri route,
        Guid actorId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        PostingType type,
        DateOnly? postingDate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new
            {
                fromAccountId,
                toAccountId,
                amount,
                type = type.ToString(),
                postingDate = postingDate ?? Today,
                sourceDocumentType = nameof(SourceDocumentType.Adjustment),
                sourceDocumentId = Guid.CreateVersion7(),
                sourceDocumentReference = (string?)null,
            }),
        };

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
            UniqueNames.Code("PM-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("PM-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;

        Project projectB = Project.Create(
            UniqueNames.Code("PM-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;

        User financeOnProjectA = User.Create(
            UniqueNames.Code("pm-fin-a"), "pm-fin-a", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User financeCompanyWide = User.Create(
            UniqueNames.Code("pm-fin-co"), "pm-fin-co", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        User financeUnassigned = User.Create(
            UniqueNames.Code("pm-fin-un"), "pm-fin-un", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(financeOnProjectA, financeCompanyWide, financeUnassigned);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectA.Id, financeOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);

        // TC-3-029's mismatch scenario posts through project B's route with project A's accounts —
        // that has to reach the domain's project-tag check rather than be turned away earlier by the
        // assignment gate, so the same actor is also staffed on project B.
        context.ProjectAssignments.Add(ProjectAssignment.Create(
            projectB.Id, financeOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _financeOnProjectA = financeOnProjectA.Id;
        _financeCompanyWide = financeCompanyWide.Id;
        _financeUnassigned = financeUnassigned.Id;
    }
}
