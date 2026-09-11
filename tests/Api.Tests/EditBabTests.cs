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
/// KAFF-204 — <c>PUT /api/babs/{babId}</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EditBabTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;

    public EditBabTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-204-H · a markup change is audited with both the old and the new rate -----------------

    [Fact]
    public async Task A_markup_change_is_audited_with_the_old_and_new_rate()
    {
        Guid id = await CreateBabAsync(markup: 0.11m);

        HttpResponseMessage response = await EditAsync(id, "باب", "Bab", 0.37m);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(r => r.EntityId == id && r.Action == AuditAction.Modified)
            .OrderByDescending(r => r.OccurredAt)
            .FirstAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(Bab.DefaultMarkup));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        WireDecimal(before.RootElement.GetProperty(nameof(Bab.DefaultMarkup))).Should().Be(0.11m);
        WireDecimal(after.RootElement.GetProperty(nameof(Bab.DefaultMarkup))).Should().Be(0.37m);
    }

    // ---- AC-204-D · a markup change writes nothing beyond the باب's own row ------------------------

    [Fact]
    public async Task A_markup_change_writes_no_row_beyond_the_babs_own()
    {
        Guid id = await CreateBabAsync(markup: 0.11m);

        await using KaffDbContext before = _database.CreateBareContext();
        long babCountBefore = await before.Babs.LongCountAsync(Ct);
        long itemCountBefore = await before.CatalogueItems.LongCountAsync(Ct);

        (await EditAsync(id, "باب", "Bab", 0.37m)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext after = _database.CreateBareContext();

        (await after.Babs.LongCountAsync(Ct)).Should().Be(babCountBefore, "no باب row is created or removed");
        (await after.CatalogueItems.LongCountAsync(Ct)).Should().Be(
            itemCountBefore, "AC-204-D: a markup change reaches no other table — no BOQ or estimate exists yet (V-35-N)");
    }

    // ---- AC-204-G · a role without BabManage cannot edit --------------------------------------------

    [Fact]
    public async Task Only_the_technical_office_and_the_owner_may_edit_a_bab()
    {
        Guid id = await CreateBabAsync();

        (await SendAsync(HttpMethod.Put, $"/api/babs/{id}", _finance, Role.Finance, Department.Finance, Body("باب", "Bab", 0.23m)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold BabManage");

        (await SendAsync(HttpMethod.Put, $"/api/babs/{id}", _owner, Role.Owner, null, Body("باب", "Bab", 0.23m)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(string nameAr, string nameEn, decimal markup) => new { nameAr, nameEn, defaultMarkup = markup };

    private Task<HttpResponseMessage> EditAsync(Guid id, string nameAr, string nameEn, decimal markup)
        => SendAsync(HttpMethod.Put, $"/api/babs/{id}", _technicalOffice, Role.TechnicalOffice, Department.Operations, Body(nameAr, nameEn, markup));

    private async Task<Guid> CreateBabAsync(decimal markup = 0.11m)
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(UniqueNames.Code("EDB-BAB"), "باب", "Bab", Percentage.FromFraction(markup)).Value;

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

    private static decimal WireDecimal(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
        : element.GetDecimal();

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("edb-owner", Role.Owner);
        User technicalOffice = MakeUser("edb-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("edb-finance", Role.Finance, Department.Finance);

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
