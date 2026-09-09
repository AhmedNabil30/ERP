using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-202 — <c>POST /api/catalogue-items</c>.
/// </summary>
/// <remarks>
/// Every test here goes through HTTP. The domain guards (blank fields, negative prices) are pinned in
/// <c>Domain.Tests/CatalogueItemEditingTests.cs</c>; what can only be observed at this level is the
/// permission gate, the database-enforced code uniqueness, and what the audit trail actually contains.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class CreateCatalogueItemTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _hr;
    private Guid _siteEngineer;
    private Guid _headOfDesign;
    private Guid _marketing;
    private Guid _babId;

    public CreateCatalogueItemTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-202-A · an item is created with exactly §4.1's fields ------------------------------

    [Fact]
    public async Task An_item_is_created_with_exactly_the_submitted_values_and_is_active()
    {
        string code = UniqueNames.Code("CRT-A");

        HttpResponseMessage response = await CreateAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code, cost: 100m, sell: 150m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement root = body.RootElement;

        root.GetProperty("code").GetString().Should().Be(code);
        root.GetProperty("descriptionAr").GetString().Should().Be("خرسانة عادية");
        root.GetProperty("unit").GetString().Should().Be("م٣");
        root.GetProperty("babId").GetGuid().Should().Be(_babId);
        root.GetProperty("costPrice").GetDecimal().Should().Be(100m);
        root.GetProperty("baseSellRate").GetDecimal().Should().Be(150m);
        root.GetProperty("status").GetString().Should().Be(nameof(CatalogueItemStatus.Active));

        typeof(Kaff.Api.Features.Catalogue.CreateCatalogueItem.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["Id", "Code", "DescriptionAr", "DescriptionEn", "Unit", "BabId", "CostPrice", "BaseSellRate", "Status"],
                "AC-202-A: the response carries no field §4.1 does not name");
    }

    // ---- AC-202-B · a repeated code is refused by the unique index, not a lookup ----------------

    [Fact]
    public async Task A_repeated_code_is_refused_in_any_case()
    {
        string code = UniqueNames.Code("CRT-B");

        (await CreateAsync(_technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await CreateAsync(_technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "the exact code is already taken");

        (await CreateAsync(
                _technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code.ToLowerInvariant())))
            .StatusCode.Should().Be(
                HttpStatusCode.Conflict,
                "CatalogueItem.Create upper-invariants the code, so the lower-case form collides too");
    }

    [Fact]
    public async Task Two_concurrent_requests_for_the_same_code_leave_exactly_one_item()
    {
        string code = UniqueNames.Code("CRT-RACE");

        HttpResponseMessage[] results = await Task.WhenAll(
            CreateAsync(_technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code)),
            CreateAsync(_technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code)));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(
            1, "the guarantee is ux_catalogue_items_code, not a read-then-write a race can defeat");

        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.CatalogueItems.CountAsync(item => item.Code == code, Ct)).Should().Be(1);
    }

    // ---- AC-202-C · both prices keep four decimals through create and read-back -----------------

    [Fact]
    public async Task Both_prices_keep_four_decimals_through_create_and_read_back()
    {
        string code = UniqueNames.Code("CRT-C");

        HttpResponseMessage response = await CreateAsync(
            _technicalOffice,
            Role.TechnicalOffice,
            Department.Operations,
            Body(code, cost: 987.6543m, sell: 1234.5678m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid id = await IdOfAsync(response);

        await using KaffDbContext reader = _database.CreateBareContext();

        CatalogueItem stored = await reader.CatalogueItems.SingleAsync(item => item.Id == id, Ct);

        stored.CostPrice.Amount.Should().Be(987.6543m);
        stored.BaseSellRate.Amount.Should().Be(1234.5678m);
    }

    // ---- rule 9 · an unknown باب is refused, translatably --------------------------------------

    [Fact]
    public async Task An_unknown_bab_is_refused_rather_than_a_raw_foreign_key_failure()
    {
        HttpResponseMessage response = await CreateAsync(
            _technicalOffice,
            Role.TechnicalOffice,
            Department.Operations,
            new
            {
                code = UniqueNames.Code("CRT-BAB"),
                descriptionAr = "خرسانة",
                unit = "م٣",
                babId = Guid.NewGuid(),
                costPrice = 100m,
                baseSellRate = 150m,
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await MessageKeyAsync(response)).Should().Be("errors.master.bab_not_found");
    }

    // ---- AC-202-H · a role without CatalogueManage reaches neither endpoint (create half) -------

    [Fact]
    public async Task Only_technical_office_and_the_owner_may_create_a_catalogue_item()
    {
        foreach ((Guid actor, Role role, Department? department) in RefusedActors())
        {
            (await CreateAsync(actor, role, department, Body(UniqueNames.Code($"CRT-{role}"))))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    "{0} does not hold CatalogueManage — spec.md §2, §4.1",
                    role);
        }

        (await CreateAsync(
                _owner, Role.Owner, null, Body(UniqueNames.Code("CRT-OWNER"))))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the Owner holds every company-wide row — D-129 §1");
    }

    /// <summary>Every role QA's TC-2-025 names, so an uncovered ninth role fails here rather than silently.</summary>
    [Fact]
    public void The_refused_list_is_every_role_TC_2_025_names()
    {
        RefusedActors().Select(actor => actor.Role).Should().BeEquivalentTo(
            [Role.Finance, Role.SiteEngineer, Role.HeadOfDesign, Role.MarketingSales, Role.Hr],
            "TC-2-025 names Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor "
            + "and Hr; Client cannot reach a company-wide staff route and Subcontractor cannot sign in "
            + "at all (spec.md §9), so both are structurally refused rather than asserted here");
    }

    // ---- AC-202-I · creation is audited -----------------------------------------------------------

    [Fact]
    public async Task Creation_is_audited_and_names_the_actor()
    {
        string code = UniqueNames.Code("CRT-AUD");

        HttpResponseMessage response = await CreateAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, Body(code, cost: 10m, sell: 20m));

        Guid id = await IdOfAsync(response);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            record => record.EntityId == id && record.Action == AuditAction.Created, Ct);

        record.EntityType.Should().Be(nameof(CatalogueItem));
        record.ActorUserId.Should().Be(_technicalOffice);
        record.ActorRole.Should().Be(Role.TechnicalOffice);
        record.GrantPath.Should().BeNull("CatalogueManage is company-wide: no project, no path to name");

        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        after.RootElement.GetProperty(nameof(CatalogueItem.Code)).GetString().Should().Be(code);
        after.RootElement.GetProperty(nameof(CatalogueItem.CostPrice)).GetDecimal().Should().Be(10m);
        after.RootElement.GetProperty(nameof(CatalogueItem.BaseSellRate)).GetDecimal().Should().Be(20m);
    }

    [Fact]
    public async Task A_refused_creation_writes_no_audit_record()
    {
        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(Ct);

        (await CreateAsync(_finance, Role.Finance, Department.Finance, Body(UniqueNames.Code("CRT-REF"))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(
            countBefore, "a refused creation is not a creation — CLAUDE.md, AC-202-I");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private object Body(string code, decimal cost = 100m, decimal sell = 150m) => new
    {
        code,
        descriptionAr = "خرسانة عادية",
        unit = "م٣",
        babId = _babId,
        costPrice = cost,
        baseSellRate = sell,
    };

    private IEnumerable<(Guid Actor, Role Role, Department? Department)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance);
        yield return (_hr, Role.Hr, Department.Hr);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations);
        yield return (_marketing, Role.MarketingSales, Department.Marketing);
    }

    private Task<HttpResponseMessage> CreateAsync(
        Guid actorId, Role actorRole, Department? actorDepartment, object body)
        => SendAsync(HttpMethod.Post, "/api/catalogue-items", actorId, actorRole, actorDepartment, body);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, Department? actorDepartment, object body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
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

        Bab bab = Bab.Create(UniqueNames.Code("CRT-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        User owner = MakeUser("crt-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "crt-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("crt-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("crt-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "crt-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "crt-design", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("crt-marketing", Role.MarketingSales, Department.Marketing);

        context.Babs.Add(bab);
        context.Users.AddRange(owner, technicalOffice, finance, hr, siteEngineer, headOfDesign, marketing);

        await context.SaveChangesAsync(Ct);

        _babId = bab.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
