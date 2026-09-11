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
/// KAFF-213 — <c>POST /api/babs/{babId}/archive</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ArchiveBabTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;

    public ArchiveBabTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-213-A · a باب with no active items is archived ------------------------------------------

    [Fact]
    public async Task A_bab_with_no_items_is_archived()
    {
        Guid bab = await CreateBabAsync();

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(candidate => candidate.Id == bab, Ct)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task A_bab_whose_items_are_all_already_archived_is_archived()
    {
        Guid bab = await CreateBabAsync();
        await CreateItemAsync(bab, archived: true);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_archived_bab_is_reachable_through_the_trees_explicit_archived_filter()
    {
        string code = await CreateBabWithCodeAsync();
        Guid bab = await ResolveBabIdAsync(code);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListBabsAsync(status: "active")).Should().NotContain(
            code, "AC-213-A: the default excludes an archived باب");
        (await ListBabsAsync(status: "archived")).Should().Contain(
            code, "AC-213-A: it is reachable through the explicit archived filter");
    }

    // ---- AC-213-F · an archived باب's already-archived items stay findable --------------------------

    [Fact]
    public async Task An_archived_babs_already_archived_items_stay_findable()
    {
        Guid bab = await CreateBabAsync();
        (Guid itemId, string itemCode) = await CreateItemWithCodeAsync(bab, archived: true);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument found = JsonDocument.Parse(
            await (await SendAsync(HttpMethod.Get, $"/api/catalogue-items?search={itemCode}&status=archived", _owner, Role.Owner, null))
                .Content.ReadAsStringAsync(Ct));

        JsonElement[] items = [.. found.RootElement.GetProperty("items").EnumerateArray()];

        items.Should().ContainSingle(item => item.GetProperty("code").GetString() == itemCode,
            "AC-213-F: the item stays findable through the catalogue's own explicit archived filter");
        items.Single(item => item.GetProperty("code").GetString() == itemCode)
            .GetProperty("babId").GetGuid().Should().Be(bab, "it still carries the باب that was archived");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CatalogueItems.SingleAsync(i => i.Id == itemId, Ct)).BabId.Should().Be(bab);
    }

    // ---- AC-213-B · a باب holding active items cannot be archived, and the refusal names the count --

    [Fact]
    public async Task A_bab_with_active_items_cannot_be_archived_and_the_refusal_names_the_count()
    {
        Guid bab = await CreateBabAsync();

        for (int i = 0; i < 12; i++)
        {
            await CreateItemAsync(bab);
        }

        HttpResponseMessage response = await ArchiveAsync(bab);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.master.bab_has_active_items");

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("count").GetInt32().Should().Be(12);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(candidate => candidate.Id == bab, Ct)).IsActive.Should().BeTrue();
    }

    // ---- AC-213-C · the count is the باب's own items, not its subtree's -----------------------------

    [Fact]
    public async Task The_active_item_count_is_the_babs_own_not_its_subtrees()
    {
        Guid parent = await CreateBabAsync();
        Guid child = await CreateBabAsync(parent: parent);
        await CreateItemAsync(child);
        await CreateItemAsync(child);
        await CreateItemAsync(child);

        (await ArchiveAsync(parent)).StatusCode.Should().Be(
            HttpStatusCode.NoContent, "the parent has no items of its own — the child's 3 items are not counted");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(candidate => candidate.Id == child, Ct)).IsActive.Should().BeTrue(
            "the child باب is completely unaffected");
    }

    // ---- AC-213-D · archiving a باب is not a cascade -------------------------------------------------

    [Fact]
    public async Task Archiving_a_bab_touches_no_item_and_no_child_bab()
    {
        Guid parent = await CreateBabAsync();
        Guid child = await CreateBabAsync(parent: parent);
        Guid archivedItem = await CreateItemAsync(parent, archived: true);

        (await ArchiveAsync(parent)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.Babs.SingleAsync(candidate => candidate.Id == child, Ct)).ParentBabId.Should().Be(
            parent, "no child باب is re-parented");
        (await reader.Babs.SingleAsync(candidate => candidate.Id == child, Ct)).IsActive.Should().BeTrue();
        (await reader.CatalogueItems.SingleAsync(item => item.Id == archivedItem, Ct)).Status.Should().Be(
            CatalogueItemStatus.Archived, "unchanged — it was already archived, this story did not touch it");
    }

    // ---- AC-213-E · moving or archiving the items first clears the refusal -------------------------

    [Fact]
    public async Task Archiving_the_active_items_first_clears_the_refusal()
    {
        Guid bab = await CreateBabAsync();
        Guid item = await CreateItemAsync(bab);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await ArchiveItemAsync(item);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent, "no active items remain");
    }

    [Fact]
    public async Task Moving_the_active_items_elsewhere_first_clears_the_refusal()
    {
        Guid bab = await CreateBabAsync();
        Guid elsewhere = await CreateBabAsync();
        Guid item = await CreateItemAsync(bab);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await MoveItemAsync(item, elsewhere);

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent, "no active items remain under it");
    }

    // ---- AC-213-G · archiving twice is refused, and writes nothing ---------------------------------

    [Fact]
    public async Task Archiving_a_bab_twice_is_refused_and_writes_no_audit_record()
    {
        Guid bab = await CreateBabAsync();

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(r => r.EntityId == bab, Ct);

        HttpResponseMessage again = await ArchiveAsync(bab);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(again)).Should().Be("errors.master.already_archived");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(r => r.EntityId == bab, Ct)).Should().Be(
            countBefore, "the refused re-archive writes no record");
    }

    // ---- AC-213-H · a role without BabManage archives nothing --------------------------------------

    [Fact]
    public async Task Only_the_technical_office_and_the_owner_may_archive_a_bab()
    {
        Guid bab = await CreateBabAsync();

        (await SendAsync(HttpMethod.Post, $"/api/babs/{bab}/archive", _finance, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold BabManage");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(candidate => candidate.Id == bab, Ct)).IsActive.Should().BeTrue();

        (await SendAsync(HttpMethod.Post, $"/api/babs/{bab}/archive", _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner holds every company-wide row");
    }

    // ---- AC-213-I · archiving is audited before and after -------------------------------------------

    [Fact]
    public async Task Archiving_is_audited_with_the_old_and_new_status()
    {
        Guid bab = await CreateBabAsync();

        (await ArchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(r => r.EntityId == bab && r.Action == AuditAction.Modified)
            .OrderByDescending(r => r.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_technicalOffice);
        record.ChangedProperties.Should().Contain(nameof(Bab.IsActive));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Bab.IsActive)).GetBoolean().Should().BeTrue();
        after.RootElement.GetProperty(nameof(Bab.IsActive)).GetBoolean().Should().BeFalse();
    }

    // ---- helpers ------------------------------------------------------------------------------

    private Task<HttpResponseMessage> ArchiveAsync(Guid babId)
        => SendAsync(HttpMethod.Post, $"/api/babs/{babId}/archive", _technicalOffice, Role.TechnicalOffice, Department.Operations);

    private async Task ArchiveItemAsync(Guid itemId)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/catalogue-items/{itemId}/archive", _technicalOffice, Role.TechnicalOffice, Department.Operations);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task MoveItemAsync(Guid itemId, Guid babId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put, new Uri($"/api/catalogue-items/{itemId}/bab", UriKind.Relative))
        {
            Content = JsonContent.Create(new { babId }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, _technicalOffice.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, Role.TechnicalOffice.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(_technicalOffice));
        request.Headers.Add(TestAuthHandler.DepartmentHeader, Department.Operations.ToString());

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateBabAsync(Guid? parent = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(UniqueNames.Code("ARB-BAB"), "باب", "Bab", Percentage.FromPercent(11m), parent).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task<string> CreateBabWithCodeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        string code = UniqueNames.Code("ARB-FLT");
        Bab bab = Bab.Create(code, "باب", "Bab", Percentage.FromPercent(11m)).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return code;
    }

    private async Task<Guid> ResolveBabIdAsync(string code)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Babs.Where(bab => bab.Code == code).Select(bab => bab.Id).SingleAsync(Ct);
    }

    private async Task<IReadOnlyList<string>> ListBabsAsync(string status)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, $"/api/babs?status={status}", _owner, Role.Owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return [.. body.RootElement.GetProperty("items").EnumerateArray()
            .Select(element => element.GetProperty("code").GetString()!)];
    }

    private async Task<Guid> CreateItemAsync(Guid babId, bool archived = false)
    {
        (Guid id, _) = await CreateItemWithCodeAsync(babId, archived);
        return id;
    }

    private async Task<(Guid Id, string Code)> CreateItemWithCodeAsync(Guid babId, bool archived = false)
    {
        await using KaffDbContext context = _database.CreateContext();

        string code = UniqueNames.Code("ARB-ITEM");

        CatalogueItem item = CatalogueItem.Create(
            code, "خرسانة عادية", "م٣", babId, new Money(100m), new Money(150m)).Value;

        if (archived)
        {
            item.Archive();
        }

        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);

        return (item.Id, code);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, Department? actorDepartment)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

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

        return await reader.Users.Where(user => user.Id == userId).Select(user => user.SecurityStamp).SingleAsync(Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("arb-owner", Role.Owner);
        User technicalOffice = MakeUser("arb-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("arb-finance", Role.Finance, Department.Finance);

        context.Users.AddRange(owner, technicalOffice, finance);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
