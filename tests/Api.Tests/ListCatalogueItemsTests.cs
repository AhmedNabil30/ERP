using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Features.Catalogue.ListCatalogueItems;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-203 — <c>GET /api/catalogue-items</c>.
/// </summary>
/// <remarks>
/// Every test here goes through HTTP, against fixtures nonced to this class, because the database is
/// shared by every class in the collection — the same discipline <c>ListClientsTests</c> uses.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ListCatalogueItemsTests : IAsyncLifetime
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
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public ListCatalogueItemsTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-203-A · a code finds the item, in either case --------------------------------------

    [Fact]
    public async Task A_code_finds_the_item_in_either_case()
    {
        Guid bab = await CreateBabAsync();
        string code = await CreateItemAsync(bab, code: UniqueNames.Code("FND-A"));

        (await SearchAsync(code)).Select(item => item.Code).Should().Contain(code);
        (await SearchAsync(code.ToLowerInvariant())).Select(item => item.Code).Should().Contain(
            code, "codes are stored upper-cased by CatalogueItem.Create, and ILIKE is already case-insensitive");
    }

    // ---- AC-203-B · a partial Arabic description finds the item --------------------------------

    [Fact]
    public async Task A_substring_from_the_middle_of_an_arabic_description_finds_the_item()
    {
        Guid bab = await CreateBabAsync();
        string nonce = UniqueNames.Code("FND-B");
        string code = await CreateItemAsync(bab, code: UniqueNames.Code("FND-B-C"), descriptionAr: $"خرسانة {nonce} عادية للأساسات");

        (await SearchAsync($"{nonce} عادية")).Select(item => item.Code).Should().Contain(
            code, "the middle of the phrase must match, not only a prefix");
    }

    // ---- AC-203-C · the search reaches both fields in one query --------------------------------

    [Fact]
    public async Task One_query_reaches_both_code_and_description()
    {
        Guid bab = await CreateBabAsync();
        string nonce = UniqueNames.Code("FND-C");

        string byCode = await CreateItemAsync(bab, code: nonce);
        string byDescription = await CreateItemAsync(
            bab, code: UniqueNames.Code("FND-C-D"), descriptionAr: $"وصف يحتوي {nonce} بالنص");

        (await SearchAsync(nonce)).Select(item => item.Code).Should().BeEquivalentTo(
            [byCode, byDescription],
            "the caller does not choose which field to search — both items matching the same term come back");
    }

    // ---- AC-203-D · an empty result is a 200 with an empty array, never null or 404 ------------

    [Fact]
    public async Task A_search_matching_nothing_is_an_empty_list_and_not_an_error()
    {
        HttpResponseMessage response = await SendAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, UniqueNames.Code("FND-NONE"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "nothing found is not something gone wrong");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.TryGetProperty("items", out JsonElement items).Should().BeTrue();
        items.ValueKind.Should().Be(
            JsonValueKind.Array, "never null — a screen cannot render an empty state from an absent field");
        items.GetArrayLength().Should().Be(0);
    }

    // ---- AC-203-E · a portal client cannot search the catalogue ---------------------------------

    [Fact]
    public async Task A_portal_client_is_refused_and_no_item_data_reaches_the_body()
    {
        Guid bab = await CreateBabAsync();
        string nonce = UniqueNames.Code("FND-PRT");
        await CreateItemAsync(bab, code: nonce, descriptionAr: $"سرية {nonce}");

        HttpResponseMessage response = await SendAsync(
            _portalClient, Role.Client, null, nonce, actorClientId: _portalClientCompany);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "spec.md §12 is absolute and D-035's portal boundary puts this endpoint off the portal surface entirely");

        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain(
            nonce, "a refusal that still names an item's code or description has refused nothing that matters");
    }

    // ---- AC-203-F · a role without CatalogueManage is refused, and the holder sees cost price ---

    [Fact]
    public async Task Only_technical_office_and_the_owner_may_search_and_the_holder_sees_cost_price()
    {
        foreach ((Guid actor, Role role, Department? department) in RefusedActors())
        {
            (await SendAsync(actor, role, department, null))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    "{0} does not hold CatalogueManage — spec.md §2, D-129 §1",
                    role);
        }

        Guid bab = await CreateBabAsync();
        string code = await CreateItemAsync(bab, code: UniqueNames.Code("FND-COST"), cost: 42.5m);

        IReadOnlyList<CatalogueItemSummary> found = await SearchAsyncAs(_owner, Role.Owner, null, code);

        found.Should().ContainSingle().Which.CostPrice.Should().Be(
            42.5m, "the Owner and the Technical Office are the only holders of CatalogueManage, and "
            + "the whole surface is internal — TC-2-033's positive control");
    }

    /// <summary>Every role QA's TC-2-033 names that can structurally reach a company-wide staff route.</summary>
    [Fact]
    public void The_refused_list_is_every_reachable_role_TC_2_033_names()
    {
        RefusedActors().Select(actor => actor.Role).Should().BeEquivalentTo(
            [Role.Finance, Role.SiteEngineer, Role.HeadOfDesign, Role.MarketingSales, Role.Hr],
            "TC-2-033 names Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor "
            + "and Hr; Client is covered separately (portal session, AC-203-E) and Subcontractor cannot "
            + "sign in at all (spec.md §9), so both are structurally refused rather than asserted here — "
            + "the same split CreateCatalogueItemTests uses");
    }

    // ---- rule 10 · a read writes no audit record -------------------------------------------------

    [Fact]
    public async Task A_search_writes_no_audit_record()
    {
        Guid bab = await CreateBabAsync();
        string code = await CreateItemAsync(bab, code: UniqueNames.Code("FND-AUD"));

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(Ct);

        (await SearchAsyncAs(_technicalOffice, Role.TechnicalOffice, Department.Operations, code))
            .Should().ContainSingle();

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(
            countBefore, "a read writes no audit record — CLAUDE.md, KAFF-203 rule 10, the audit trail is not a search log");
    }

    // ---- AC-203-G · no money beyond the item's own two prices -----------------------------------

    [Fact]
    public void No_field_beyond_the_items_own_two_prices_is_projected()
    {
        typeof(CatalogueItemSummary).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["Id", "Code", "DescriptionAr", "DescriptionEn", "Unit", "BabId", "CostPrice", "BaseSellRate", "Status"],
                "AC-203-G: no total, no margin, no project or BOQ value — a whitelist, not a search for suspect words");

        typeof(Response).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Items"], "the wrapper carries the list and nothing else");
    }

    // ---- AC-206-A / AC-206-F · archived items are hidden by default and findable on request -----

    [Fact]
    public async Task An_archived_item_is_hidden_by_default_and_returned_when_asked_for()
    {
        Guid bab = await CreateBabAsync();
        string nonce = UniqueNames.Code("ARC");

        string active = await CreateItemAsync(bab, code: UniqueNames.Code($"{nonce}-A"));
        string archived = await CreateItemAsync(bab, code: UniqueNames.Code($"{nonce}-B"));

        await ArchiveAsync(archived);

        (await SearchAsyncAs(_owner, Role.Owner, null, nonce)).Select(item => item.Code).Should().BeEquivalentTo(
            [active],
            "KAFF-206 rule 7: the default search excludes archived items — this is also the search the "
            + "BOQ builder's \"add item\" reaches later, so this default is what keeps an archived item "
            + "off new work (`Q65`, D-130 §3) without the caller having to know to ask");

        IReadOnlyList<CatalogueItemSummary> all = await SearchAsyncAs(_owner, Role.Owner, null, nonce, status: "all");

        all.Select(item => item.Code).Should().BeEquivalentTo(
            [active, archived], "§4.5 keeps the row findable — it is archived, never deleted");

        all.Single(item => item.Code == archived).Status.Should().Be(CatalogueItemStatus.Archived);

        (await SearchAsyncAs(_owner, Role.Owner, null, nonce, status: "archived"))
            .Select(item => item.Code).Should().BeEquivalentTo(
                [archived],
                "three states, not a boolean — [ All ] [ Active ] [ Archived ] — D-111 §3 applied here "
                + "exactly as KAFF-124 applied it to clients");
    }

    /// <summary>An unknown filter is refused rather than quietly treated as the default.</summary>
    [Fact]
    public async Task A_filter_this_list_does_not_know_is_refused_and_not_defaulted()
    {
        HttpResponseMessage response = await SendAsync(
            _owner, Role.Owner, null, null, status: "archvied");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be(
            "errors.master.catalogue_item_list_filter_unknown",
            "a silently-defaulted wrong filter is indistinguishable from an empty archive");
    }

    [Fact]
    public void The_filter_has_exactly_the_three_states_the_screen_draws()
    {
        Enum.GetNames<CatalogueItemListFilter>().Should().BeEquivalentTo(
            ["Active", "Archived", "All"],
            "a fourth state here is a chip nobody drew, and a missing one is a chip that cannot work");
    }

    // ---- AC-203-I · ordered by باب, then by code -------------------------------------------------

    [Fact]
    public async Task Results_are_grouped_by_bab_then_ordered_by_code_within_each()
    {
        string nonce = UniqueNames.Code("FND-ORD");

        // Two أبواب, created out of order, so a code-only sort would interleave them.
        Guid babZ = await CreateBabAsync(sortOrder: 20, codePrefix: $"{nonce}-Z");
        Guid babA = await CreateBabAsync(sortOrder: 10, codePrefix: $"{nonce}-A");

        string zHigh = await CreateItemAsync(babZ, code: $"{nonce}-Z-9");
        string zLow = await CreateItemAsync(babZ, code: $"{nonce}-Z-1");
        string aHigh = await CreateItemAsync(babA, code: $"{nonce}-A-9");
        string aLow = await CreateItemAsync(babA, code: $"{nonce}-A-1");

        IReadOnlyList<CatalogueItemSummary> found = await SearchAsyncAs(_owner, Role.Owner, null, nonce);

        found.Select(item => item.Code).Should().Equal(
            [aLow, aHigh, zLow, zHigh],
            "grouped by باب in the باب's own SortOrder (babA before babZ), and within each باب ordered "
            + "by item code — D-129 §5, AC-203-I. The Arabic-collation tiebreak is not asserted here — "
            + "it is the Architect's per Q63's standing caveat");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private IEnumerable<(Guid Actor, Role Role, Department? Department)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance);
        yield return (_hr, Role.Hr, Department.Hr);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations);
        yield return (_marketing, Role.MarketingSales, Department.Marketing);
    }

    private async Task<Guid> CreateBabAsync(int sortOrder = 0, string? codePrefix = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(
            UniqueNames.Code(codePrefix ?? "FND-BAB"),
            "باب",
            "Bab",
            Percentage.FromPercent(15m),
            sortOrder: sortOrder).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task<string> CreateItemAsync(
        Guid babId, string code, string descriptionAr = "خرسانة عادية", decimal cost = 100m)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/catalogue-items", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                code,
                descriptionAr,
                unit = "م٣",
                babId,
                costPrice = cost,
                baseSellRate = cost + 50m,
            }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, _technicalOffice.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, Role.TechnicalOffice.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(_technicalOffice));
        request.Headers.Add(TestAuthHandler.DepartmentHeader, Department.Operations.ToString());

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return code;
    }

    /// <summary>
    /// Archives an item directly through the entity, bypassing the archive endpoint — this file owns
    /// the list, not the archive act, which has its own coverage in <c>ArchiveCatalogueItemTests</c>.
    /// Same shape as <c>ListClientsTests.ArchiveAsync</c>.
    /// </summary>
    private async Task ArchiveAsync(string code)
    {
        await using KaffDbContext context = _database.CreateContext();

        CatalogueItem item = await context.CatalogueItems.SingleAsync(candidate => candidate.Code == code, Ct);

        item.Archive().IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(Ct);
    }

    private async Task<IReadOnlyList<CatalogueItemSummary>> SearchAsync(string? search)
        => await SearchAsyncAs(_technicalOffice, Role.TechnicalOffice, Department.Operations, search);

    private async Task<IReadOnlyList<CatalogueItemSummary>> SearchAsyncAs(
        Guid actorId, Role actorRole, Department? actorDepartment, string? search, string? status = null)
    {
        HttpResponseMessage response = await SendAsync(actorId, actorRole, actorDepartment, search, status: status);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return
        [
            .. body.RootElement.GetProperty("items").EnumerateArray().Select(element => new CatalogueItemSummary(
                element.GetProperty("id").GetGuid(),
                element.GetProperty("code").GetString()!,
                element.GetProperty("descriptionAr").GetString()!,
                element.TryGetProperty("descriptionEn", out JsonElement en) ? en.GetString() : null,
                element.GetProperty("unit").GetString()!,
                element.GetProperty("babId").GetGuid(),
                element.GetProperty("costPrice").GetDecimal(),
                element.GetProperty("baseSellRate").GetDecimal(),
                Enum.Parse<CatalogueItemStatus>(element.GetProperty("status").GetString()!))),
        ];
    }

    private async Task<HttpResponseMessage> SendAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        string? search,
        Guid? actorClientId = null,
        string? status = null)
    {
        string route = "/api/catalogue-items";
        List<string> queryParts = [];

        if (search is not null)
        {
            queryParts.Add("search=" + Uri.EscapeDataString(search));
        }

        if (status is not null)
        {
            queryParts.Add("status=" + Uri.EscapeDataString(status));
        }

        if (queryParts.Count > 0)
        {
            route += "?" + string.Join('&', queryParts);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        if (actorClientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, actorClientId.Value.ToString());
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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("FND-C1"), "عميل بوابة الفهرس", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("fnd-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "fnd-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("fnd-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("fnd-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "fnd-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "fnd-design", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("fnd-marketing", Role.MarketingSales, Department.Marketing);
        User portal = MakeUser("fnd-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(
            owner, technicalOffice, finance, hr, siteEngineer, headOfDesign, marketing, portal);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment,
            clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
