using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Accounting;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Accounting;

/// <summary>
/// Release 4 accounting layer over HTTP: the asset register and depreciation (KAFF-700), accruals and prepayments (KAFF-701), the
/// trial balance and its export (KAFF-704), the expense and overhead report (KAFF-707, KAFF-708) and the budget baseline (KAFF-709).
/// Behaviour from spec.md sections 6.2, 6.6, 6.8 and 6.10. Permission cases hit the endpoints directly.
/// </summary>
/// <remarks>
/// The database is shared across the whole run, so every account and project here is new, and the reports are read through date
/// ranges (2012) no other test posts into.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class AccountingEndpointTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _projectA;
    private Guid _owner;
    private Guid _financeCompanyWide;
    private Guid _financeOnProjectA;
    private Guid _hr;
    private Guid _technicalOffice;
    private Guid _babId;

    public AccountingEndpointTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- KAFF-701 · accruals ---------------------------------------------------------------------------

    [Fact]
    public async Task AC_701_A_a_company_accrual_waits_for_the_owner_then_posts_an_expense_accrual_and_shows_in_the_open_list()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        HttpResponseMessage asked = await SendAsync(
            HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "5000", accruedOn = "2026-05-20", description = "Electricity" });

        asked.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement waiting = await JsonAsync(asked);
        waiting.GetProperty("status").GetString().Should().Be("Requested");
        Guid requestId = waiting.GetProperty("requestId").GetGuid();

        // Nothing posts before the Owner approves.
        (await PostingCountAsync(accrued.Id)).Should().Be(0);

        JsonElement approved = await ApproveAsync(requestId);
        Guid accrualId = approved.GetProperty("accrualId").GetGuid();
        Guid postingId = approved.GetProperty("postingIds")[0].GetGuid();

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Posting posting = await reader.Postings.SingleAsync(candidate => candidate.Id == postingId, Ct);
            posting.Type.Should().Be(PostingType.ExpenseAccrual);
            posting.FromAccountId.Should().Be(accrued.Id);
            posting.ToAccountId.Should().Be(expense.Id);
            posting.Amount.Amount.Should().Be(5_000m);
            posting.ProjectId.Should().BeNull();
            posting.CreatedByUserId.Should().Be(_financeCompanyWide, "the postings carry the requester; the Owner is the approver");
        }

        JsonElement open = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accruals?open=true", _financeCompanyWide));
        open.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).Should().Contain(accrualId);
    }

    [Fact]
    public async Task AC_701_D_the_expense_account_must_match_the_tag_company_or_project_never_both()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account company = await AddAccountAsync(AccountType.CompanyExpense);
        Account projectCost = await AddAccountAsync(AccountType.ProjectCost, _projectA);

        object ProjectCostBody() => new { accruedExpenseAccountId = accrued.Id, expenseAccountId = projectCost.Id, amount = "100", accruedOn = "2026-05-20" };
        object CompanyBody() => new { accruedExpenseAccountId = accrued.Id, expenseAccountId = company.Id, amount = "100", accruedOn = "2026-05-20" };

        HttpResponseMessage companyTagWithProjectCost = await SendAsync(HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide, ProjectCostBody());
        companyTagWithProjectCost.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(companyTagWithProjectCost)).Should().Be("errors.accounting.account_wrong_type");

        HttpResponseMessage projectTagWithCompanyExpense = await SendAsync(
            HttpMethod.Post, $"/api/projects/{_projectA}/treasury/accruals", _financeOnProjectA, CompanyBody());
        projectTagWithCompanyExpense.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage projectTagged = await SendAsync(
            HttpMethod.Post, $"/api/projects/{_projectA}/treasury/accruals", _financeOnProjectA, ProjectCostBody());
        projectTagged.StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement approved = await ApproveAsync((await JsonAsync(projectTagged)).GetProperty("requestId").GetGuid());
        Guid postingId = approved.GetProperty("postingIds")[0].GetGuid();
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Postings.SingleAsync(candidate => candidate.Id == postingId, Ct)).ProjectId.Should().Be(_projectA);
    }

    [Fact]
    public async Task AC_701_C_releasing_an_accrual_posts_the_mirror_pair_on_approval_once_and_clears_it_from_the_open_list()
    {
        (Guid accrualId, Account accrued, Account expense) = await CreateAccrualAsync();

        HttpResponseMessage asked = await SendAsync(
            HttpMethod.Post, $"/api/treasury/accruals/{accrualId}/release", _financeCompanyWide, new { releasedOn = "2026-05-25" });
        asked.StatusCode.Should().Be(HttpStatusCode.Created);

        (await PostingCountAsync(accrued.Id)).Should().Be(1, "only the accrual itself: the release waits for the Owner");

        JsonElement approved = await ApproveAsync((await JsonAsync(asked)).GetProperty("requestId").GetGuid());
        Guid postingId = approved.GetProperty("postingIds")[0].GetGuid();

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Posting release = await reader.Postings.SingleAsync(candidate => candidate.Id == postingId, Ct);
            release.Type.Should().Be(PostingType.AccrualRelease);
            release.FromAccountId.Should().Be(expense.Id);
            release.ToAccountId.Should().Be(accrued.Id);
        }

        JsonElement open = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accruals?open=true", _financeCompanyWide));
        open.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).Should().NotContain(accrualId);

        HttpResponseMessage again = await SendAsync(
            HttpMethod.Post, $"/api/treasury/accruals/{accrualId}/release", _financeCompanyWide, new { releasedOn = "2026-05-26" });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(again)).Should().Be("errors.accounting.accrual_already_released");
    }

    [Fact]
    public async Task A_project_accrual_is_reached_only_through_its_own_project_route()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account projectCost = await AddAccountAsync(AccountType.ProjectCost, _projectA);

        JsonElement approved = await AskAndApproveAsync(
            $"/api/projects/{_projectA}/treasury/accruals", _financeOnProjectA,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = projectCost.Id, amount = "100", accruedOn = "2026-05-20" });
        Guid accrualId = approved.GetProperty("accrualId").GetGuid();

        (await SendAsync(HttpMethod.Post, $"/api/treasury/accruals/{accrualId}/release", _financeCompanyWide, new { releasedOn = "2026-05-25" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage release = await SendAsync(
            HttpMethod.Post, $"/api/projects/{_projectA}/treasury/accruals/{accrualId}/release", _financeOnProjectA, new { releasedOn = "2026-05-25" });
        release.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ApproveAsync((await JsonAsync(release)).GetProperty("requestId").GetGuid())).GetProperty("postingIds").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task An_accrual_dated_in_a_closed_month_is_refused()
    {
        await EnsureClosedPeriodAsync(2012, 3);
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "10", accruedOn = "2012-03-15" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.closed_period");
    }

    // ---- KAFF-701 · prepayments ------------------------------------------------------------------------

    [Fact]
    public async Task AC_701_B_a_prepayment_moves_to_expense_month_by_month_on_approval_and_ends_at_exactly_zero()
    {
        (Guid scheduleId, Account prepaid, Account expense) = await CreatePrepaymentAsync(10_000m, 3, "2026-03-01");

        foreach (string month in new[] { "2026-03-10", "2026-04-10", "2026-05-10" })
        {
            await AskAndApproveAsync("/api/treasury/prepayment-amortisations", _financeCompanyWide, new { month });
        }

        JsonElement listed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/prepayments", _financeCompanyWide));
        JsonElement row = listed.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == scheduleId);
        Dec(row.GetProperty("amortised")).Should().Be(10_000m);
        Dec(row.GetProperty("remaining")).Should().Be(0m);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> postings = await reader.Postings
            .Where(posting => posting.SourceDocumentId == scheduleId && posting.Type == PostingType.PrepaymentAmortisation)
            .OrderBy(posting => posting.PostingDate)
            .ToListAsync(Ct);

        postings.Select(posting => posting.Amount.Amount).Should().Equal(3_333.3333m, 3_333.3334m, 3_333.3333m);
        postings.Should().OnlyContain(posting => posting.FromAccountId == prepaid.Id && posting.ToAccountId == expense.Id);
    }

    [Fact]
    public async Task Approving_a_prepayment_month_twice_writes_nothing_the_second_time()
    {
        (Guid scheduleId, _, _) = await CreatePrepaymentAsync(1_200m, 12, "2026-01-01");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            await AskAndApproveAsync("/api/treasury/prepayment-amortisations", _financeCompanyWide, new { month = "2026-01-15" });
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Postings.CountAsync(posting => posting.SourceDocumentId == scheduleId && posting.Type == PostingType.PrepaymentAmortisation, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task A_prepayment_schedule_names_a_standing_payment_of_the_same_amount()
    {
        Account prepaid = await AddAccountAsync(AccountType.Prepayment);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        Guid funding = await FundAsync(prepaid, 12_000m);

        HttpResponseMessage mismatch = await SendAsync(
            HttpMethod.Post, "/api/treasury/prepayments", _financeCompanyWide,
            new { prepaymentAccountId = prepaid.Id, expenseAccountId = expense.Id, fundingPostingId = funding, totalAmount = "11000", months = 12, firstMonth = "2026-01-01" });

        mismatch.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(mismatch)).Should().Be("errors.accounting.prepayment_funding_mismatch");
    }

    // ---- KAFF-700 · asset register and depreciation ------------------------------------------------------

    [Fact]
    public async Task AC_700_A_a_computer_of_12000_shows_50_percent_and_depreciates_500_a_month_on_approval_with_book_value()
    {
        (Account accumulated, Account expense) = await DepreciationAccountsAsync();

        HttpResponseMessage created = await RegisterAssetAsync(accumulated, expense, "ComputersSoftwareData", 12_000m, "2026-05-10");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement asset = await JsonAsync(created);
        Dec(asset.GetProperty("ratePercent")).Should().Be(50m);
        Guid assetId = asset.GetProperty("id").GetGuid();

        HttpResponseMessage asked = await SendAsync(HttpMethod.Post, "/api/treasury/depreciation-runs", _financeCompanyWide, new { month = "2026-05-01" });
        asked.StatusCode.Should().Be(HttpStatusCode.Created);

        await using (KaffDbContext before = _database.CreateBareContext())
        {
            (await before.Postings.CountAsync(posting => posting.SourceDocumentId == assetId, Ct)).Should().Be(0, "nothing posts before the Owner approves");
        }

        await ApproveAsync((await JsonAsync(asked)).GetProperty("requestId").GetGuid());

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Posting posting = await reader.Postings.SingleAsync(candidate => candidate.SourceDocumentId == assetId, Ct);
            posting.Type.Should().Be(PostingType.Depreciation);
            posting.FromAccountId.Should().Be(accumulated.Id);
            posting.ToAccountId.Should().Be(expense.Id);
            posting.Amount.Amount.Should().Be(500m);
            posting.PostingDate.Year.Should().Be(2026);
            posting.PostingDate.Month.Should().Be(5);
        }

        JsonElement listed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/assets", _financeCompanyWide));
        JsonElement row = listed.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == assetId);
        Dec(row.GetProperty("accumulated")).Should().Be(500m);
        Dec(row.GetProperty("bookValue")).Should().Be(11_500m);
        Dec(row.GetProperty("monthlyDepreciation")).Should().Be(500m, "12,000 at 50 percent a year, derived on read");
    }

    [Fact]
    public async Task AC_700_E_approving_a_second_depreciation_run_for_one_month_writes_nothing()
    {
        (Account accumulated, Account expense) = await DepreciationAccountsAsync();
        Guid assetId = (await JsonAsync(await RegisterAssetAsync(accumulated, expense, "OtherBusinessAssets", 2_400m, "2026-04-02"))).GetProperty("id").GetGuid();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            await AskAndApproveAsync("/api/treasury/depreciation-runs", _financeCompanyWide, new { month = "2026-04-15" });
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Postings.CountAsync(posting => posting.SourceDocumentId == assetId, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task F_R4_2_a_second_waiting_request_for_the_same_month_is_refused_and_the_database_agrees()
    {
        (Account accumulated, Account expense) = await DepreciationAccountsAsync();
        await RegisterAssetAsync(accumulated, expense, "OtherBusinessAssets", 2_400m, "2026-04-02");
        object body = new { month = "2026-05-10" };

        HttpResponseMessage first = await SendAsync(HttpMethod.Post, "/api/treasury/depreciation-runs", _financeCompanyWide, body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        HttpResponseMessage second = await SendAsync(HttpMethod.Post, "/api/treasury/depreciation-runs", _financeCompanyWide, body);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(second)).Should().Be("errors.accounting.request_already_waiting");

        await using (KaffDbContext writer = _database.CreateContext())
        {
            writer.AccountingRequests.Add(AccountingRequest.Create(
                AccountingRequestKind.Depreciation, null, "{}", "depreciate:2026-05", _financeCompanyWide, Now).Value);
            Func<Task> save = () => writer.SaveChangesAsync(Ct);
            await save.Should().ThrowAsync<DbUpdateException>("the partial unique index holds even if the application check is raced");
        }

        await ApproveAsync((await JsonAsync(first)).GetProperty("requestId").GetGuid());
        HttpResponseMessage again = await SendAsync(HttpMethod.Post, "/api/treasury/depreciation-runs", _financeCompanyWide, body);
        again.StatusCode.Should().Be(HttpStatusCode.Created, "once decided, the month is no longer waiting");
        await ApproveAsync((await JsonAsync(again)).GetProperty("requestId").GetGuid()); // leave nothing waiting for the other tests
    }

    [Fact]
    public async Task The_request_list_shows_the_amount_the_owner_will_approve()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        Guid requestId = (await JsonAsync(await SendAsync(HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "70", accruedOn = "2026-05-20" }))).GetProperty("requestId").GetGuid();

        JsonElement listed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accounting-requests?status=Requested", _financeCompanyWide));
        JsonElement row = listed.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == requestId);
        row.GetProperty("amount").GetString().Should().Be("70.0000");
        row.GetProperty("lineCount").GetInt32().Should().Be(1);

        await ApproveAsync(requestId);
    }

    [Theory]
    [InlineData("amount = amount + 1")]
    [InlineData("amount = NULL")]
    public async Task Approval_is_refused_when_the_total_moved_since_the_request_or_was_never_stored(string change)
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        Guid requestId = (await JsonAsync(await SendAsync(HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "70", accruedOn = "2026-05-20" }))).GetProperty("requestId").GetGuid();

        await using (KaffDbContext writer = _database.CreateContext())
        {
#pragma warning disable EF1002 // The fragment is a constant from the InlineData above, not user input.
            await writer.Database.ExecuteSqlRawAsync($"UPDATE accounting_requests SET {change} WHERE id = '{requestId}'", Ct);
#pragma warning restore EF1002
        }

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/approval", _owner);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(refused)).Should().Be("errors.accounting.request_amount_changed");
        (await PostingCountAsync(accrued.Id)).Should().Be(0, "nothing posts when the amount changed");

        await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/rejection", _owner, new { reason = "Asked again" });
    }

    [Fact]
    public async Task A_junior_site_engineer_assigned_to_the_project_is_refused_its_accruals()
    {
        Guid project = await NewSignedLumpSumProjectAsync();
        Guid junior;
        await using (KaffDbContext context = _database.CreateContext())
        {
            User engineer = User.Create(
                UniqueNames.Code("ac-jr"), "ac-jr", UniqueNames.Phone(), Role.SiteEngineer, Now,
                WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
            context.Users.Add(engineer);
            await context.SaveChangesAsync(Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project, engineer, AssignmentLevel.Junior, Guid.CreateVersion7(), Now).Value);
            await context.SaveChangesAsync(Ct);
            junior = engineer.Id;
        }

        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/treasury/accruals", junior)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "D-218 item 6");
        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/treasury/accounting-requests", junior)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AC_700_D_an_asset_dated_inside_a_closed_month_is_refused()
    {
        await EnsureClosedPeriodAsync(2012, 3);
        (Account accumulated, Account expense) = await DepreciationAccountsAsync();

        HttpResponseMessage response = await RegisterAssetAsync(accumulated, expense, "ComputersSoftwareData", 1_000m, "2012-03-10");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.treasury.closed_period");
    }

    [Fact]
    public async Task The_rate_may_change_per_asset_until_depreciation_is_posted()
    {
        (Account accumulated, Account expense) = await DepreciationAccountsAsync();
        Guid assetId = (await JsonAsync(await RegisterAssetAsync(accumulated, expense, "Buildings", 60_000m, "2026-02-01"))).GetProperty("id").GetGuid();

        HttpResponseMessage changed = await SendAsync(HttpMethod.Post, $"/api/treasury/assets/{assetId}/rate", _financeCompanyWide, new { ratePercent = 8 });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        Dec((await JsonAsync(changed)).GetProperty("ratePercent")).Should().Be(8m);

        await AskAndApproveAsync("/api/treasury/depreciation-runs", _financeCompanyWide, new { month = "2026-02-01" });

        HttpResponseMessage locked = await SendAsync(HttpMethod.Post, $"/api/treasury/assets/{assetId}/rate", _financeCompanyWide, new { ratePercent = 9 });
        locked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(locked)).Should().Be("errors.accounting.asset_rate_locked");
    }

    [Fact]
    public async Task A_depreciation_month_that_has_not_started_is_refused()
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, "/api/treasury/depreciation-runs", _financeCompanyWide, new { month = "2099-01-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.accounting.month_in_future");
    }

    // ---- permissions: role and assignment, hitting the endpoints directly --------------------------------

    [Fact]
    public async Task AC_700_F_owner_hr_and_technical_office_cannot_write_accounting_entries()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        (Account accumulated, Account depreciation) = await DepreciationAccountsAsync();

        foreach (Guid actor in new[] { _owner, _hr, _technicalOffice })
        {
            (await SendAsync(HttpMethod.Post, "/api/treasury/accruals", actor,
                new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "10", accruedOn = "2026-05-20" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await SendAsync(HttpMethod.Post, "/api/treasury/prepayments", actor, new { months = 12 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, "/api/treasury/prepayment-amortisations", actor, new { month = "2026-05-01" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, "/api/treasury/depreciation-runs", actor, new { month = "2026-05-01" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await RegisterAssetAsync(accumulated, depreciation, "ComputersSoftwareData", 1_000m, "2026-05-10", actor)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/treasury/assets/{Guid.CreateVersion7()}/rate", actor, new { ratePercent = 10 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/treasury/accruals/{Guid.CreateVersion7()}/release", actor, new { releasedOn = "2026-05-20" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Finance_without_an_assignment_to_the_project_is_refused_on_every_project_route()
    {
        Guid projectB = await NewProjectAsync(assignFinance: false);
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account projectCost = await AddAccountAsync(AccountType.ProjectCost, projectB);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{projectB}/treasury/accruals", _financeOnProjectA,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = projectCost.Id, amount = "10", accruedOn = "2026-05-20" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{projectB}/treasury/prepayment-amortisations", _financeOnProjectA, new { month = "2026-05-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{projectB}/treasury/accruals", _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{projectB}/budget-baseline", _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_account_list_serves_the_form_types_and_a_project_list_needs_the_project_read_with_assignment()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account company = await AddAccountAsync(AccountType.CompanyExpense);
        Account cost = await AddAccountAsync(AccountType.ProjectCost, _projectA);
        Account safe = await AddAccountAsync(AccountType.Safe);
        Guid projectB = await NewProjectAsync(assignFinance: false);

        JsonElement all = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accounts", _financeCompanyWide));
        Guid[] ids = [.. all.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
        ids.Should().Contain([accrued.Id, company.Id]).And.NotContain([cost.Id, safe.Id], "project accounts need the project read, and a safe is not a form type");

        JsonElement one = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accounts?type=CompanyExpense", _owner));
        one.EnumerateArray().Select(row => row.GetProperty("type").GetString()).Should().OnlyContain(type => type == "CompanyExpense");
        JsonElement shape = one.EnumerateArray().First(row => row.GetProperty("id").GetGuid() == company.Id);
        shape.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(["id", "code", "nameAr", "nameEn", "type", "projectId"]);

        JsonElement mine = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/treasury/accounts?projectId={_projectA}", _financeOnProjectA));
        mine.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).Should().Contain(cost.Id).And.NotContain(company.Id);

        (await SendAsync(HttpMethod.Get, $"/api/treasury/accounts?projectId={projectB}", _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/treasury/accounts?projectId={_projectA}", _hr)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_reports_are_read_by_owner_and_finance_only()
    {
        foreach (string route in new[]
                 {
                     "/api/treasury/trial-balance", "/api/treasury/trial-balance/export", "/api/treasury/overhead-spread?month=2012-08-01",
                     "/api/treasury/assets", "/api/treasury/accruals", "/api/treasury/prepayments", "/api/treasury/accounts",
                 })
        {
            foreach (Guid actor in new[] { _hr, _technicalOffice })
            {
                (await SendAsync(HttpMethod.Get, route, actor)).StatusCode.Should().Be(HttpStatusCode.Forbidden, route);
            }

            (await SendAsync(HttpMethod.Get, route, _owner)).StatusCode.Should().Be(HttpStatusCode.OK, route);
            (await SendAsync(HttpMethod.Get, route, _financeCompanyWide)).StatusCode.Should().Be(HttpStatusCode.OK, route);
        }
    }

    // ---- KAFF-704 · trial balance ------------------------------------------------------------------------

    [Fact]
    public async Task AC_704_A_the_trial_balance_shows_each_account_in_one_column_with_a_contra_account_in_credit_and_equal_totals()
    {
        Account accumulated = await AddAccountAsync(AccountType.AccumulatedDepreciation);
        Account depreciation = await AddAccountAsync(AccountType.DepreciationExpense);
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        await PostDirectAsync(accumulated, depreciation, 500m, PostingType.Depreciation, new DateOnly(2012, 7, 31));
        await PostDirectAsync(accrued, expense, 2_000m, PostingType.ExpenseAccrual, new DateOnly(2012, 7, 15));

        JsonElement report = await JsonAsync(await SendAsync(
            HttpMethod.Get, "/api/treasury/trial-balance?from=2012-07-01&to=2012-07-31", _financeCompanyWide));

        report.GetProperty("isBalanced").GetBoolean().Should().BeTrue();
        Dec(report.GetProperty("totalDebit")).Should().Be(Dec(report.GetProperty("totalCredit")));
        Dec(report.GetProperty("totalDebit")).Should().Be(2_500m);

        JsonElement[] lines = [.. report.GetProperty("lines").EnumerateArray()];
        lines.Should().HaveCount(4);

        JsonElement contra = lines.Single(line => line.GetProperty("accountId").GetGuid() == accumulated.Id);
        Dec(contra.GetProperty("credit")).Should().Be(500m);
        Dec(contra.GetProperty("debit")).Should().Be(0m);

        JsonElement cost = lines.Single(line => line.GetProperty("accountId").GetGuid() == expense.Id);
        Dec(cost.GetProperty("debit")).Should().Be(2_000m);
    }

    [Fact]
    public async Task AC_704_B_the_five_ledgers_are_separate_rows_that_never_net()
    {
        Guid project = await NewProjectAsync(assignFinance: false);
        Guid party = Guid.CreateVersion7();

        Account hold = await AddAccountAsync(AccountType.Hold, project, PartyType.Client, party);
        Account advance = await AddAccountAsync(AccountType.ClientAdvance, project, PartyType.Client, party);
        Account receivable = await AddAccountAsync(AccountType.ClientReceivable, project, PartyType.Client, party);
        Account safe = await AddAccountAsync(AccountType.Safe);
        Account capital = await AddAccountAsync(AccountType.PaidInCapital);

        await PostDirectAsync(capital, safe, 1_000m, PostingType.OpeningBalance, new DateOnly(2012, 9, 1));
        await PostDirectAsync(advance, safe, 300m, PostingType.ClientAdvanceReceipt, new DateOnly(2012, 9, 2), project);
        await PostDirectAsync(receivable, hold, 100m, PostingType.HoldAccrual, new DateOnly(2012, 9, 3), project);

        JsonElement report = await JsonAsync(await SendAsync(
            HttpMethod.Get, "/api/treasury/trial-balance?from=2012-09-01&to=2012-09-30", _owner));

        JsonElement[] lines = [.. report.GetProperty("lines").EnumerateArray()];
        lines.Select(line => line.GetProperty("accountId").GetGuid()).Should().Contain([hold.Id, advance.Id]);
        Dec(lines.Single(line => line.GetProperty("accountId").GetGuid() == hold.Id).GetProperty("debit")).Should().Be(100m);
        Dec(lines.Single(line => line.GetProperty("accountId").GetGuid() == advance.Id).GetProperty("credit")).Should().Be(300m);
        lines.Single(line => line.GetProperty("accountId").GetGuid() == hold.Id).GetProperty("projectName").GetString().Should().NotBeNullOrWhiteSpace("a project-tagged account names its project");
        lines.Single(line => line.GetProperty("accountId").GetGuid() == safe.Id).GetProperty("projectName").ValueKind.Should().Be(JsonValueKind.Null, "a company account has no project");
        report.GetProperty("isBalanced").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AC_704_C_the_export_is_a_csv_file_with_a_byte_order_mark_and_readable_arabic_names()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        await PostDirectAsync(accrued, expense, 75.5m, PostingType.ExpenseAccrual, new DateOnly(2012, 10, 5));

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, "/api/treasury/trial-balance/export?from=2012-10-01&to=2012-10-31", _financeCompanyWide);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);

        string text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        text.Should().StartWith("الرمز,الاسم بالعربية,الاسم بالإنجليزية,النوع,التصنيف,المشروع,مدين,دائن");
        text.Should().Contain("حساب اختبار");
        text.Should().Contain("75.50");
        text.Should().Contain("الإجمالي,,,,,,75.50,75.50");
    }

    [Fact]
    public async Task A_trial_balance_range_that_runs_backwards_is_refused()
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, "/api/treasury/trial-balance?from=2012-12-31&to=2012-12-01", _financeCompanyWide);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.accounting.report_range_invalid");
    }

    [Fact]
    public async Task AC_704_D_the_trial_balance_reads_postings_never_a_stored_balance()
    {
        typeof(Account).GetProperties().Select(property => property.Name).Should().NotContain(["Balance", "CurrentBalance"]);

        await using KaffDbContext context = _database.CreateBareContext();
        context.Model.GetEntityTypes()
            .Where(entity => entity.ClrType != typeof(AccountBalance))
            .SelectMany(entity => entity.GetProperties())
            .Select(property => property.Name)
            .Where(name => name.Contains("Balance", StringComparison.OrdinalIgnoreCase) && !name.Contains("Normal", StringComparison.Ordinal))
            .Should().BeEmpty("no table in the model stores a balance (CLAUDE.md); the account_balances view is derived from postings");
    }

    // ---- KAFF-708 and KAFF-707 AC5 · overhead spread and project margins -------------------------------------

    [Fact]
    public async Task AC_708_A_overhead_is_spread_over_active_projects_and_the_shares_add_up_to_it_and_AC_708_B_no_posting_is_written()
    {
        Guid project = await NewSignedActiveProjectAsync(1_000_000m);

        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account overhead = await AddAccountAsync(AccountType.CompanyExpense);
        await PostDirectAsync(accrued, overhead, 900m, PostingType.ExpenseAccrual, new DateOnly(2012, 8, 10));

        int before = await PostingCountAsync();

        JsonElement report = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/overhead-spread?month=2012-08-20", _financeCompanyWide));

        Dec(report.GetProperty("overhead")).Should().Be(900m);
        Dec(report.GetProperty("sharesTotal")).Should().Be(900m);
        report.GetProperty("projects").EnumerateArray().Select(line => line.GetProperty("projectId").GetGuid()).Should().Contain(project);
        report.GetProperty("projects").EnumerateArray().Single(line => line.GetProperty("projectId").GetGuid() == project)
            .GetProperty("projectName").GetString().Should().NotBeNullOrWhiteSpace();

        (await PostingCountAsync()).Should().Be(before, "the spread is a report: it writes no posting (spec.md section 6.10)");
    }

    [Fact]
    public async Task AC_707_E_a_projects_gross_and_net_margin_follow_the_tag_on_its_revenue_and_cost()
    {
        Guid project = await NewSignedActiveProjectAsync(2_000_000m);
        Guid client = _clientId;

        Account revenue = await AddAccountAsync(AccountType.ContractRevenue, project);
        Account receivable = await AddAccountAsync(AccountType.ClientReceivable, project, PartyType.Client, client);
        Account projectCost = await AddAccountAsync(AccountType.ProjectCost, project);
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);

        await PostDirectAsync(revenue, receivable, 5_000m, PostingType.RevenueRecognition, new DateOnly(2012, 11, 10), project);
        await PostDirectAsync(accrued, projectCost, 1_500m, PostingType.ExpenseAccrual, new DateOnly(2012, 11, 12), project);

        JsonElement report = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/overhead-spread?month=2012-11-01", _financeCompanyWide));
        JsonElement line = report.GetProperty("projects").EnumerateArray().Single(candidate => candidate.GetProperty("projectId").GetGuid() == project);

        Dec(line.GetProperty("revenue")).Should().Be(5_000m);
        Dec(line.GetProperty("projectCost")).Should().Be(1_500m);
        Dec(line.GetProperty("grossMargin")).Should().Be(3_500m);

        // No company overhead this month, so the net margin equals the gross margin.
        Dec(line.GetProperty("netMargin")).Should().Be(3_500m);
    }

    // ---- KAFF-709 · budget baseline ----------------------------------------------------------------------

    [Fact]
    public async Task AC_709_A_the_owner_sets_the_baseline_from_the_signed_boq_cost_with_the_default_tolerance()
    {
        Guid project = await NewSignedLumpSumProjectAsync();

        HttpResponseMessage set = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/budget-baseline", _owner, new { });

        set.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement baseline = await JsonAsync(set);

        // 1,000 units at a cost price of 800 each, not the 1,000 sell rate.
        Dec(baseline.GetProperty("costTotal")).Should().Be(800_000m);
        Dec(baseline.GetProperty("tolerancePercent")).Should().Be(10m);
        Dec(baseline.GetProperty("limit")).Should().Be(880_000m);

        JsonElement read = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _owner));
        Dec(read.GetProperty("costTotal")).Should().Be(800_000m);
    }

    [Fact]
    public async Task AC_709_A_the_owner_names_the_tolerance()
    {
        Guid project = await NewSignedLumpSumProjectAsync();

        JsonElement baseline = await JsonAsync(await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/budget-baseline", _owner, new { tolerancePercent = 5 }));

        Dec(baseline.GetProperty("tolerancePercent")).Should().Be(5m);
        Dec(baseline.GetProperty("limit")).Should().Be(840_000m);
    }

    [Fact]
    public async Task AC_709_C_a_baseline_is_set_once_and_has_no_edit_or_delete_route()
    {
        Guid project = await NewSignedLumpSumProjectAsync();
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/budget-baseline", _owner, new { })).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage again = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/budget-baseline", _owner, new { tolerancePercent = 50 });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(again)).Should().Be("errors.accounting.baseline_already_set");

        foreach (HttpMethod method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            HttpResponseMessage response = await SendAsync(method, $"/api/projects/{project}/budget-baseline", _owner, new { tolerancePercent = 50 });
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        JsonElement read = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _owner));
        Dec(read.GetProperty("tolerancePercent")).Should().Be(10m);
    }

    [Fact]
    public async Task A_baseline_needs_a_signed_project_and_only_the_owner_sets_it()
    {
        (await SendAsync(HttpMethod.Post, $"/api/projects/{_projectA}/budget-baseline", _owner, new { }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        Guid signed = await NewSignedLumpSumProjectAsync();

        foreach (Guid actor in new[] { _financeOnProjectA, _financeCompanyWide, _hr, _technicalOffice })
        {
            (await SendAsync(HttpMethod.Post, $"/api/projects/{signed}/budget-baseline", actor, new { }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Get, $"/api/projects/{signed}/budget-baseline", _owner)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_and_assigned_finance_read_the_baseline_and_an_assigned_site_engineer_is_refused()
    {
        Guid project = await NewSignedLumpSumProjectAsync();
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/budget-baseline", _owner, new { })).StatusCode.Should().Be(HttpStatusCode.Created);

        Guid siteEngineer;
        await using (KaffDbContext context = _database.CreateContext())
        {
            User engineer = User.Create(
                UniqueNames.Code("ac-eng"), "ac-eng", UniqueNames.Phone(), Role.SiteEngineer, Now,
                WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
            context.Users.Add(engineer);
            await context.SaveChangesAsync(Ct);

            User finance = await context.Users.SingleAsync(user => user.Id == _financeOnProjectA, Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project, engineer, AssignmentLevel.Junior, Guid.CreateVersion7(), Now).Value);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project, finance, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);
            await context.SaveChangesAsync(Ct);
            siteEngineer = engineer.Id;
        }

        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _owner)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", siteEngineer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _technicalOffice)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _hr)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AC_709_D_a_later_price_change_does_not_reach_the_stored_baseline()
    {
        Guid project = await NewSignedLumpSumProjectAsync();
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/budget-baseline", _owner, new { })).StatusCode.Should().Be(HttpStatusCode.Created);

        await using (KaffDbContext context = _database.CreateContext())
        {
            // The catalogue price moving cannot reach a signed BOQ line, and the baseline holds its own copy of the total.
            List<CatalogueItem> items = await context.CatalogueItems.ToListAsync(Ct);
            foreach (CatalogueItem item in items)
            {
                item.Reprice(new Money(9_999m), new Money(19_999m));
            }

            await context.SaveChangesAsync(Ct);
        }

        Dec((await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{project}/budget-baseline", _owner))).GetProperty("costTotal")).Should().Be(800_000m);
    }

    // ---- the Owner decides: approval, return with a reason, who may decide --------------------------------------

    [Fact]
    public async Task The_owner_returns_a_request_with_a_reason_nothing_posts_and_finance_may_ask_again()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        object body = new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "70", accruedOn = "2026-05-20" };

        Guid requestId = (await JsonAsync(await SendAsync(HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide, body))).GetProperty("requestId").GetGuid();

        HttpResponseMessage blank = await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/rejection", _owner, new { reason = "  " });
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(blank)).Should().Be("errors.closure.reason_required");

        HttpResponseMessage rejected = await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/rejection", _owner, new { reason = "Wrong month" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(rejected)).GetProperty("status").GetString().Should().Be("Rejected");

        (await PostingCountAsync(accrued.Id)).Should().Be(0);

        JsonElement listed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accounting-requests", _financeCompanyWide));
        JsonElement row = listed.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == requestId);
        row.GetProperty("rejectionReason").GetString().Should().Be("Wrong month");
        row.GetProperty("askedByName").GetString().Should().NotBeNullOrWhiteSpace();
        row.GetProperty("decidedByName").GetString().Should().NotBeNullOrWhiteSpace();

        // A decided request cannot be decided again, and Finance may simply ask again.
        HttpResponseMessage again = await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/approval", _owner);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(again)).Should().Be("errors.accounting.request_not_waiting");

        (await SendAsync(HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide, body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_approval_cannot_be_given_twice_and_an_unknown_request_is_not_found()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        Guid requestId = (await JsonAsync(await SendAsync(
            HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "70", accruedOn = "2026-05-20" }))).GetProperty("requestId").GetGuid();

        await ApproveAsync(requestId);

        (await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/approval", _owner)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostingCountAsync(accrued.Id)).Should().Be(1);
        (await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{Guid.CreateVersion7()}/approval", _owner)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_the_owner_decides_finance_hr_and_technical_office_are_refused()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        Guid requestId = (await JsonAsync(await SendAsync(
            HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "70", accruedOn = "2026-05-20" }))).GetProperty("requestId").GetGuid();

        foreach (Guid actor in new[] { _financeCompanyWide, _financeOnProjectA, _hr, _technicalOffice })
        {
            (await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/approval", actor)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/rejection", actor, new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await PostingCountAsync(accrued.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_project_request_is_listed_through_its_own_project_and_approved_by_the_owner()
    {
        Guid projectB = await NewProjectAsync(assignFinance: false);
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account projectCost = await AddAccountAsync(AccountType.ProjectCost, _projectA);

        Guid requestId = (await JsonAsync(await SendAsync(
            HttpMethod.Post, $"/api/projects/{_projectA}/treasury/accruals", _financeOnProjectA,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = projectCost.Id, amount = "70", accruedOn = "2026-05-20" }))).GetProperty("requestId").GetGuid();

        JsonElement mine = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{_projectA}/treasury/accounting-requests?status=Requested", _financeOnProjectA));
        mine.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).Should().Contain(requestId);

        JsonElement company = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accounting-requests", _financeCompanyWide));
        company.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).Should().NotContain(requestId, "the company list holds company-tagged requests only");

        (await SendAsync(HttpMethod.Get, $"/api/projects/{projectB}/treasury/accounting-requests", _financeOnProjectA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        JsonElement approved = await ApproveAsync(requestId);
        approved.GetProperty("postingIds").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task A_request_that_could_never_post_is_refused_at_once()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "0", accruedOn = "2026-05-20" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonElement listed = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/treasury/accounting-requests?status=Requested", _financeCompanyWide));
        listed.EnumerateArray().Should().NotContain(row => row.GetProperty("payload").GetString()!.Contains(accrued.Id.ToString(), StringComparison.Ordinal));
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    /// <summary>The Owner approves a waiting request and the call must succeed.</summary>
    private async Task<JsonElement> ApproveAsync(Guid requestId)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/treasury/accounting-requests/{requestId}/approval", _owner);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonAsync(response);
    }

    private async Task<JsonElement> AskAndApproveAsync(string route, Guid asker, object body)
    {
        HttpResponseMessage asked = await SendAsync(HttpMethod.Post, route, asker, body);
        asked.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ApproveAsync((await JsonAsync(asked)).GetProperty("requestId").GetGuid());
    }

    private async Task<int> PostingCountAsync(Guid accountId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.CountAsync(posting => posting.FromAccountId == accountId || posting.ToAccountId == accountId, Ct);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static DateOnly Today => new(2026, 6, 1);

    private static decimal Dec(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
        : element.GetDecimal();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        JsonElement problem = await JsonAsync(response);
        return problem.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstOrDefaultAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found?.SecurityStamp ?? "no-such-user");

        if (found is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<Account> AddAccountAsync(AccountType type, Guid? projectId = null, PartyType? partyType = null, Guid? partyId = null)
    {
        Result<Account> created = Account.Create(
            type, UniqueNames.Code($"AC{(int)type}"), "حساب اختبار", "Test account", Currency.Egp, new DateOnly(2012, 1, 1), projectId, partyType, partyId);

        created.IsSuccess.Should().BeTrue(created.IsFailure ? created.Error.Code : string.Empty);

        await using KaffDbContext context = _database.CreateContext();
        context.Accounts.Add(created.Value);
        await context.SaveChangesAsync(Ct);

        return created.Value;
    }

    private async Task PostDirectAsync(
        Account from, Account to, decimal amount, PostingType type, DateOnly date, Guid? projectId = null)
    {
        Result<Posting> posting = Posting.Create(
            from, to, new Money(amount), type, new SourceDocument(SourceDocumentType.Payment, Guid.CreateVersion7(), null),
            date, _financeCompanyWide, Now, projectId);

        posting.IsSuccess.Should().BeTrue(posting.IsFailure ? posting.Error.Code : string.Empty);

        await using KaffDbContext context = _database.CreateContext();
        context.Postings.Add(posting.Value);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<Guid> FundAsync(Account prepaid, decimal amount)
    {
        Account capital = await AddAccountAsync(AccountType.PaidInCapital);
        Account safe = await AddAccountAsync(AccountType.Safe);
        await PostDirectAsync(capital, safe, amount, PostingType.OpeningBalance, new DateOnly(2012, 1, 5));

        Posting payment = Posting.Create(
            safe, prepaid, new Money(amount), PostingType.CompanyExpensePayment,
            new SourceDocument(SourceDocumentType.Payment, Guid.CreateVersion7(), null), new DateOnly(2012, 1, 6), _financeCompanyWide, Now).Value;

        await using KaffDbContext context = _database.CreateContext();
        context.Postings.Add(payment);
        await context.SaveChangesAsync(Ct);

        return payment.Id;
    }

    private async Task<(Guid AccrualId, Account Accrued, Account Expense)> CreateAccrualAsync()
    {
        Account accrued = await AddAccountAsync(AccountType.AccruedExpense);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);

        JsonElement approved = await AskAndApproveAsync(
            "/api/treasury/accruals", _financeCompanyWide,
            new { accruedExpenseAccountId = accrued.Id, expenseAccountId = expense.Id, amount = "5000", accruedOn = "2026-05-20" });

        return (approved.GetProperty("accrualId").GetGuid(), accrued, expense);
    }

    private async Task<(Guid ScheduleId, Account Prepaid, Account Expense)> CreatePrepaymentAsync(decimal total, int months, string firstMonth)
    {
        Account prepaid = await AddAccountAsync(AccountType.Prepayment);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        Guid funding = await FundAsync(prepaid, total);

        HttpResponseMessage created = await SendAsync(
            HttpMethod.Post, "/api/treasury/prepayments", _financeCompanyWide,
            new
            {
                prepaymentAccountId = prepaid.Id,
                expenseAccountId = expense.Id,
                fundingPostingId = funding,
                totalAmount = total.ToString(CultureInfo.InvariantCulture),
                months,
                firstMonth,
                description = "Insurance",
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        return ((await JsonAsync(created)).GetProperty("id").GetGuid(), prepaid, expense);
    }

    private async Task<(Account Accumulated, Account Expense)> DepreciationAccountsAsync() =>
        (await AddAccountAsync(AccountType.AccumulatedDepreciation), await AddAccountAsync(AccountType.DepreciationExpense));

    private Task<HttpResponseMessage> RegisterAssetAsync(
        Account accumulated, Account expense, string category, decimal cost, string purchaseDate, Guid? actor = null) =>
        SendAsync(
            HttpMethod.Post, "/api/treasury/assets", actor ?? _financeCompanyWide,
            new
            {
                name = "Asset",
                category,
                cost = cost.ToString(CultureInfo.InvariantCulture),
                purchaseDate,
                accumulatedDepreciationAccountId = accumulated.Id,
                depreciationExpenseAccountId = expense.Id,
            });

    private async Task EnsureClosedPeriodAsync(int year, int month)
    {
        await using KaffDbContext context = _database.CreateContext();

        if (await context.AccountingPeriods.AnyAsync(period => period.Year == year && period.Month == month, Ct))
        {
            return;
        }

        AccountingPeriod period = AccountingPeriod.Create(year, month).Value;
        period.Close(_owner, Now).IsSuccess.Should().BeTrue();
        context.AccountingPeriods.Add(period);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<int> PostingCountAsync()
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.CountAsync(Ct);
    }

    private async Task<Guid> NewProjectAsync(bool assignFinance)
    {
        await using KaffDbContext context = _database.CreateContext();

        Project project = Project.Create(
            UniqueNames.Code("AC-P"), "مشروع محاسبة", _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        if (assignFinance)
        {
            User finance = await context.Users.SingleAsync(user => user.Id == _financeOnProjectA, Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, finance, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);
            await context.SaveChangesAsync(Ct);
        }

        return project.Id;
    }

    /// <summary>An active project with a contract value, for the overhead spread. No BOQ: the value is what the spread reads.</summary>
    private async Task<Guid> NewSignedActiveProjectAsync(decimal contractValue)
    {
        await using KaffDbContext context = _database.CreateContext();

        Project project = Project.Create(
            UniqueNames.Code("AC-S"), "مشروع موقع", _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.Sign(new DateOnly(2026, 1, 5), new Money(contractValue)).IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);

        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        return project.Id;
    }

    /// <summary>A Lump Sum project with a BOQ of 1,000 units at cost 800 and sell rate 1,000, signed through the real sign endpoint.</summary>
    private async Task<Guid> NewSignedLumpSumProjectAsync()
    {
        Project project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            project = Project.Create(UniqueNames.Code("AC-B"), UniqueNames.Code("مشروع مقايسة"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
            project.SetLumpSumTerms(
                Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
                Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
            context.Projects.Add(project);
            await context.SaveChangesAsync(Ct);

            Bab bab = await context.Babs.SingleAsync(candidate => candidate.Id == _babId, Ct);
            Boq boq = Boq.Open(project, Now).Value;
            boq.AddCustomLine(null, "خرسانة", "Concrete", "m3", bab, new Money(800m), new Money(1_000m), 1_000m, 1_000m, null, null)
                .IsSuccess.Should().BeTrue();
            context.Boqs.Add(boq);

            IReadOnlyList<Account> accounts = ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), new DateOnly(2026, 9, 24)).Value;
            context.Accounts.AddRange(accounts);
            await context.SaveChangesAsync(Ct);
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/sign", _owner,
                new { signedOn = "2026-09-24", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return project.Id;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(UniqueNames.Code("AC-C1"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Project projectA = Project.Create(UniqueNames.Code("AC-PA"), "مشروع أ", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        projectA.TransitionTo(ProjectStatus.Active, Today).IsSuccess.Should().BeTrue();

        User owner = User.Create(UniqueNames.Code("ac-owner"), "ac-owner", UniqueNames.Phone(), Role.Owner, Now, WellKnownDepartments.FinanceId).Value;
        User financeCompanyWide = User.Create(UniqueNames.Code("ac-fin-co"), "ac-fin-co", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;
        User financeOnProjectA = User.Create(UniqueNames.Code("ac-fin-a"), "ac-fin-a", UniqueNames.Phone(), Role.Finance, Now, WellKnownDepartments.FinanceId).Value;
        User hr = User.Create(UniqueNames.Code("ac-hr"), "ac-hr", UniqueNames.Phone(), Role.Hr, Now, WellKnownDepartments.HrId).Value;
        User technicalOffice = User.Create(
            UniqueNames.Code("ac-to"), "ac-to", UniqueNames.Phone(), Role.TechnicalOffice, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;

        Bab bab = Bab.Create(UniqueNames.Code("AC-BAB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;

        context.Clients.Add(client);
        context.Projects.Add(projectA);
        context.Users.AddRange(owner, financeCompanyWide, financeOnProjectA, hr, technicalOffice);
        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(ProjectAssignment.Create(projectA.Id, financeOnProjectA, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);
        context.ProjectAssignments.Add(ProjectAssignment.Create(projectA.Id, technicalOffice, AssignmentLevel.Standard, Guid.CreateVersion7(), Now).Value);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _projectA = projectA.Id;
        _owner = owner.Id;
        _financeCompanyWide = financeCompanyWide.Id;
        _financeOnProjectA = financeOnProjectA.Id;
        _hr = hr.Id;
        _technicalOffice = technicalOffice.Id;
        _babId = bab.Id;
    }
}
