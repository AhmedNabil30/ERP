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
/// KAFF-205 — <c>PUT /api/catalogue-items/{catalogueItemId}/bab</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MoveCatalogueItemTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _babA;
    private Guid _babB;

    public MoveCatalogueItemTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-205-D · an item moves between أبواب ------------------------------------------------------

    [Fact]
    public async Task An_item_moves_from_one_bab_to_another()
    {
        Guid item = await CreateItemAsync(_babA);

        (await MoveAsync(item, _babB)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CatalogueItems.SingleAsync(i => i.Id == item, Ct)).BabId.Should().Be(_babB);
    }

    [Fact]
    public async Task Moving_to_an_unknown_bab_is_refused()
    {
        Guid item = await CreateItemAsync(_babA);

        HttpResponseMessage response = await MoveAsync(item, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.master.bab_not_found");
    }

    // ---- AC-205-H · a role without CatalogueManage moves nothing ------------------------------------

    [Fact]
    public async Task Only_the_technical_office_and_the_owner_may_move_an_item()
    {
        Guid item = await CreateItemAsync(_babA);

        (await SendAsync(HttpMethod.Put, $"/api/catalogue-items/{item}/bab", _finance, Role.Finance, WellKnownDepartments.FinanceId, new { babId = _babB }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold CatalogueManage");

        (await SendAsync(HttpMethod.Put, $"/api/catalogue-items/{item}/bab", _owner, Role.Owner, null, new { babId = _babB }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- AC-205-I · a move is audited with the old and the new باب ----------------------------------

    [Fact]
    public async Task A_move_is_audited_with_the_old_and_the_new_bab()
    {
        Guid item = await CreateItemAsync(_babA);

        (await MoveAsync(item, _babB)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(r => r.EntityId == item && r.Action == AuditAction.Modified)
            .OrderByDescending(r => r.OccurredAt)
            .FirstAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(CatalogueItem.BabId));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(CatalogueItem.BabId)).GetGuid().Should().Be(_babA);
        after.RootElement.GetProperty(nameof(CatalogueItem.BabId)).GetGuid().Should().Be(_babB);
    }

    // ---- KAFF-205 rule 5 · a move touches only the item's own row -----------------------------------

    [Fact]
    public async Task A_move_writes_no_row_beyond_the_items_own()
    {
        Guid item = await CreateItemAsync(_babA);

        await using KaffDbContext before = _database.CreateBareContext();
        long itemCountBefore = await before.CatalogueItems.LongCountAsync(Ct);
        long babCountBefore = await before.Babs.LongCountAsync(Ct);

        (await MoveAsync(item, _babB)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.CatalogueItems.LongCountAsync(Ct)).Should().Be(itemCountBefore);
        (await after.Babs.LongCountAsync(Ct)).Should().Be(babCountBefore, "a move reaches no باب row");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private Task<HttpResponseMessage> MoveAsync(Guid itemId, Guid babId)
        => SendAsync(
            HttpMethod.Put, $"/api/catalogue-items/{itemId}/bab", _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId,
            new { babId });

    private async Task<Guid> CreateItemAsync(Guid babId)
    {
        await using KaffDbContext context = _database.CreateContext();

        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code("MVI-ITEM"), "خرسانة عادية", "م٣", babId, new Money(100m), new Money(150m)).Value;

        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);

        return item.Id;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, Guid? actorDepartment, object body)
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

        Bab babA = Bab.Create(UniqueNames.Code("MVI-BAB-A"), "باب أ", "Bab A", Percentage.FromPercent(11m)).Value;
        Bab babB = Bab.Create(UniqueNames.Code("MVI-BAB-B"), "باب ب", "Bab B", Percentage.FromPercent(37m)).Value;

        User owner = MakeUser("mvi-owner", Role.Owner);
        User technicalOffice = MakeUser("mvi-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("mvi-finance", Role.Finance, WellKnownDepartments.FinanceId);

        context.Babs.AddRange(babA, babB);
        context.Users.AddRange(owner, technicalOffice, finance);
        await context.SaveChangesAsync(Ct);

        _babA = babA.Id;
        _babB = babB.Id;
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
