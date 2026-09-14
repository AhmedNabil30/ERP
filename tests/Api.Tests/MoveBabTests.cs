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
/// KAFF-205 — <c>PUT /api/babs/{babId}/parent</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MoveBabTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;

    public MoveBabTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-205-A · a باب moves, and its children move with it -------------------------------------

    [Fact]
    public async Task A_bab_moves_and_its_children_stay_its_children()
    {
        Guid a = await CreateBabAsync();
        Guid b = await CreateBabAsync(parent: a);
        Guid c = await CreateBabAsync(parent: a);
        Guid d = await CreateBabAsync();

        (await MoveAsync(a, d)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.Babs.SingleAsync(bab => bab.Id == a, Ct)).ParentBabId.Should().Be(d);
        (await reader.Babs.SingleAsync(bab => bab.Id == b, Ct)).ParentBabId.Should().Be(
            a, "B stays A's child, not re-parented to D directly");
        (await reader.Babs.SingleAsync(bab => bab.Id == c, Ct)).ParentBabId.Should().Be(a);
    }

    // ---- AC-205-B · a باب becomes a root -------------------------------------------------------------

    [Fact]
    public async Task Clearing_the_parent_makes_a_bab_a_root_and_its_children_are_unchanged()
    {
        Guid parent = await CreateBabAsync();
        Guid child = await CreateBabAsync(parent: parent);
        Guid grandchild = await CreateBabAsync(parent: child);

        (await MoveAsync(child, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.Babs.SingleAsync(bab => bab.Id == child, Ct)).ParentBabId.Should().BeNull();
        (await reader.Babs.SingleAsync(bab => bab.Id == grandchild, Ct)).ParentBabId.Should().Be(
            child, "the child's own children are unchanged");
    }

    // ---- AC-205-C · a باب cannot become its own ancestor, at any depth -----------------------------

    [Fact]
    public async Task A_bab_cannot_become_its_own_ancestor_at_any_depth()
    {
        Guid a = await CreateBabAsync();
        Guid b = await CreateBabAsync(parent: a);
        Guid c = await CreateBabAsync(parent: b);

        // Self-parent.
        (await MoveAsync(a, a)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Two-step: B's parent is already A; making A's parent B closes the cycle.
        HttpResponseMessage twoStep = await MoveAsync(a, b);
        twoStep.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(twoStep)).Should().Be("errors.master.bab_cannot_be_its_own_ancestor");

        // Three-step chain A -> B -> C, closing as A re-parented to C.
        HttpResponseMessage threeStep = await MoveAsync(a, c);
        threeStep.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(threeStep)).Should().Be("errors.master.bab_cannot_be_its_own_ancestor");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Babs.SingleAsync(bab => bab.Id == a, Ct)).ParentBabId.Should().BeNull(
            "every refusal above left every باب reachable from a root");
    }

    // ---- AC-205-H · a role without BabManage moves nothing ------------------------------------------

    [Fact]
    public async Task Only_the_technical_office_and_the_owner_may_move_a_bab()
    {
        Guid a = await CreateBabAsync();
        Guid d = await CreateBabAsync();

        (await SendAsync(HttpMethod.Put, $"/api/babs/{a}/parent", _finance, Role.Finance, WellKnownDepartments.FinanceId, new { parentBabId = d }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold BabManage");

        (await SendAsync(HttpMethod.Put, $"/api/babs/{a}/parent", _owner, Role.Owner, null, new { parentBabId = d }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- AC-205-I · a re-parent is audited with the old and new parent -----------------------------

    [Fact]
    public async Task A_reparent_is_audited_with_the_old_and_the_new_parent()
    {
        Guid a = await CreateBabAsync();
        Guid d = await CreateBabAsync();

        (await MoveAsync(a, d)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(r => r.EntityId == a && r.Action == AuditAction.Modified)
            .OrderByDescending(r => r.OccurredAt)
            .FirstAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(Bab.ParentBabId));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Bab.ParentBabId)).ValueKind.Should().Be(JsonValueKind.Null);
        after.RootElement.GetProperty(nameof(Bab.ParentBabId)).GetGuid().Should().Be(d);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private Task<HttpResponseMessage> MoveAsync(Guid babId, Guid? newParent)
        => SendAsync(
            HttpMethod.Put, $"/api/babs/{babId}/parent", _technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId,
            new { parentBabId = newParent });

    private async Task<Guid> CreateBabAsync(Guid? parent = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(
            UniqueNames.Code("MVB-BAB"), "باب", "Bab", Percentage.FromPercent(11m), parent).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
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

        User owner = MakeUser("mvb-owner", Role.Owner);
        User technicalOffice = MakeUser("mvb-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("mvb-finance", Role.Finance, WellKnownDepartments.FinanceId);

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
