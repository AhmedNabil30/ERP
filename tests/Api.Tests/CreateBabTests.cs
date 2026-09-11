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
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-204 — <c>POST /api/babs</c>.
/// </summary>
/// <remarks>
/// Every test goes through HTTP, against fixtures nonced to this class, because the database is
/// shared across the collection — the same discipline every other catalogue/باب test class uses.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class CreateBabTests : IAsyncLifetime
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

    public CreateBabTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-204-A · created with a name, a parent (or none) and a markup -------------------------

    [Fact]
    public async Task A_bab_is_created_as_a_root_when_no_parent_is_given()
    {
        string code = UniqueNames.Code("BAB-ROOT");

        HttpResponseMessage response = await CreateAsync(Body(code, markup: 0.11m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement root = body.RootElement;

        root.GetProperty("code").GetString().Should().Be(code);
        root.TryGetProperty("parentBabId", out JsonElement parent).Should().BeTrue();
        parent.ValueKind.Should().Be(JsonValueKind.Null, "no parent was given — this باب is a root");

        WireDecimal(root.GetProperty("defaultMarkup")).Should().Be(
            0.11m, "AC-204-B/D-135: the wire carries the fraction, not the percent");
    }

    [Fact]
    public async Task A_bab_is_created_under_its_given_parent()
    {
        Guid parentId = await CreateBabDirectlyAsync();
        string code = UniqueNames.Code("BAB-CHILD");

        HttpResponseMessage response = await CreateAsync(Body(code, markup: 0.37m, parentBabId: parentId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("parentBabId").GetGuid().Should().Be(parentId);
    }

    // ---- AC-204-C · every باب's markup is its own, required, with no inheritance ------------------

    [Fact]
    public async Task A_child_bab_carries_its_own_markup_distinct_from_its_parents()
    {
        Guid parentId = await CreateBabDirectlyAsync(markup: 0.11m);

        HttpResponseMessage response = await CreateAsync(
            Body(UniqueNames.Code("BAB-OWNMK"), markup: 0.37m, parentBabId: parentId));

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        WireDecimal(body.RootElement.GetProperty("defaultMarkup")).Should().Be(
            0.37m, "AC-204-C: the child's own markup, not the parent's");
    }

    [Fact]
    public async Task An_omitted_markup_is_refused()
    {
        HttpResponseMessage response = await CreateAsync(new
        {
            code = UniqueNames.Code("BAB-NOMK"),
            nameAr = "باب",
            nameEn = "Bab",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.default_markup_required");
    }

    // ---- AC-204-F · a repeated code is refused, case-insensitively --------------------------------

    [Fact]
    public async Task A_repeated_code_is_refused_in_any_case()
    {
        string code = UniqueNames.Code("BAB-DUP");

        (await CreateAsync(Body(code))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await CreateAsync(Body(code))).StatusCode.Should().Be(
            HttpStatusCode.Conflict, "the exact code is already taken");

        (await CreateAsync(Body(code.ToLowerInvariant()))).StatusCode.Should().Be(
            HttpStatusCode.Conflict, "Bab.Create upper-invariants the code, so the lower-case form collides too");
    }

    // ---- AC-204-E · a باب cannot be its own ancestor, across Create + MoveBab (KAFF-205) -----------

    [Fact]
    public async Task A_bab_created_under_an_existing_bab_cannot_then_be_made_that_babs_parent()
    {
        Guid a = await CreateBabDirectlyAsync();
        string bCode = UniqueNames.Code("BAB-CYCLE-B");

        HttpResponseMessage createB = await CreateAsync(Body(bCode, parentBabId: a));
        createB.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument bBody = JsonDocument.Parse(await createB.Content.ReadAsStringAsync(Ct));
        Guid b = bBody.RootElement.GetProperty("id").GetGuid();

        // B is now A's child. Making B the parent of A would close a two-step cycle.
        using var moveRequest = new HttpRequestMessage(
            HttpMethod.Put, new Uri($"/api/babs/{a}/parent", UriKind.Relative))
        {
            Content = JsonContent.Create(new { parentBabId = b }),
        };

        await StampAsync(moveRequest, _technicalOffice, Role.TechnicalOffice, Department.Operations);

        HttpResponseMessage moveResponse = await _client.SendAsync(moveRequest, Ct);

        moveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(moveResponse)).Should().Be("errors.master.bab_cannot_be_its_own_ancestor");
    }

    // ---- AC-204-G · a role without BabManage reaches nothing ---------------------------------------

    [Fact]
    public async Task Only_the_technical_office_and_the_owner_may_create_a_bab()
    {
        foreach ((Guid actor, Role role, Department? department) in RefusedActors())
        {
            (await CreateAsync(Body(UniqueNames.Code($"BAB-{role}")), actor, role, department))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden, "{0} does not hold BabManage — spec.md §2, D-129 §1", role);
        }

        (await CreateAsync(Body(UniqueNames.Code("BAB-OWNER")), _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the Owner holds every company-wide row — D-129 §1");
    }

    // ---- AC-204-H · a markup change is audited before and after (creation half: names actor) ------

    [Fact]
    public async Task Creation_is_audited_and_names_the_actor()
    {
        string code = UniqueNames.Code("BAB-AUD");

        HttpResponseMessage response = await CreateAsync(Body(code, markup: 0.23m));

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            record => record.EntityId == id && record.Action == AuditAction.Created, Ct);

        record.EntityType.Should().Be(nameof(Bab));
        record.ActorUserId.Should().Be(_technicalOffice);
        record.GrantPath.Should().BeNull("BabManage is company-wide: no project, no path to name");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(string code, decimal markup = 0.11m, Guid? parentBabId = null) => new
    {
        code,
        nameAr = "باب",
        nameEn = "Bab",
        parentBabId,
        defaultMarkup = markup,
    };

    private IEnumerable<(Guid Actor, Role Role, Department? Department)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance);
        yield return (_hr, Role.Hr, Department.Hr);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations);
        yield return (_marketing, Role.MarketingSales, Department.Marketing);
    }

    private Task<HttpResponseMessage> CreateAsync(object body, Guid? actorId = null, Role? actorRole = null, Department? actorDepartment = null)
        => SendAsync(
            HttpMethod.Post,
            "/api/babs",
            actorId ?? _technicalOffice,
            actorRole ?? Role.TechnicalOffice,
            actorDepartment ?? Department.Operations,
            body);

    private async Task<Guid> CreateBabDirectlyAsync(decimal markup = 0.11m)
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(
            UniqueNames.Code("BAB-SEED"), "باب", "Bab", Percentage.FromFraction(markup)).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, Department? actorDepartment, object body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

        await StampAsync(request, actorId, actorRole, actorDepartment);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(HttpRequestMessage request, Guid actorId, Role actorRole, Department? actorDepartment)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
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

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private static decimal WireDecimal(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
        : element.GetDecimal();

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("crb-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "crb-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("crb-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("crb-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "crb-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "crb-design", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("crb-marketing", Role.MarketingSales, Department.Marketing);

        context.Users.AddRange(owner, technicalOffice, finance, hr, siteEngineer, headOfDesign, marketing);

        await context.SaveChangesAsync(Ct);

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
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
