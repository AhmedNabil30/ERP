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
/// <b><c>AC-202-E</c> and <c>AC-202-F</c>, as far as slice 2 can prove them.</b> Neither a signed BOQ
/// line nor an open estimate exists in this codebase yet — both are slice 4. What is provable now, and
/// what <see cref="Repricing_touches_no_row_but_the_items_own_and_writes_no_estimate_or_boq_row"/>
/// proves, is the mechanism's whole reach: a reprice writes exactly one entity row (this item) and one
/// audit row, and nothing else in the schema moves — the same "propagates to nothing" shape
/// <c>qa/slice-2/test-cases.md</c> uses for <c>TC-2-040</c> and <c>TC-2-053</c>. The BOQ/estimate half
/// of both criteria is re-driven when slice 4 ships those entities.
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
        body.RootElement.GetProperty("baseSellRate").GetDecimal().Should().Be(175m);

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

        before.RootElement.GetProperty(nameof(CatalogueItem.BaseSellRate)).GetDecimal().Should().Be(150m);
        after.RootElement.GetProperty(nameof(CatalogueItem.BaseSellRate)).GetDecimal().Should().Be(175m);
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

    // ---- AC-202-E / AC-202-F, the slice-2 half — reprice moves nothing but its own row -----------

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
            babCountBefore, "AC-202-E/F: repricing reaches nothing beyond this item's own row — there "
            + "is no BOQ or estimate table in this codebase yet for it to reach either");

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
