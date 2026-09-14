using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Treasury;

/// <summary>
/// KAFF-302 — a project's ledger-account set is created when the project is created.
/// </summary>
/// <remarks>
/// One test per <c>qa/slice-3/test-cases.md</c> <c>TC-3-033</c>…<c>TC-3-039</c> (<c>AC-302-A</c>…<c>G</c>).
/// The story's own "Permissions" section says it plainly: "This story adds no endpoint of its own" — so
/// there is no HTTP surface to drive. The mechanism under test is
/// <see cref="ProjectAccountSetCreator"/>, exercised directly against a real PostgreSQL the same way a
/// future project-creation handler would call it: the project and its accounts added to one
/// <see cref="KaffDbContext"/> and saved in a single <c>SaveChangesAsync</c>. Real PostgreSQL, per the
/// qa file's own layer assignment for this story, matters most for <c>TC-3-037</c>, which needs a real
/// unique-index collision and a real transaction rollback — a mock could not prove either.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectAccountSetCreationTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private Guid _clientId;

    public ProjectAccountSetCreationTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("PAS-C"), "عميل اختبار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        context.Clients.Add(client);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- AC-302-A · a Lump Sum project gets the full five-account set ----------------------------

    [Fact]
    public async Task TC_3_033_A_lump_sum_project_gets_the_full_five_account_set()
    {
        Project project = await CreateProjectAsync(ContractType.LumpSum);

        List<Account> accounts = await LoadAccountsAsync(project.Id);

        AccountType[] expected =
        [
            AccountType.ClientAdvance,
            AccountType.Hold,
            AccountType.FirmAdvance,
            AccountType.MaterialAdvance,
            AccountType.ClientReceivable,
        ];

        accounts.Select(account => account.Type).Should().BeEquivalentTo(expected);

        foreach (Account account in accounts)
        {
            AccountTypeMetadata meta = AccountTypes.Of(account.Type);

            account.ProjectId.Should().Be(project.Id);
            account.PartyType.Should().Be(PartyType.Client);
            account.PartyId.Should().Be(_clientId);
            account.Class.Should().Be(meta.Class, $"{account.Type} must carry the class AccountTypes dictates");
            account.NormalBalance.Should().Be(meta.NormalBalance, $"{account.Type} must carry the normal balance AccountTypes dictates");
            account.EnforceNonNegative.Should().Be(meta.EnforceNonNegative, $"{account.Type} must carry the floor AccountTypes dictates");
        }
    }

    // ---- AC-302-B · a Cost Plus project gets no Hold and no تشوينات account -----------------------

    [Fact]
    public async Task TC_3_034_A_cost_plus_project_gets_no_hold_and_no_material_advance_account()
    {
        Project project = await CreateProjectAsync(ContractType.CostPlus);

        List<Account> accounts = await LoadAccountsAsync(project.Id);

        accounts.Select(account => account.Type).Should().NotContain(AccountType.Hold);
        accounts.Select(account => account.Type).Should().NotContain(AccountType.MaterialAdvance);

        // Q88 (D-161): Cost Plus opens a revenue and a cost account instead.
        accounts.Select(account => account.Type).Should().Contain(AccountType.ContractRevenue);
        accounts.Select(account => account.Type).Should().Contain(AccountType.ProjectCost);
    }

    // ---- AC-302-C · a Design project gets no Hold and no تشوينات account -------------------------

    [Fact]
    public async Task TC_3_035_A_design_project_gets_no_hold_and_no_material_advance_account()
    {
        Project project = await CreateProjectAsync(ContractType.Design);

        List<Account> accounts = await LoadAccountsAsync(project.Id);

        accounts.Select(account => account.Type).Should().NotContain(AccountType.Hold);
        accounts.Select(account => account.Type).Should().NotContain(AccountType.MaterialAdvance);

        // Q88 (D-161): Design opens a revenue and a cost account instead.
        accounts.Select(account => account.Type).Should().Contain(AccountType.ContractRevenue);
        accounts.Select(account => account.Type).Should().Contain(AccountType.ProjectCost);
    }

    // ---- AC-302-D · عهدة is never opened as part of the automatic set ------------------------------

    [Fact]
    public async Task TC_3_036_Petty_cash_advance_is_never_opened_as_part_of_the_automatic_set()
    {
        foreach (ContractType contractType in Enum.GetValues<ContractType>())
        {
            Project project = await CreateProjectAsync(contractType);
            List<Account> accounts = await LoadAccountsAsync(project.Id);

            accounts.Select(account => account.Type).Should().NotContain(
                AccountType.PettyCashAdvance,
                "عهدة is per-employee and has no employee to attach to at project-creation time — KAFF-311 opens it");
        }
    }

    // ---- AC-302-E · account-set creation is all-or-nothing with project creation -------------------

    [Fact]
    public async Task TC_3_037_Account_set_creation_is_all_or_nothing_with_project_creation()
    {
        Project project = Project.Create(
            UniqueNames.Code("PAS-E"), "مشروع اختبار الذرية", _clientId, ContractType.LumpSum, Now).Value;

        // Force the account-creation step to fail: pre-plant a row at the exact code the factory
        // would generate for this project's Hold account, so the later INSERT collides on the unique
        // index ux_accounts_code — a real database error, not a mock or an injected fault.
        await using (KaffDbContext planter = _database.CreateContext())
        {
            Account collider = Account.Create(
                AccountType.Hold,
                $"{project.Code}-HOLD",
                "تصادم متعمد",
                "Deliberate collision",
                Currency.Egp,
                Today,
                projectId: Guid.CreateVersion7(),
                partyType: PartyType.Client,
                partyId: Guid.CreateVersion7()).Value;

            planter.Accounts.Add(collider);
            await planter.SaveChangesAsync(Ct);
        }

        await using KaffDbContext context = _database.CreateContext();
        context.Projects.Add(project);

        var creator = new ProjectAccountSetCreator(context);
        await creator.CreateAsync(project, Today, Ct);

        Func<Task> save = () => context.SaveChangesAsync(Ct);

        await save.Should().ThrowAsync<DbUpdateException>(
            "the Hold account's code collides with the pre-planted row, so the one save that carries "
            + "both the project and its accounts must roll back entirely");

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.Projects.AnyAsync(p => p.Id == project.Id, Ct)).Should().BeFalse(
            "the project must not be left existing with zero accounts to post against");
    }

    // ---- AC-302-F · creating the set twice is a no-op the second time -----------------------------

    [Fact]
    public async Task TC_3_038_Creating_the_account_set_twice_is_a_no_op_the_second_time()
    {
        Project project = await CreateProjectAsync(ContractType.LumpSum);

        List<Account> firstRun = await LoadAccountsAsync(project.Id);
        Dictionary<AccountType, Guid> firstIds = firstRun.ToDictionary(account => account.Type, account => account.Id);

        await using (KaffDbContext context = _database.CreateContext())
        {
            Project reloaded = await context.Projects.SingleAsync(p => p.Id == project.Id, Ct);
            var creator = new ProjectAccountSetCreator(context);

            IReadOnlyList<Account> secondRun = await creator.CreateAsync(reloaded, Today, Ct);

            secondRun.Should().BeEmpty("every type this project needs already exists — nothing left to create");

            await context.SaveChangesAsync(Ct);
        }

        List<Account> afterSecondRun = await LoadAccountsAsync(project.Id);

        afterSecondRun.Should().HaveCount(firstRun.Count, "no duplicate account was created by the re-run");

        foreach (Account account in afterSecondRun)
        {
            firstIds[account.Type].Should().Be(account.Id, "the existing account's row was not replaced or re-created");
        }
    }

    // ---- AC-302-G · every created account passes the same validation Account.Create enforces -------

    [Fact]
    public async Task TC_3_039_Every_created_account_passes_the_same_validation_account_create_enforces()
    {
        Project project = await CreateProjectAsync(ContractType.LumpSum);
        List<Account> accounts = await LoadAccountsAsync(project.Id);

        accounts.Should().NotBeEmpty();

        foreach (Account account in accounts)
        {
            AccountTypeMetadata meta = AccountTypes.Of(account.Type);

            // Scope: every type this factory opens is AccountScope.ProjectRequired. A bypassed
            // Account.Create.ValidateScope would let a null ProjectId through; it did not.
            meta.Scope.Should().Be(AccountScope.ProjectRequired);
            account.ProjectId.Should().NotBeNull();

            // Party: every type this factory opens requires PartyType.Client. A bypassed
            // Account.Create.ValidateParty would let a missing or mismatched party through; it did not.
            meta.RequiredParty.Should().Be(PartyType.Client);
            account.PartyType.Should().Be(PartyType.Client);
            account.PartyId.Should().NotBeNull();
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private async Task<Project> CreateProjectAsync(ContractType contractType)
    {
        Project project = Project.Create(
            UniqueNames.Code("PAS-P"), "مشروع اختبار", _clientId, contractType, Now).Value;

        await using KaffDbContext context = _database.CreateContext();
        context.Projects.Add(project);

        var creator = new ProjectAccountSetCreator(context);
        await creator.CreateAsync(project, Today, Ct);

        await context.SaveChangesAsync(Ct);

        return project;
    }

    private async Task<List<Account>> LoadAccountsAsync(Guid projectId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Accounts
            .Where(account => account.ProjectId == projectId)
            .ToListAsync(Ct);
    }
}
