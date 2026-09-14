using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/employees/babs</c> — the markup-free باب picker for the employee form. decisions.md
/// D-137. Closes the gap where HR gets <c>403</c> from <c>GET /api/babs</c> and so cannot register a
/// day labourer at all.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ListBabOptionsTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _marketing;
    private Guid _siteEngineer;
    private Guid _technicalOffice;

    public ListBabOptionsTests(PostgresDatabase database) => _database = database;

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

    [Fact]
    public async Task Hr_lists_bab_options_and_no_option_carries_a_markup()
    {
        await CreateBabAsync("LBO-HR");

        HttpResponseMessage response = await SendAsync(_hr, Role.Hr, WellKnownDepartments.HrId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        JsonElement.ArrayEnumerator items = body.RootElement.GetProperty("items").EnumerateArray();

        items.Should().NotBeEmpty();

        string[] allowList = ["id", "code", "nameAr", "nameEn", "parentBabId", "isActive"];

        foreach (JsonElement item in items)
        {
            item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
                allowList,
                "an allow-list, like AC-207-E — a markup added under any name fails it (decisions.md D-137)");
        }
    }

    [Fact]
    public async Task Hr_is_still_refused_the_bab_manage_list()
    {
        HttpResponseMessage response = await SendTo("/api/babs", _hr, Role.Hr, WellKnownDepartments.HrId);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "GET /api/babs stays gated BabManage, which HR does not hold — decisions.md D-137 closes "
            + "the gap with a new route, not by widening this one");
    }

    [Fact]
    public async Task A_role_without_EmployeeManage_is_refused_the_bab_options()
    {
        (await SendAsync(_finance, Role.Finance, WellKnownDepartments.FinanceId)).StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "Finance does not hold EmployeeManage");

        (await SendAsync(_marketing, Role.MarketingSales, null)).StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "Marketing/Sales does not hold EmployeeManage");

        (await SendAsync(_siteEngineer, Role.SiteEngineer, WellKnownDepartments.OperationsId)).StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "Site Engineer does not hold EmployeeManage");

        (await SendAsync(_technicalOffice, Role.TechnicalOffice, WellKnownDepartments.OperationsId)).StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "Technical Office is refused on purpose — it reads أبواب through GET /api/babs instead");
    }

    [Fact]
    public async Task The_owner_lists_bab_options()
    {
        await CreateBabAsync("LBO-OWN");

        (await SendAsync(_owner, Role.Owner, null)).StatusCode.Should().Be(
            HttpStatusCode.OK, "the Owner holds every company-wide row — decisions.md D-129 §1");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<string> CreateBabAsync(string codePrefix)
    {
        await using KaffDbContext context = _database.CreateContext();

        string code = UniqueNames.Code(codePrefix);

        Bab bab = Bab.Create(code, "باب", "Bab", Percentage.FromPercent(15m)).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return code;
    }

    private Task<HttpResponseMessage> SendAsync(Guid actorId, Role actorRole, Guid? actorDepartment)
        => SendTo("/api/employees/babs", actorId, actorRole, actorDepartment);

    private async Task<HttpResponseMessage> SendTo(
        string route, Guid actorId, Role actorRole, Guid? actorDepartment)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));

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

        User owner = MakeUser("lbo-owner", Role.Owner);
        User hr = MakeUser("lbo-hr", Role.Hr, WellKnownDepartments.HrId);
        User finance = MakeUser("lbo-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User marketing = MakeUser("lbo-marketing", Role.MarketingSales, null);
        User siteEngineer = MakeUser(
            "lbo-siteeng", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User technicalOffice = MakeUser(
            "lbo-techoffice", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

        context.Users.AddRange(owner, hr, finance, marketing, siteEngineer, technicalOffice);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _marketing = marketing.Id;
        _siteEngineer = siteEngineer.Id;
        _technicalOffice = technicalOffice.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Guid? department = null,
        OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
