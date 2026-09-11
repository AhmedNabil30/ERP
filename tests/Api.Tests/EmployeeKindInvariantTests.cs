using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
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
    /// <b>What this test documents rather than hides:</b> <c>ux_employees_phone</c> is a full-table
    /// unique index (KAFF-207/208's own "one mechanism, three places" table), not one scoped to active
    /// rows — the same non-partial-index convention <c>ux_babs_code</c> and
    /// <c>ux_catalogue_items_code</c> already use, where an archived row's identity stays reserved
    /// forever. Registering the new salaried record for <i>the same real person</i> — the only way the
    /// system can recognise it as the same person at all, since phone is the sole cross-reference — is
    /// therefore refused today, by the very index that is this story's own enforcement of "nobody
    /// appears in both". Loosening that index (a partial unique index scoped to <c>IsActive</c>, say)
    /// is a phone-deduplication rule this session was told to stop on rather than invent: <c>Q70</c>
    /// is open with Karim on exactly this axis. Reported in this commit rather than fixed silently.
    /// </remarks>
    [Fact]
    public async Task Re_registering_the_same_person_by_phone_after_archiving_is_currently_blocked_by_the_phone_index()
    {
        Guid bab = await CreateBabAsync();
        (Guid dayLabourId, string phone) = await CreateAsync("Field Worker", EmployeeKind.DayLabour, bab);

        (await ArchiveAsync(dayLabourId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage reregistered = await CreateRawAsync("Field Worker", EmployeeKind.Salaried, phone, null);

        reregistered.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "documented gap: ux_employees_phone has no exception for an archived original, so the same "
            + "phone cannot be re-registered — see this test's remarks and the session report");

        (await MessageKeyAsync(reregistered)).Should().Be("errors.master.employee_phone_taken");

        // The mechanism works cleanly when the new record carries its own distinct phone — proving
        // the archive-then-create shape itself, independent of the phone-index gap above.
        HttpResponseMessage distinctPhone = await CreateRawAsync(
            "Field Worker", EmployeeKind.Salaried, UniqueNames.Phone().ToString(), null);

        distinctPhone.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee original = await reader.Employees.SingleAsync(e => e.Id == dayLabourId, Ct);
        original.Kind.Should().Be(EmployeeKind.DayLabour, "the original record was never moved");
        original.IsActive.Should().BeFalse();
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

    private async Task<HttpResponseMessage> CreateRawAsync(string fullName, EmployeeKind kind, string phone, Guid? babId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName,
                phone,
                kind = kind.ToString(),
                babId,
            }),
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
        request.Headers.Add(TestAuthHandler.DepartmentHeader, Department.Hr.ToString());
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

        User hr = MakeUser("eki-hr", Role.Hr, Department.Hr);

        context.Users.Add(hr);
        await context.SaveChangesAsync(Ct);

        _hr = hr.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
