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
/// KAFF-206, D-130 §4 (<c>Q66</c>) — <c>POST /api/catalogue-items/{catalogueItemId}/unarchive</c>.
/// </summary>
/// <remarks>
/// <para>
/// No <c>AC-206-*</c> id and no <c>TC-2-*</c> id names this endpoint — <c>qa/slice-2/test-cases.md</c>
/// records the ruling and explicitly writes no case for it (<c>"NO STORY"</c>), because placement was
/// left to the Scrum Master and the endpoint did not exist when the file was written. It is built here
/// per the brief that placed it in this story; these tests are this session's own coverage of it,
/// against the shape the archive endpoint's own criteria already establish (audited, permission-gated,
/// the entity's own refusal).
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

    // ---- an archived item is brought back, and the trail names the change -------------------------

    [Fact]
    public async Task An_archived_item_is_unarchived_and_reappears_in_the_default_search()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, string code) = await CreateItemAsync(bab);

        (await ArchiveAsync(id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UnarchiveAsync(id, _technicalOffice, Role.TechnicalOffice, Department.Operations))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        CatalogueItem stored = await ReadAsync(id);
        stored.Status.Should().Be(CatalogueItemStatus.Active);
        stored.Code.Should().Be(code, "unarchiving touches Status and nothing else — the same shape Archive holds");

        (await SearchAsync(code)).Select(item => item.Code).Should().Contain(
            code, "codes are unique — Q66's whole reason for existing is that a mis-archived item must "
            + "return under the same code rather than a new one");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_technicalOffice, "un-archiving is a state change and is audited too");
        record.ChangedProperties.Should().Contain(nameof(CatalogueItem.Status));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(CatalogueItem.Status)).GetString().Should().Be(
            nameof(CatalogueItemStatus.Archived));
        after.RootElement.GetProperty(nameof(CatalogueItem.Status)).GetString().Should().Be(
            nameof(CatalogueItemStatus.Active));
    }

    // ---- unarchiving an item that is not archived is refused, and the refusal writes nothing ------

    [Fact]
    public async Task Unarchiving_an_active_item_is_refused_and_writes_no_audit_record()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, _) = await CreateItemAsync(bab);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        HttpResponseMessage response = await UnarchiveAsync(
            id, _technicalOffice, Role.TechnicalOffice, Department.Operations);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be(
            "errors.master.not_archived",
            "the refusal is CatalogueItem.Unarchive's — MasterDataErrors.NotArchived existed for "
            + "exactly this and, before this endpoint, was returned by nothing");

        (await ReadAsync(id)).Status.Should().Be(CatalogueItemStatus.Active, "a refused unarchive changes nothing");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(countBefore);
    }

    [Fact]
    public async Task Unarchiving_an_item_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await UnarchiveAsync(
            Guid.NewGuid(), _technicalOffice, Role.TechnicalOffice, Department.Operations);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.catalogue_item_not_found");
    }

    // ---- permission: same gate as archive -----------------------------------------------------

    [Fact]
    public async Task A_role_without_CatalogueManage_cannot_unarchive_an_item()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, _) = await CreateItemAsync(bab);

        await ArchiveAsync(id);

        (await UnarchiveAsync(id, _finance, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold CatalogueManage — spec.md §2, D-129 §1");

        (await ReadAsync(id)).Status.Should().Be(CatalogueItemStatus.Archived, "the refused call changed nothing");

        (await UnarchiveAsync(id, _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner holds every company-wide row — Q12, D-129 §1");
    }

    // ---- helpers ------------------------------------------------------------------------------

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

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, Department.Operations, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), code);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(Guid itemId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/catalogue-items/{itemId}/archive", UriKind.Relative));

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, Department.Operations, null);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> UnarchiveAsync(
        Guid itemId, Guid actorId, Role actorRole, Department? actorDepartment)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/catalogue-items/{itemId}/unarchive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, null);

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

        User owner = MakeUser("unai-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "unai-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("unai-finance", Role.Finance, Department.Finance);

        context.Users.AddRange(owner, technicalOffice, finance);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment)
            .Value;

    private static DateTimeOffset Now => new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
