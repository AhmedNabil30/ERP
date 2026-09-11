using System.Globalization;
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
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-202 — <c>PUT /api/catalogue-items/{catalogueItemId}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every test here goes through HTTP. The mutation surface itself (<c>SetDescription</c>,
/// <c>SetUnit</c>, <c>Reprice</c>) is pinned in <c>Domain.Tests/CatalogueItemEditingTests.cs</c>; what
/// can only be observed at this level is the permission gate and what the audit trail holds after a
/// correction.
/// </para>
/// <para>
/// <b>What <see cref="Repricing_touches_no_row_but_the_items_own_and_writes_no_estimate_or_boq_row"/>
/// actually proves, and what it does not (<c>V-35-N</c>).</b> It proves the reprice mechanism's whole
/// reach: a reprice writes exactly one entity row (this item) and one audit row, and nothing else in
/// the schema moves — the same "propagates to nothing" shape <c>qa/slice-2/test-cases.md</c> uses for
/// <c>TC-2-040</c> and <c>TC-2-053</c>. <b>It does not discharge <c>AC-202-E</c> or <c>AC-202-F</c></b>
/// — neither a signed BOQ line nor an open estimate exists in this codebase yet, so its أبواب-row count
/// could not move under any implementation of reprice and is not a witness for either criterion.
/// <c>TC-2-022</c> and <c>TC-2-023</c> are held to slice 4, the same <c>TC-2-059</c>/<c>TC-2-060</c>
/// shape.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class EditCatalogueItemTests : IAsyncLifetime
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
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public EditCatalogueItemTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-202-I · a description and a price edited together are audited together ---------------

    [Fact]
    public async Task A_description_and_a_sell_rate_edited_together_are_one_audited_change()
    {
        Guid id = await RegisterAsync(cost: 100m, sell: 150m);

        HttpResponseMessage response = await EditAsync(
            id, Body(descriptionAr: "خرسانة مسلحة معدلة", unit: "م٣", cost: 100m, sell: 175m));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("descriptionAr").GetString().Should().Be("خرسانة مسلحة معدلة");
        WireDecimal(body.RootElement.GetProperty("baseSellRate")).Should().Be(175m);

        await using KaffDbContext stored = _database.CreateBareContext();
        CatalogueItem persisted = await stored.CatalogueItems.SingleAsync(i => i.Id == id, Ct);
        persisted.DescriptionAr.Should().Be("خرسانة مسلحة معدلة", "the row itself, not only the response, must carry the edit");
        persisted.BaseSellRate.Amount.Should().Be(175m, "the row itself, not only the response, must carry the edit");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(record => record.EntityId == id && record.Action == AuditAction.Modified)
            .OrderByDescending(record => record.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_technicalOffice);
        record.ChangedProperties.Should().Contain(nameof(CatalogueItem.DescriptionAr));
        record.ChangedProperties.Should().Contain(nameof(CatalogueItem.BaseSellRate));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        WireDecimal(before.RootElement.GetProperty(nameof(CatalogueItem.BaseSellRate))).Should().Be(150m);
        WireDecimal(after.RootElement.GetProperty(nameof(CatalogueItem.BaseSellRate))).Should().Be(175m);

        before.RootElement.GetProperty(nameof(CatalogueItem.DescriptionAr)).GetString().Should().Be(
            "خرسانة عادية", "V-36-F: AC-202-I asserts old and new values for BOTH edited fields");
        after.RootElement.GetProperty(nameof(CatalogueItem.DescriptionAr)).GetString().Should().Be(
            "خرسانة مسلحة معدلة", "V-36-F: AC-202-I asserts old and new values for BOTH edited fields");
    }

    [Fact]
    public async Task A_refused_edit_writes_no_audit_record_and_changes_nothing()
    {
        Guid id = await RegisterAsync(cost: 100m, sell: 150m);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(Ct);

        (await SendAsync(HttpMethod.Put, $"/api/catalogue-items/{id}", _finance, Role.Finance, Department.Finance, Body(sell: 999m)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.AuditRecords.LongCountAsync(Ct)).Should().Be(countBefore, "a refused edit is not an edit");

        CatalogueItem unchanged = await reader.CatalogueItems.SingleAsync(item => item.Id == id, Ct);
        unchanged.BaseSellRate.Amount.Should().Be(150m, "nothing changed for a refusal that never reached the handler");
    }

    // ---- AC-202-D applied on the edit path, and its positive control -----------------------------

    [Fact]
    public async Task A_negative_cost_price_is_refused_on_edit()
    {
        Guid id = await RegisterAsync();

        HttpResponseMessage response = await EditAsync(id, Body(cost: -1m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.cost_price_negative");
    }

    [Fact]
    public async Task A_loss_making_sell_rate_is_accepted_on_edit()
    {
        Guid id = await RegisterAsync(cost: 200m, sell: 300m);

        (await EditAsync(id, Body(cost: 200m, sell: 100m)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "nothing in spec.md forbids re-pricing into a loss");
    }

    // ---- V-36-H · an omitted price on edit is refused, not defaulted to zero ---------------------

    [Fact]
    public async Task An_omitted_cost_price_is_refused_on_edit_not_defaulted_to_zero()
    {
        Guid id = await RegisterAsync(cost: 100m, sell: 150m);

        HttpResponseMessage response = await EditAsync(
            id, new { descriptionAr = "خرسانة عادية", descriptionEn = (string?)null, unit = "م٣", baseSellRate = 150m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.cost_price_required");

        (await ReadAsync(id)).CostPrice.Amount.Should().Be(100m, "a refused edit changes nothing");
    }

    [Fact]
    public async Task An_omitted_sell_rate_is_refused_on_edit_not_defaulted_to_zero()
    {
        Guid id = await RegisterAsync(cost: 100m, sell: 150m);

        HttpResponseMessage response = await EditAsync(
            id, new { descriptionAr = "خرسانة عادية", descriptionEn = (string?)null, unit = "م٣", costPrice = 100m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.sell_rate_required");
    }

    [Fact]
    public async Task An_explicit_zero_price_is_accepted_on_edit()
    {
        Guid id = await RegisterAsync(cost: 100m, sell: 150m);

        (await EditAsync(id, Body(cost: 0m, sell: 0m)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "AC-202-D refuses only negatives — an explicit zero is legal");
    }

    // ---- Reprice's whole reach: its own row and one audit record — AC-202-E/F held to slice 4,
    // TC-2-022/TC-2-023 (V-35-N) ----------------------------------------------------------------

    [Fact]
    public async Task Repricing_touches_no_row_but_the_items_own_and_writes_no_estimate_or_boq_row()
    {
        Guid id = await RegisterAsync(cost: 100m, sell: 150m);

        await using KaffDbContext before = _database.CreateBareContext();
        long auditCountBefore = await before.AuditRecords.LongCountAsync(Ct);
        long itemCountBefore = await before.CatalogueItems.LongCountAsync(Ct);
        long babCountBefore = await before.Babs.LongCountAsync(Ct);

        (await EditAsync(id, Body(cost: 400m, sell: 600m)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext after = _database.CreateBareContext();

        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(
            auditCountBefore + 1, "exactly one audit record for one reprice — no alert, no side event");

        (await after.CatalogueItems.LongCountAsync(Ct)).Should().Be(
            itemCountBefore, "no item is created or removed by a reprice");

        (await after.Babs.LongCountAsync(Ct)).Should().Be(
            babCountBefore, "repricing reaches nothing beyond this item's own row — the mechanism's "
            + "whole reach, not a witness for AC-202-E/F, which are held to slice 4 as TC-2-022/"
            + "TC-2-023 because no BOQ or estimate table exists in this codebase yet (V-35-N)");

        CatalogueItem stored = await after.CatalogueItems.SingleAsync(item => item.Id == id, Ct);
        stored.CostPrice.Amount.Should().Be(400m);
        stored.BaseSellRate.Amount.Should().Be(600m);
    }

    // ---- AC-202-G · cost price never leaves the internal surface ---------------------------------

    [Fact]
    public void No_portal_route_is_mapped_anywhere_in_the_application()
    {
        // The absence half. If a portal surface is ever added, it must not be able to project
        // CostPrice — but today there is no /api/portal/* route at all, so the strongest true
        // statement is that the surface does not exist to leak from.
        IEnumerable<string> portalRoutes = ShippedRoutes()
            .Where(route => route.Contains("/api/portal", StringComparison.OrdinalIgnoreCase));

        portalRoutes.Should().BeEmpty(
            "AC-202-G: costPrice must appear on no /api/portal/* response, which today holds "
            + "vacuously because no such route is mapped");
    }

    /// <summary>
    /// Positive control (D-116): this endpoint's own response DOES carry <c>CostPrice</c>, which is
    /// what proves the absence check above is looking at something rather than passing because
    /// nothing anywhere ever returns the field. S-018 is the internal (TO/Owner) surface, not a
    /// portal one — carrying the field here is correct, not a leak.
    /// </summary>
    [Fact]
    public void The_internal_edit_response_does_carry_cost_price()
    {
        typeof(Kaff.Api.Features.Catalogue.EditCatalogueItem.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().Contain(nameof(CatalogueItem.CostPrice));
    }

    private IEnumerable<string> ShippedRoutes()
    {
        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (Microsoft.AspNetCore.Http.Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is RouteEndpoint route)
                {
                    yield return route.RoutePattern.RawText ?? string.Empty;
                }
            }
        }
    }

    // ---- AC-202-H · a role without CatalogueManage reaches neither endpoint (edit half) ----------

    [Fact]
    public async Task Only_technical_office_and_the_owner_may_edit_a_catalogue_item()
    {
        Guid id = await RegisterAsync();

        foreach ((Guid actor, Role role, Department? department) in RefusedActors())
        {
            (await SendAsync(HttpMethod.Put, $"/api/catalogue-items/{id}", actor, role, department, Body()))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden, "{0} does not hold CatalogueManage — spec.md §2, §4.1", role);
        }

        (await SendAsync(
                HttpMethod.Put, $"/api/catalogue-items/{id}", _portalClient, Role.Client, null, Body(),
                actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "spec.md §12 — a portal client reaches none of the internal catalogue surface — TC-2-025, V-35-T");

        (await ReadAsync(id)).BaseSellRate.Amount.Should().Be(
            150m, "not one of the six refused calls, including the Client's, changed anything");

        (await SendAsync(HttpMethod.Put, $"/api/catalogue-items/{id}", _owner, Role.Owner, null, Body()))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row — D-129 §1");
    }

    // ---- the route addresses a row by id, so it has to answer when the id names nobody -----------

    [Fact]
    public async Task Editing_an_item_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await EditAsync(Guid.NewGuid(), Body());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.master.catalogue_item_not_found");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(
        string descriptionAr = "خرسانة عادية", string unit = "م٣", decimal cost = 100m, decimal sell = 150m) => new
        {
            descriptionAr,
            descriptionEn = (string?)null,
            unit,
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

    private async Task<Guid> RegisterAsync(decimal cost = 100m, decimal sell = 150m)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            "/api/catalogue-items",
            _technicalOffice,
            Role.TechnicalOffice,
            Department.Operations,
            new
            {
                code = UniqueNames.Code("EDT"),
                descriptionAr = "خرسانة عادية",
                unit = "م٣",
                babId = _babId,
                costPrice = cost,
                baseSellRate = sell,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> EditAsync(Guid id, object body)
        => SendAsync(HttpMethod.Put, $"/api/catalogue-items/{id}", _technicalOffice, Role.TechnicalOffice, Department.Operations, body);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        object body,
        Guid? actorClientId = null)
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

        if (actorClientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, actorClientId.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<CatalogueItem> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.CatalogueItems.SingleAsync(item => item.Id == id, Ct);
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    /// <summary>
    /// A decimal off the wire, per decisions.md D-135: it travels as a JSON string in both
    /// directions, but a pre-ruling audit snapshot may still hold a bare number, so both are read.
    /// </summary>
    private static decimal WireDecimal(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
        : element.GetDecimal();

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

        Bab bab = Bab.Create(UniqueNames.Code("EDT-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        Client company = Client.Create(
            UniqueNames.Code("EDT-C1"), "عميل بوابة التعديل", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("edt-cat-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "edt-cat-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("edt-cat-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("edt-cat-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "edt-cat-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "edt-cat-design", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("edt-cat-marketing", Role.MarketingSales, Department.Marketing);
        User portal = MakeUser("edt-cat-portal", Role.Client, clientId: company.Id);

        context.Babs.Add(bab);
        context.Clients.Add(company);
        context.Users.AddRange(
            owner, technicalOffice, finance, hr, siteEngineer, headOfDesign, marketing, portal);

        await context.SaveChangesAsync(Ct);

        _babId = bab.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
        _portalClientCompany = company.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment, clientId)
            .Value;

    private static DateTimeOffset Now => new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
