using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Features.Babs.ListBabs;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/babs</c> — the smallest read that unblocks a باب name and group header on the
/// catalogue screens. Not KAFF-204 (the tree) — that story is not Ready. This is a flat list.
/// </summary>
/// <remarks>
/// Every test goes through HTTP, against fixtures nonced to this class, because the database is
/// shared by every class in the collection — the same discipline <c>ListCatalogueItemsTests</c> uses.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ListBabsTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;

    public ListBabsTests(PostgresDatabase database) => _database = database;

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

    // ---- a holder of BabManage gets the list -----------------------------------------------------

    [Fact]
    public async Task A_holder_of_bab_manage_gets_the_list()
    {
        string code = await CreateBabAsync(codePrefix: "LST-HLD");

        (await ListAsync(_technicalOffice, Role.TechnicalOffice, Department.Operations))
            .Select(bab => bab.Code).Should().Contain(code);
    }

    // ---- a role without BabManage is refused, hit directly ----------------------------------------

    [Fact]
    public async Task A_role_without_bab_manage_is_refused()
    {
        HttpResponseMessage response = await SendAsync(_finance, Role.Finance, Department.Finance);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "Finance does not hold BabManage — only Owner and TechnicalOffice do, spec.md §2");
    }

    // ---- ordering: SortOrder first, Code second, and the two must disagree ------------------------

    [Fact]
    public async Task Results_are_ordered_by_sort_order_then_by_code()
    {
        string nonce = UniqueNames.Code("LST-ORD");

        // SortOrder and Code deliberately disagree: the باب with the HIGHER code sorts FIRST by
        // SortOrder, so a code-only sort (the V-35-R defect) would report these in the wrong order.
        string first = await CreateBabAsync(sortOrder: 1, codePrefix: $"{nonce}-Z");
        string second = await CreateBabAsync(sortOrder: 2, codePrefix: $"{nonce}-A");

        IReadOnlyList<BabSummary> babs = await ListAsync(_owner, Role.Owner, null);

        babs.Where(bab => bab.Code == first || bab.Code == second)
            .Select(bab => bab.Code).Should().Equal(
                [first, second],
                "ordered by SortOrder (1 before 2), not by Code (which would put the '-A' code first) — "
                + "this exact defect was V-35-R in qa/slice-2/verification-2026-09-10.md");
    }

    // ---- an empty database returns an empty list, not an error ------------------------------------

    [Fact]
    public async Task An_empty_database_returns_an_empty_list()
    {
        // A fresh Postgres fixture per test class (PostgresDatabase), and no باب is seeded by this
        // class's SeedAsync — only users. CLAUDE.md: no أبواب are ever seeded by this story.
        HttpResponseMessage response = await SendAsync(_owner, Role.Owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "an empty list is not an error");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.TryGetProperty("items", out JsonElement items).Should().BeTrue();
        items.ValueKind.Should().Be(JsonValueKind.Array, "never null");
        items.GetArrayLength().Should().Be(0, "this story seeds no أبواب — Q75 is still open");
    }

    // ---- shape: flat, carries ParentBabId, not a tree ----------------------------------------------

    [Fact]
    public void The_response_is_flat_and_carries_every_field_the_brief_names()
    {
        typeof(BabSummary).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["Id", "Code", "NameAr", "NameEn", "ParentBabId", "DefaultMarkup", "IsActive"],
                "flat, not a tree — ParentBabId is in the payload so the client can nest it; building "
                + "the tree itself is KAFF-204's job");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<string> CreateBabAsync(int sortOrder = 0, string? codePrefix = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        string code = UniqueNames.Code(codePrefix ?? "LST-BAB");

        Bab bab = Bab.Create(
            code, "باب", "Bab", Percentage.FromPercent(15m), sortOrder: sortOrder).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return code;
    }

    private async Task<IReadOnlyList<BabSummary>> ListAsync(Guid actorId, Role actorRole, Department? actorDepartment)
    {
        HttpResponseMessage response = await SendAsync(actorId, actorRole, actorDepartment);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return
        [
            .. body.RootElement.GetProperty("items").EnumerateArray().Select(element => new BabSummary(
                element.GetProperty("id").GetGuid(),
                element.GetProperty("code").GetString()!,
                element.GetProperty("nameAr").GetString()!,
                element.GetProperty("nameEn").GetString()!,
                element.TryGetProperty("parentBabId", out JsonElement parent) && parent.ValueKind != JsonValueKind.Null
                    ? parent.GetGuid()
                    : null,
                element.GetProperty("defaultMarkup").GetDecimal(),
                element.GetProperty("isActive").GetBoolean())),
        ];
    }

    private async Task<HttpResponseMessage> SendAsync(Guid actorId, Role actorRole, Department? actorDepartment)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/babs", UriKind.Relative));

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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("lst-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "lst-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("lst-finance", Role.Finance, Department.Finance);

        context.Users.AddRange(owner, technicalOffice, finance);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
