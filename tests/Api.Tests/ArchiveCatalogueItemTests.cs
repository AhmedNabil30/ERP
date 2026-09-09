using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Features.Catalogue.ListCatalogueItems;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-206 — <c>POST /api/catalogue-items/{catalogueItemId}/archive</c>.
/// </summary>
/// <remarks>
/// Same discipline as <c>ArchiveClientTests</c>: every test goes through HTTP, against fixtures
/// nonced to this class, because the database is shared across the collection.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ArchiveCatalogueItemTests : IAsyncLifetime
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

    public ArchiveCatalogueItemTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-206-A / AC-206-H · archived, still there, and the trail names the change -------------

    [Fact]
    public async Task An_item_is_archived_the_row_survives_and_the_trail_names_the_change()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, string code) = await CreateItemAsync(bab);

        (await ArchiveAsync(id, _technicalOffice, Role.TechnicalOffice, Department.Operations))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        CatalogueItem stored = await ReadAsync(id);
        stored.Status.Should().Be(CatalogueItemStatus.Archived);
        stored.Code.Should().Be(code, "AC-206-A: archiving touches Status and nothing else");

        (await SearchAsync(code)).Select(item => item.Code).Should().NotContain(
            code, "the default search excludes archived items — KAFF-206 rule 7");

        (await SearchAsync(code, status: "archived")).Select(item => item.Code).Should().Contain(
            code, "reachable through the list's explicit archived filter — AC-206-A");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.EntityType.Should().Be(nameof(CatalogueItem));
        record.ActorUserId.Should().Be(_technicalOffice, "the trail names who archived it — AC-206-H");
        record.ChangedProperties.Should().Contain(nameof(CatalogueItem.Status));
        record.GrantPath.Should().BeNull("CatalogueManage is company-wide — no project, no path to name");

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(CatalogueItem.Status)).GetString().Should().Be(
            nameof(CatalogueItemStatus.Active));
        after.RootElement.GetProperty(nameof(CatalogueItem.Status)).GetString().Should().Be(
            nameof(CatalogueItemStatus.Archived));
    }

    // ---- AC-206-E / AC-206-H · archiving twice is refused, and the refusal writes nothing --------

    [Fact]
    public async Task Archiving_an_item_twice_is_refused_and_the_refusal_writes_no_audit_record()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, _) = await CreateItemAsync(bab);

        (await ArchiveAsync(id, _technicalOffice, Role.TechnicalOffice, Department.Operations))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        HttpResponseMessage again = await ArchiveAsync(
            id, _technicalOffice, Role.TechnicalOffice, Department.Operations);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using JsonDocument problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be(
            "errors.master.already_archived",
            "the refusal is CatalogueItem.Archive's — a second copy of the rule in the handler is the "
            + "copy that drifts from the entity every other caller goes through");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(
            countBefore, "AC-206-H: the refused second archive produces no record at all");
    }

    [Fact]
    public async Task Archiving_an_item_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await ArchiveAsync(
            Guid.NewGuid(), _technicalOffice, Role.TechnicalOffice, Department.Operations);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.catalogue_item_not_found");
    }

    // ---- AC-206-B · no delete route on the catalogue-items surface, with a positive control ------

    /// <summary>
    /// <c>ArchiveClientTests.No_endpoint_in_the_application_deletes_anything</c> already scans every
    /// route the whole assembly maps, catalogue included. This narrows the same assertion to
    /// <c>/api/catalogue-items</c> because TC-2-058 names the catalogue endpoints specifically, and
    /// adds the positive control the case itself asks for: the archive route on this same prefix does
    /// exist and does change <c>status</c> — proved above, in
    /// <see cref="An_item_is_archived_the_row_survives_and_the_trail_names_the_change"/> — so this
    /// allow-list enumeration is exercised against a real, populated route table.
    /// </summary>
    [Fact]
    public void No_route_under_catalogue_items_deletes_anything()
    {
        Assembly shipped = typeof(PermissionRequirement).Assembly;

        List<string> catalogueRoutes = [];
        List<string> deleteRoutes = [];

        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (Microsoft.AspNetCore.Http.Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint route)
                {
                    continue;
                }

                Assembly? handler = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly;

                if (handler is not null && handler != shipped)
                {
                    continue;
                }

                string pattern = route.RoutePattern.RawText ?? string.Empty;

                if (!pattern.StartsWith("/api/catalogue-items", StringComparison.Ordinal))
                {
                    continue;
                }

                catalogueRoutes.Add(pattern);

                HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

                if (methods?.HttpMethods.Contains(HttpMethods.Delete, StringComparer.OrdinalIgnoreCase) == true)
                {
                    deleteRoutes.Add(pattern);
                }
            }
        }

        catalogueRoutes.Should().NotBeEmpty(
            "the enumeration must find real, mapped catalogue routes — an empty table would pass this "
            + "test regardless of what exists (D-116, V-32-A's lesson)");

        deleteRoutes.Should().BeEmpty(
            "a catalogue item is archived and never deleted — CLAUDE.md, KAFF-123's precedent, this "
            + "story's own title");
    }

    // ---- AC-206-G · nobody outside the Owner and the Technical Office may archive -----------------

    [Fact]
    public async Task Only_the_technical_office_and_the_owner_may_archive_an_item()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, _) = await CreateItemAsync(bab);

        foreach ((Guid actor, Role role, Department? department) in RefusedActors())
        {
            (await ArchiveAsync(id, actor, role, department))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden, "{0} does not hold CatalogueManage — spec.md §2, D-129 §1", role);
        }

        (await ArchiveAsync(id, _portalClient, Role.Client, null, actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "spec.md §12 — a portal client reaches none of the internal catalogue surface");

        (await ReadAsync(id)).Status.Should().Be(
            CatalogueItemStatus.Active, "not one of the six refused calls changed anything");

        (await ArchiveAsync(id, _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner holds every company-wide row — Q12, D-129 §1");
    }

    /// <summary>Every role QA's TC-2-063 names that can structurally reach a company-wide staff route.</summary>
    [Fact]
    public void The_refused_list_is_every_reachable_role_TC_2_063_names()
    {
        RefusedActors().Select(actor => actor.Role).Should().BeEquivalentTo(
            [Role.Finance, Role.SiteEngineer, Role.HeadOfDesign, Role.MarketingSales, Role.Hr],
            "TC-2-063 names Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor "
            + "and Hr; Client is covered separately (portal session) and Subcontractor cannot sign in "
            + "at all (spec.md §9), so both are structurally refused rather than asserted here — the "
            + "same split ListCatalogueItemsTests and CreateCatalogueItemTests use");
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

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(
            UniqueNames.Code("ARCI-BAB"), "باب", "Bab", Percentage.FromPercent(15m), sortOrder: 0).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task<(Guid Id, string Code)> CreateItemAsync(Guid babId)
    {
        string code = UniqueNames.Code("ARCI");

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

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, Department.Operations, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), code);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(
        Guid itemId, Guid actorId, Role actorRole, Department? actorDepartment, Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/catalogue-items/{itemId}/archive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<IReadOnlyList<CatalogueItemSummary>> SearchAsync(string search, string status = "active")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"/api/catalogue-items?status={status}&search={Uri.EscapeDataString(search)}", UriKind.Relative));

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, Department.Operations, null);

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
                element.GetProperty("costPrice").GetDecimal(),
                element.GetProperty("baseSellRate").GetDecimal(),
                Enum.Parse<CatalogueItemStatus>(element.GetProperty("status").GetString()!))),
        ];
    }

    private async Task StampAsync(
        HttpRequestMessage request, Guid actorId, Role actorRole, Department? actorDepartment, Guid? actorClientId)
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
            UniqueNames.Code("ARCI-C1"), "عميل بوابة الأرشفة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("arci-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "arci-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("arci-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("arci-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "arci-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "arci-design", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("arci-marketing", Role.MarketingSales, Department.Marketing);
        User portal = MakeUser("arci-portal", Role.Client, clientId: company.Id);

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
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment, clientId)
            .Value;

    private static DateTimeOffset Now => new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
