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
/// KAFF-207 — <c>PUT /api/employees/{employeeId}</c>. Also carries KAFF-208's <c>AC-208-C</c>: this
/// is the edit endpoint that story's own Definition-of-Ready row held pending
/// (<c>MasterDataErrors.EmployeeKindIsImmutable</c> was declared and unreachable). It ships here as
/// part of KAFF-207's own criteria — <c>AC-207-I</c> requires an edit request to audit — so
/// <c>AC-208-C</c>/<c>TC-2-078</c> comes off Held in this same commit.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EditEmployeeTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;

    public EditEmployeeTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-207-I · every change is audited before and after --------------------------------------

    [Fact]
    public async Task Editing_name_and_specialty_in_one_request_is_audited_with_both_before_and_after()
    {
        (Guid id, string phone) = await CreateEmployeeAsync(babId: null);

        HttpResponseMessage response = await EditAsync(
            id, "New Name", phone, EmployeeKind.Salaried, babId: null, specialty: "New Specialty");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(Employee.FullName));
        record.ChangedProperties.Should().Contain(nameof(Employee.Specialty));
        record.ActorUserId.Should().Be(_hr);

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Employee.FullName)).GetString().Should().Be("Original Name");
        after.RootElement.GetProperty(nameof(Employee.FullName)).GetString().Should().Be("New Name");
        before.RootElement.GetProperty(nameof(Employee.Specialty)).GetString().Should().BeNull();
        after.RootElement.GetProperty(nameof(Employee.Specialty)).GetString().Should().Be("New Specialty");
    }

    // ---- AC-207-I (second half) · a refused edit writes no record and changes nothing -------------

    [Fact]
    public async Task A_refused_edit_writes_no_audit_record_and_changes_nothing()
    {
        (Guid id, string phone) = await CreateEmployeeAsync(babId: null);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        // Refused by Finance, who does not hold EmployeeManage.
        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Put,
            $"/api/employees/{id}",
            _finance,
            Role.Finance,
            Department.Finance,
            EditBody("Attempted Name", phone, EmployeeKind.Salaried, null, null));

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(countBefore);

        Employee stored = await after.Employees.SingleAsync(candidate => candidate.Id == id, Ct);
        stored.FullName.Should().Be("Original Name", "the refused edit changed nothing");
    }

    // ---- AC-208-C · a population cannot be edited from one to the other ---------------------------

    [Fact]
    public async Task A_request_naming_a_different_kind_is_refused_and_the_population_does_not_change()
    {
        (Guid id, string phone) = await CreateEmployeeAsync(babId: null);

        HttpResponseMessage response = await EditAsync(
            id, "Original Name", phone, EmployeeKind.DayLabour, babId: Guid.NewGuid(), specialty: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.master.employee_kind_immutable");

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee stored = await reader.Employees.SingleAsync(candidate => candidate.Id == id, Ct);
        stored.Kind.Should().Be(EmployeeKind.Salaried, "AC-208-C: the population does not change");
    }

    [Fact]
    public async Task A_refused_kind_change_writes_no_audit_record()
    {
        (Guid id, string phone) = await CreateEmployeeAsync(babId: null);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        await EditAsync(id, "Original Name", phone, EmployeeKind.DayLabour, Guid.NewGuid(), null);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(countBefore);
    }

    // ---- AC-207-B (edit half) · day labour still requires a باب on edit ---------------------------

    [Fact]
    public async Task Editing_a_day_labourer_to_have_no_bab_is_refused()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, string phone) = await CreateDayLabourerAsync(bab);

        HttpResponseMessage response = await EditAsync(
            id, "Worker", phone, EmployeeKind.DayLabour, babId: null, specialty: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.day_labour_requires_trade");
    }

    // ---- AC-207-H · a role without EmployeeManage cannot edit --------------------------------------

    [Fact]
    public async Task Only_hr_and_the_owner_may_edit_an_employee()
    {
        (Guid id, string phone) = await CreateEmployeeAsync(babId: null);

        (await SendAsync(
            HttpMethod.Put, $"/api/employees/{id}", _finance, Role.Finance, Department.Finance,
            EditBody("Attempt", phone, EmployeeKind.Salaried, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold EmployeeManage");

        (await SendAsync(
            HttpMethod.Put, $"/api/employees/{id}", _owner, Role.Owner, null,
            EditBody("Owner Edit", phone, EmployeeKind.Salaried, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object EditBody(string fullName, string phone, EmployeeKind kind, Guid? babId, string? specialty) => new
    {
        fullName,
        phone,
        kind = kind.ToString(),
        babId,
        specialty,
    };

    private Task<HttpResponseMessage> EditAsync(
        Guid id, string fullName, string phone, EmployeeKind kind, Guid? babId, string? specialty)
        => SendAsync(
            HttpMethod.Put, $"/api/employees/{id}", _hr, Role.Hr, Department.Hr,
            EditBody(fullName, phone, kind, babId, specialty));

    private async Task<(Guid Id, string Phone)> CreateEmployeeAsync(Guid? babId)
    {
        string phone = UniqueNames.Phone().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName = "Original Name",
                phone,
                kind = nameof(EmployeeKind.Salaried),
                babId,
            }),
        };

        await StampAsync(request, _hr, Role.Hr, Department.Hr);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<(Guid Id, string Phone)> CreateDayLabourerAsync(Guid babId)
    {
        string phone = UniqueNames.Phone().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName = "Worker",
                phone,
                kind = nameof(EmployeeKind.DayLabour),
                babId,
            }),
        };

        await StampAsync(request, _hr, Role.Hr, Department.Hr);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(UniqueNames.Code("EDE-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("ede-owner", Role.Owner);
        User hr = MakeUser("ede-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("ede-finance", Role.Finance, Department.Finance);

        context.Users.AddRange(owner, hr, finance);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
