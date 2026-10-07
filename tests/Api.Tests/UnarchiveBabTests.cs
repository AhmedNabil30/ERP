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
/// KAFF-943 — <c>POST /api/babs/{babId}/unarchive</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UnarchiveBabTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;

    public UnarchiveBabTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-943-A · an archived باب comes back ------------------------------------------------------

    [Fact]
    public async Task An_archived_bab_comes_back_and_appears_in_the_default_tree()
    {
        string code = await CreateBabWithCodeAsync();
        Guid bab = await ResolveBabIdAsync(code);
        await ArchiveDirectAsync(bab);

        (await ListBabsAsync(status: "active")).Should().NotContain(code);

        (await UnarchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(candidate => candidate.Id == bab, Ct)).IsActive.Should().BeTrue();
        (await ListBabsAsync(status: "active")).Should().Contain(code);
    }

    // ---- AC-943-B · the permission holds ------------------------------------------------------------

    [Fact]
    public async Task A_role_without_bab_manage_cannot_unarchive_a_bab()
    {
        Guid bab = await CreateBabAsync();
        await ArchiveDirectAsync(bab);

        (await SendAsync(HttpMethod.Post, $"/api/babs/{bab}/unarchive", _finance, Role.Finance, WellKnownDepartments.FinanceId))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold BabManage");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(candidate => candidate.Id == bab, Ct)).IsActive.Should().BeFalse();
    }

    // ---- AC-943-C · it writes an audit record -------------------------------------------------------

    [Fact]
    public async Task Unarchiving_writes_one_audit_record_with_the_old_and_new_status()
    {
        Guid bab = await CreateBabAsync();
        await ArchiveDirectAsync(bab);

        await using KaffDbContext beforeContext = _database.CreateBareContext();
        long countBefore = await beforeContext.AuditRecords.LongCountAsync(r => r.EntityId == bab, Ct);

        (await UnarchiveAsync(bab)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.AuditRecords.LongCountAsync(r => r.EntityId == bab, Ct)).Should().Be(countBefore + 1);

        AuditRecord record = await reader.AuditRecords
            .Where(r => r.EntityId == bab && r.Action == AuditAction.Modified)
            .OrderByDescending(r => r.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_technicalOffice);
        record.ChangedProperties.Should().Contain(nameof(Bab.IsActive));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Bab.IsActive)).GetBoolean().Should().BeFalse();
        after.RootElement.GetProperty(nameof(Bab.IsActive)).GetBoolean().Should().BeTrue();
    }

    // ---- AC-943-D · nothing else moves --------------------------------------------------------------

    [Fact]
    public async Task Unarchiving_a_bab_changes_no_item_and_no_child_bab()
    {
        Guid parent = await CreateBabAsync();
        Guid archivedChild = await CreateBabAsync(parent: parent);
        Guid activeChild = await CreateBabAsync(parent: parent);
        Guid archivedItem = await CreateItemAsync(parent, archived: true);
        Guid activeItem = await CreateItemAsync(parent);

        await ArchiveDirectAsync(archivedChild);
        await ArchiveDirectAsync(parent);

        (await UnarchiveAsync(parent)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(b => b.Id == parent, Ct)).IsActive.Should().BeTrue();
        (await reader.Babs.SingleAsync(b => b.Id == archivedChild, Ct)).IsActive.Should().BeFalse("no cascade down");
        (await reader.Babs.SingleAsync(b => b.Id == activeChild, Ct)).IsActive.Should().BeTrue();
        (await reader.CatalogueItems.SingleAsync(i => i.Id == archivedItem, Ct)).Status.Should().Be(CatalogueItemStatus.Archived);
        (await reader.CatalogueItems.SingleAsync(i => i.Id == activeItem, Ct)).Status.Should().Be(CatalogueItemStatus.Active);
    }

    // ---- AC-943-E · unarchiving an active باب is refused --------------------------------------------

    [Fact]
    public async Task Unarchiving_an_active_bab_is_refused_with_a_translated_message_and_writes_no_audit_record()
    {
        Guid bab = await CreateBabAsync();

        await using KaffDbContext beforeContext = _database.CreateBareContext();
        long countBefore = await beforeContext.AuditRecords.LongCountAsync(r => r.EntityId == bab, Ct);

        HttpResponseMessage response = await UnarchiveAsync(bab);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.master.not_archived");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.AuditRecords.LongCountAsync(r => r.EntityId == bab, Ct)).Should().Be(countBefore);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private Task<HttpResponseMessage> UnarchiveAsync(Guid babId)
        => SendAsync(HttpMethod.Post, $"/api/babs/{babId}/unarchive", _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId);

    private async Task ArchiveDirectAsync(Guid babId)
    {
        await using KaffDbContext context = _database.CreateContext();
        Bab bab = await context.Babs.SingleAsync(candidate => candidate.Id == babId, Ct);
        bab.Archive();
        await context.SaveChangesAsync(Ct);
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
        HttpMethod method, string route, Guid actorId, Role actorRole, Guid? actorDepartment)
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
        User technicalOffice = MakeUser("arb-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("arb-finance", Role.Finance, WellKnownDepartments.FinanceId);

        context.Users.AddRange(owner, technicalOffice, finance);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
