using System.Globalization;
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
/// KAFF-214, D-130 §4 (<c>Q66</c>) — <c>POST /api/catalogue-items/{catalogueItemId}/unarchive</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>UnarchiveCatalogueItem</c> shipped inside <c>f675f1b</c>/<c>934bfb9</c>, attributed at the time to
/// <c>KAFF-206</c>, with no <c>AC-</c> id and no <c>TC-2-</c> id (<c>V-35-U</c>). The Scrum Master cut
/// <c>KAFF-214</c> as its own retrospective story (D-133 §1) to give the shipped code a criterion:
/// <c>AC-214-A</c> … <c>AC-214-E</c>, cased as <c>TC-2-099</c> … <c>TC-2-103</c>
/// (<c>qa/slice-2/test-cases.md</c>). These tests trace to those ids.
/// </para>
/// <para>
/// The delete-route allow-list in <c>ArchiveCatalogueItemTests.No_route_under_catalogue_items_deletes_anything</c>
/// already covers this route too — it enumerates the whole <c>/api/catalogue-items</c> prefix, not
/// only the archive sub-resource.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class UnarchiveCatalogueItemTests : IAsyncLifetime
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

    public UnarchiveCatalogueItemTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-214-A / TC-2-099 · an archived item is brought back with every §4.1 field unchanged,
    // and the trail names the change (AC-214-D / TC-2-102's positive half) ------------------------

    [Fact]
    public async Task An_archived_item_is_unarchived_and_reappears_in_the_default_search()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, string code) = await CreateItemAsync(bab);

        CatalogueItem original = await ReadAsync(id);
        string descriptionAr = original.DescriptionAr;
        string unit = original.Unit;
        Guid babId = original.BabId;
        decimal costPrice = original.CostPrice.Amount;
        decimal baseSellRate = original.BaseSellRate.Amount;

        (await ArchiveAsync(id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UnarchiveAsync(id, _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        CatalogueItem stored = await ReadAsync(id);
        stored.Status.Should().Be(CatalogueItemStatus.Active);
        stored.Code.Should().Be(code, "AC-214-A: unarchiving touches Status and nothing else — the same shape Archive holds");
        stored.DescriptionAr.Should().Be(
            descriptionAr, "AC-214-A/TC-2-099: every §4.1 field survives un-archiving, not only status and code");
        stored.Unit.Should().Be(unit, "AC-214-A/TC-2-099");
        stored.BabId.Should().Be(babId, "AC-214-A/TC-2-099");
        stored.CostPrice.Amount.Should().Be(costPrice, "AC-214-A/TC-2-099");
        stored.BaseSellRate.Amount.Should().Be(baseSellRate, "AC-214-A/TC-2-099");

        (await SearchAsync(code)).Select(item => item.Code).Should().Contain(
            code, "codes are unique — Q66's whole reason for existing is that a mis-archived item must "
            + "return under the same code rather than a new one");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_technicalOffice, "AC-214-D: un-archiving is a state change and is audited too");
        record.ChangedProperties.Should().Contain(nameof(CatalogueItem.Status));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(CatalogueItem.Status)).GetString().Should().Be(
            nameof(CatalogueItemStatus.Archived));
        after.RootElement.GetProperty(nameof(CatalogueItem.Status)).GetString().Should().Be(
            nameof(CatalogueItemStatus.Active));
    }

    // ---- AC-214-B / TC-2-100 · unarchiving an item that is not archived is refused, and the
    // refusal writes nothing, scoped to the item's own EntityId (AC-214-D's refusal half, TC-2-102,
    // the D-116/TC-2-064 shape ArchiveCatalogueItemTests uses) --------------------------------------

    [Fact]
    public async Task Unarchiving_an_active_item_is_refused_and_writes_no_audit_record()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, _) = await CreateItemAsync(bab);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        HttpResponseMessage response = await UnarchiveAsync(
            id, _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be(
            "errors.master.not_archived",
            "AC-214-B: the refusal is CatalogueItem.Unarchive's — MasterDataErrors.NotArchived existed "
            + "for exactly this and, before this endpoint, was returned by nothing");

        (await ReadAsync(id)).Status.Should().Be(CatalogueItemStatus.Active, "a refused unarchive changes nothing");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(
            countBefore,
            "AC-214-D/TC-2-102: scoped to this item's own EntityId, not an unscoped global count that "
            + "could hide a stray write to a different row");
    }

    [Fact]
    public async Task Unarchiving_an_item_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await UnarchiveAsync(
            Guid.NewGuid(), _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.catalogue_item_not_found");
    }

    // ---- AC-214-C / TC-2-101 · same gate as archive, executed for every refused role including a
    // portal Client session, not asserted in a comment (the V-35-T shape KAFF-214's own story records) --

    [Fact]
    public async Task A_role_without_CatalogueManage_cannot_unarchive_an_item()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, _) = await CreateItemAsync(bab);

        await ArchiveAsync(id);

        foreach ((Guid actor, Role role, Guid? department) in RefusedActors())
        {
            (await UnarchiveAsync(id, actor, role, department))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden, "{0} does not hold CatalogueManage — spec.md §2, D-129 §1", role);
        }

        (await UnarchiveAsync(id, _portalClient, Role.Client, null, actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "AC-214-C/TC-2-101: spec.md §12 — a portal client reaches none of the internal catalogue "
                + "surface, executed as a real request rather than asserted in a comment");

        (await ReadAsync(id)).Status.Should().Be(
            CatalogueItemStatus.Archived, "not one of the six refused calls, including the Client's, changed anything");

        (await UnarchiveAsync(id, _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner holds every company-wide row — Q12, D-129 §1");
    }

    /// <summary>Every role QA's TC-2-101 names that can structurally reach a company-wide staff route.</summary>
    [Fact]
    public void The_refused_list_is_every_reachable_role_TC_2_101_names()
    {
        RefusedActors().Select(actor => actor.Role).Should().BeEquivalentTo(
            [Role.Finance, Role.SiteEngineer, Role.HeadOfDesign, Role.MarketingSales, Role.Hr],
            "TC-2-101 names Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor "
            + "and Hr; Client is covered separately (portal session, executed above) and Subcontractor "
            + "cannot sign in at all (spec.md §9), so both are structurally refused rather than asserted "
            + "here — the same split ArchiveCatalogueItemTests uses");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private IEnumerable<(Guid Actor, Role Role, Guid? Department)> RefusedActors()
    {
        yield return (_finance, Role.Finance, WellKnownDepartments.FinanceId);
        yield return (_hr, Role.Hr, WellKnownDepartments.HrId);
        yield return (_siteEngineer, Role.SiteEngineer, WellKnownDepartments.OperationsId);
        yield return (_headOfDesign, Role.HeadOfDesign, WellKnownDepartments.OperationsId);
        yield return (_marketing, Role.MarketingSales, null);
    }

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(
            UniqueNames.Code("UNAI-BAB"), "باب", "Bab", Percentage.FromPercent(15m), sortOrder: 0).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task<(Guid Id, string Code)> CreateItemAsync(Guid babId)
    {
        string code = UniqueNames.Code("UNAI");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/catalogue-items", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                code,
                descriptionAr = "خرسانة عادية",
                unit = "م٣",
                babId,
                costPrice = 100m,
                baseSellRate = 150m,
            }),
        };

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), code);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(Guid itemId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/catalogue-items/{itemId}/archive", UriKind.Relative));

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId, null);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> UnarchiveAsync(
        Guid itemId, Guid actorId, Role actorRole, Guid? actorDepartment, Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/catalogue-items/{itemId}/unarchive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<IReadOnlyList<CatalogueItemSummary>> SearchAsync(string search, string status = "active")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"/api/catalogue-items?status={status}&search={Uri.EscapeDataString(search)}", UriKind.Relative));

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

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
                WireDecimal(element.GetProperty("costPrice")),
                WireDecimal(element.GetProperty("baseSellRate")),
                Enum.Parse<CatalogueItemStatus>(element.GetProperty("status").GetString()!))),
        ];
    }

    /// <summary>
    /// A decimal off the wire, per decisions.md D-135: it travels as a JSON string in both
    /// directions, but a pre-ruling audit snapshot may still hold a bare number, so both are read.
    /// </summary>
    private static decimal WireDecimal(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
        : element.GetDecimal();

    private async Task StampAsync(
        HttpRequestMessage request, Guid actorId, Role actorRole, Guid? actorDepartment, Guid? actorClientId)
    {
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
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private async Task<CatalogueItem> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.CatalogueItems.SingleAsync(item => item.Id == id, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("UNAI-C1"), "عميل بوابة إلغاء الأرشفة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("unai-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "unai-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("unai-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User hr = MakeUser("unai-hr", Role.Hr, WellKnownDepartments.HrId);
        User siteEngineer = MakeUser(
            "unai-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "unai-design", Role.HeadOfDesign, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User marketing = MakeUser("unai-marketing", Role.MarketingSales, null);
        User portal = MakeUser("unai-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(
            owner, technicalOffice, finance, hr, siteEngineer, headOfDesign, marketing, portal);

        await context.SaveChangesAsync(Ct);

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
        Guid? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment, clientId)
            .Value;

    private static DateTimeOffset Now => new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
