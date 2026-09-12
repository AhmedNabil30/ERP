using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Features.Subcontractors.ListTaxRegistrations;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// decisions.md D-147 — Finance's own mechanism for the subcontractor's tax registration number:
/// <c>PUT /api/subcontractors/{id}/tax-registration</c> and
/// <c>GET /api/subcontractors/tax-registrations</c>, both gated
/// <c>Permission.SubcontractorTaxRegistrationEdit</c>. KAFF-211 rule 8, AC-211-K (restated), AC-211-N.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SubcontractorTaxRegistrationTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _hr;
    private Guid _marketing;
    private Guid _siteEngineer;

    public SubcontractorTaxRegistrationTests(PostgresDatabase database) => _database = database;

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

    // ---- D-147 test 3 ---------------------------------------------------------------------------

    [Fact]
    public async Task Finance_sets_a_subcontractors_tax_registration_and_it_is_audited_before_and_after()
    {
        Guid id = await CreateSubcontractorAsync("شركة قبل الرقم الضريبي");

        HttpResponseMessage response = await SetTaxRegistrationAsync(id, _finance, Role.Finance, "123-456-789");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Subcontractor stored = await ReadAsync(id);
        stored.TaxRegistrationNumber.Should().Be("123-456-789");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.EntityType.Should().Be(nameof(Subcontractor));
        record.ActorUserId.Should().Be(_finance);
        record.ActorRole.Should().Be(Role.Finance);
        record.ChangedProperties.Should().Contain(nameof(Subcontractor.TaxRegistrationNumber));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Subcontractor.TaxRegistrationNumber))
            .ValueKind.Should().Be(JsonValueKind.Null);
        after.RootElement.GetProperty(nameof(Subcontractor.TaxRegistrationNumber))
            .GetString().Should().Be("123-456-789");
    }

    // ---- AC-211-K, restated: two roles, two endpoints, two audit records ------------------------

    [Fact]
    public async Task The_retention_edit_and_the_tax_registration_edit_write_two_separate_audit_records()
    {
        Guid id = await CreateSubcontractorAsync("شركة سجلين تدقيق");
        Subcontractor seed = await ReadAsync(id);

        (await EditAsync(id, _technicalOffice, seed, retentionRate: 0m))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SetTaxRegistrationAsync(id, _finance, Role.Finance, "987-654-321"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        List<AuditRecord> modifications = await reader.AuditRecords
            .Where(record => record.EntityId == id && record.Action == AuditAction.Modified)
            .ToListAsync(Ct);

        modifications.Should().HaveCount(
            2, "D-147: two requests on two endpoints, never one combined save");

        modifications.Should().ContainSingle(
            record => record.ActorUserId == _technicalOffice
                      && record.ChangedProperties.Contains(nameof(Subcontractor.RetentionRate)));

        modifications.Should().ContainSingle(
            record => record.ActorUserId == _finance
                      && record.ChangedProperties.Contains(nameof(Subcontractor.TaxRegistrationNumber)));
    }

    // ---- D-147 test 4 / AC-211-N -----------------------------------------------------------------

    [Fact]
    public async Task The_technical_office_cannot_set_a_subcontractors_tax_registration_by_any_route()
    {
        Guid id = await CreateSubcontractorAsync("شركة محمية من الفني");

        (await SetTaxRegistrationAsync(id, _technicalOffice, Role.TechnicalOffice, "111-222-333"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await ReadAsync(id)).TaxRegistrationNumber.Should().BeNull("the refused call changed nothing");

        typeof(Kaff.Api.Features.Subcontractors.CreateSubcontractor.Request)
            .GetProperties().Select(property => property.Name)
            .Should().NotContain("TaxRegistrationNumber", "no member exists to write it through create");

        typeof(Kaff.Api.Features.Subcontractors.EditSubcontractor.Request)
            .GetProperties().Select(property => property.Name)
            .Should().NotContain("TaxRegistrationNumber", "no member exists to write it through edit");
    }

    // ---- D-147 test 5 -----------------------------------------------------------------------------

    [Fact]
    public async Task Every_role_without_SubcontractorTaxRegistrationEdit_is_refused_the_tax_registration_routes()
    {
        Guid id = await CreateSubcontractorAsync("شركة كل من دون الصلاحية");

        (Guid Actor, Role Role)[] refused =
        [
            (_technicalOffice, Role.TechnicalOffice),
            (_hr, Role.Hr),
            (_marketing, Role.MarketingSales),
            (_siteEngineer, Role.SiteEngineer),
        ];

        foreach ((Guid actor, Role role) in refused)
        {
            (await SetTaxRegistrationAsync(id, actor, role, "999-999-999"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} does not hold SubcontractorTaxRegistrationEdit", role);

            (await ListTaxRegistrationsRawAsync(actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} list", role);
        }

        (await SetTaxRegistrationAsync(id, _finance, Role.Finance, "444-444-444"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "Finance holds the row");

        (await ListTaxRegistrationsRawAsync(_owner, Role.Owner))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- D-147 test 6 — the projection is the control ---------------------------------------------

    [Fact]
    public async Task The_finance_subcontractor_list_carries_only_the_tax_registration_fields()
    {
        typeof(SubcontractorTaxRegistration).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["Id", "Code", "Name", "TaxRegistrationNumber", "IsActive"],
                "D-147 point 2 — no retention rate, no trade, no phone reaches Finance through this route");

        Guid id = await CreateSubcontractorAsync("شركة في قائمة المالية");
        await SetTaxRegistrationAsync(id, _finance, Role.Finance, "555-555-555");

        HttpResponseMessage response = await ListTaxRegistrationsRawAsync(_finance, Role.Finance);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        JsonElement row = body.RootElement.GetProperty("subcontractors").EnumerateArray()
            .Single(element => element.GetProperty("id").GetGuid() == id);

        string[] fields = [.. row.EnumerateObject().Select(property => property.Name)];

        fields.Should().BeEquivalentTo(["id", "code", "name", "taxRegistrationNumber", "isActive"]);
        row.GetProperty("taxRegistrationNumber").GetString().Should().Be("555-555-555");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<Guid> CreateSubcontractorAsync(string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/subcontractors", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name,
                phone = UniqueNames.Phone().Entered,
                acknowledgedDuplicatePhone = false,
            }),
        };

        await StampAsync(request, _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> EditAsync(
        Guid id, Guid actorId, Subcontractor seed, decimal? retentionRate = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/subcontractors/{id}", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name = seed.Name,
                phone = seed.PhoneEntered,
                tradeBabId = seed.TradeBabId,
                retentionRate = retentionRate ?? seed.RetentionRate.Fraction,
                acknowledgedDuplicatePhone = false,
            }),
        };

        await StampAsync(request, actorId, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> SetTaxRegistrationAsync(
        Guid id, Guid actorId, Role actorRole, string? taxRegistrationNumber)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put, new Uri($"/api/subcontractors/{id}/tax-registration", UriKind.Relative))
        {
            Content = JsonContent.Create(new { taxRegistrationNumber }),
        };

        await StampAsync(request, actorId, actorRole, DepartmentOf(actorRole), SubDepartmentOf(actorRole));

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> ListTaxRegistrationsRawAsync(Guid actorId, Role actorRole)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/subcontractors/tax-registrations", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, DepartmentOf(actorRole), SubDepartmentOf(actorRole));

        return await _client.SendAsync(request, Ct);
    }

    private static Department? DepartmentOf(Role role) => role switch
    {
        Role.Owner => null,
        Role.Finance => Department.Finance,
        Role.TechnicalOffice or Role.SiteEngineer => Department.Operations,
        Role.Hr => Department.Hr,
        Role.MarketingSales => Department.Marketing,
        _ => null,
    };

    private static OperationsSubDepartment? SubDepartmentOf(Role role) =>
        role is Role.TechnicalOffice or Role.SiteEngineer ? OperationsSubDepartment.Technical : null;

    private async Task StampAsync(
        HttpRequestMessage request, Guid actorId, Role actorRole, Department? department, OperationsSubDepartment? sub)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
        }

        if (sub is not null)
        {
            request.Headers.Add(TestAuthHandler.SubDepartmentHeader, sub.Value.ToString());
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

    private async Task<Subcontractor> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Subcontractors.SingleAsync(candidate => candidate.Id == id, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("sctax-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "sctax-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("sctax-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("sctax-hr", Role.Hr, Department.Hr);
        User marketing = MakeUser("sctax-marketing", Role.MarketingSales, Department.Marketing);
        User siteEngineer = MakeUser(
            "sctax-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);

        context.Users.AddRange(owner, technicalOffice, finance, hr, marketing, siteEngineer);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _marketing = marketing.Id;
        _siteEngineer = siteEngineer.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
