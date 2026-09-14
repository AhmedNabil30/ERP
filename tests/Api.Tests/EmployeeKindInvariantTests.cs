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
/// KAFF-208 — "nobody appears in both populations", the parts of the invariant that need a real
/// database rather than the entity alone. <c>AC-208-C</c> and <c>AC-208-D</c> are in
/// <c>EditEmployeeTests</c> and <c>Kaff.Domain.Tests.EmployeeTests</c> respectively.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmployeeKindInvariantTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _hr;

    public EmployeeKindInvariantTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-208-A · every costed person is in exactly one population, read back from storage ------

    [Fact]
    public async Task Every_stored_employee_carries_exactly_one_defined_kind()
    {
        Guid bab = await CreateBabAsync();

        (Guid salariedId, _) = await CreateAsync("Salaried Person", EmployeeKind.Salaried, null);
        (Guid dayLabourId, _) = await CreateAsync("Day Labourer", EmployeeKind.DayLabour, bab);

        await using KaffDbContext reader = _database.CreateBareContext();

        Employee salaried = await reader.Employees.SingleAsync(e => e.Id == salariedId, Ct);
        Employee dayLabour = await reader.Employees.SingleAsync(e => e.Id == dayLabourId, Ct);

        foreach (Employee employee in new[] { salaried, dayLabour })
        {
            Enum.IsDefined(employee.Kind).Should().BeTrue(
                "AC-208-A: every stored Kind round-trips as a defined member, never the zero value");
        }

        salaried.Kind.Should().Be(EmployeeKind.Salaried);
        dayLabour.Kind.Should().Be(EmployeeKind.DayLabour);
    }

    // ---- AC-208-E · archived, not moved — and the gap this exposes ---------------------------------

    [Fact]
    public async Task Archiving_a_day_labourer_leaves_the_record_unchanged_and_never_flips_its_kind()
    {
        Guid bab = await CreateBabAsync();
        (Guid dayLabourId, string phone) = await CreateAsync("Field Worker", EmployeeKind.DayLabour, bab);

        (await ArchiveAsync(dayLabourId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee archived = await reader.Employees.SingleAsync(e => e.Id == dayLabourId, Ct);

        archived.IsActive.Should().BeFalse();
        archived.Kind.Should().Be(
            EmployeeKind.DayLabour, "D-130 §7: archiving never flips Kind in place — that would be a move");
        archived.PhoneEntered.Should().Be(phone, "archiving touches IsActive alone");
    }

    /// <summary>
    /// D-130 §7's ruling — archive the day-labour record, register a new one — is built exactly as
    /// ruled: two independent operations (<c>ArchiveEmployee</c>, then <c>CreateEmployee</c>), with no
    /// <c>SetKind</c> anywhere for a "move" to even be expressed (<c>AC-208-D</c>).
    /// </summary>
    /// <remarks>
    /// <b>Ruled by decisions.md D-141/D-146, releasing this test's former "documented gap."</b> A
    /// cross-population match against an ARCHIVED record of the other kind is warn-and-acknowledge in
    /// both directions (D-146 point 4(a)) — the same real person keeps his phone when he moves from day
    /// labour onto the payroll, and refusing the match would make D-130 §7's ruled flow impossible.
    /// <c>ux_employees_salaried_phone</c> is scoped to <c>kind = 'Salaried'</c>, so it never sees a
    /// day-labour row on either side of this match; the mechanism here is the acknowledgement, not the
    /// index.
    /// </remarks>
    [Fact]
    public async Task Re_registering_the_same_person_by_phone_after_archiving_warns_and_succeeds_once_acknowledged()
    {
        Guid bab = await CreateBabAsync();
        (Guid dayLabourId, string phone) = await CreateAsync("Field Worker", EmployeeKind.DayLabour, bab);

        (await ArchiveAsync(dayLabourId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage checkResponse = await CheckAsync(phone);
        checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument checkBody = JsonDocument.Parse(await checkResponse.Content.ReadAsStringAsync(Ct));
        JsonElement[] matches = checkBody.RootElement.GetProperty("matches").EnumerateArray().ToArray();

        matches.Should().ContainSingle(match => match.GetProperty("id").GetGuid() == dayLabourId);
        JsonElement archivedMatch = matches.Single(match => match.GetProperty("id").GetGuid() == dayLabourId);
        archivedMatch.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        archivedMatch.GetProperty("name").GetString().Should().Be("Field Worker");

        HttpResponseMessage unacknowledged = await CreateRawAsync(
            "Field Worker", EmployeeKind.Salaried, phone, null, acknowledgedDuplicatePhone: false);

        unacknowledged.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(unacknowledged)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage reregistered = await CreateRawAsync(
            "Field Worker", EmployeeKind.Salaried, phone, null, acknowledgedDuplicatePhone: true);

        reregistered.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument createdBody = JsonDocument.Parse(await reregistered.Content.ReadAsStringAsync(Ct));
        Guid newSalariedId = createdBody.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();

        Employee original = await reader.Employees.SingleAsync(e => e.Id == dayLabourId, Ct);
        original.Kind.Should().Be(EmployeeKind.DayLabour, "the original record was never moved");
        original.IsActive.Should().BeFalse();

        Employee newSalaried = await reader.Employees.SingleAsync(e => e.Id == newSalariedId, Ct);
        newSalaried.Kind.Should().Be(EmployeeKind.Salaried);

        AuditRecord auditRecord = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.EntityId == dayLabourId,
            Ct);

        auditRecord.EntityType.Should().Be(nameof(Employee));
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<(Guid Id, string Phone)> CreateAsync(string fullName, EmployeeKind kind, Guid? babId)
    {
        string phone = UniqueNames.Phone().ToString();

        HttpResponseMessage response = await CreateRawAsync(fullName, kind, phone, babId);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<HttpResponseMessage> CreateRawAsync(
        string fullName, EmployeeKind kind, string phone, Guid? babId, bool acknowledgedDuplicatePhone = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName,
                phone,
                kind = kind.ToString(),
                babId,
                acknowledgedDuplicatePhone,
            }),
        };

        await StampAsync(request);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> CheckAsync(string phone)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/employees/phone-check", UriKind.Relative))
        {
            Content = JsonContent.Create(new { phone }),
        };

        await StampAsync(request);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(Guid id)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/employees/{id}/archive", UriKind.Relative));

        await StampAsync(request);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(UniqueNames.Code("EKI-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task StampAsync(HttpRequestMessage request)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, _hr.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, Role.Hr.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(_hr));
        request.Headers.Add(TestAuthHandler.DepartmentHeader, WellKnownDepartments.HrId.ToString());
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

        User hr = MakeUser("eki-hr", Role.Hr, WellKnownDepartments.HrId);

        context.Users.Add(hr);
        await context.SaveChangesAsync(Ct);

        _hr = hr.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
