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
/// D-233 item 1 — <c>POST /api/employees/{employeeId}/unarchive</c>, the catalogue recoverable pattern.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UnarchiveEmployeeTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;

    public UnarchiveEmployeeTests(PostgresDatabase database) => _database = database;

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
    public async Task An_archived_employee_is_restored_in_place_and_the_trail_names_the_change()
    {
        (Guid id, string phone) = await CreateEmployeeAsync();
        (await ArchiveAsync(id, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UnarchiveAsync(id, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Employee stored = await ReadAsync(id);
        stored.IsActive.Should().BeTrue();
        stored.PhoneEntered.Should().Be(phone, "restoring touches IsActive and nothing else");
        (await ListAsync()).Should().Contain(id);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_hr);
        record.ChangedProperties.Should().Contain(nameof(Employee.IsActive));
        record.GrantPath.Should().BeNull("EmployeeManage is company-wide");
    }

    [Fact]
    public async Task Restoring_an_active_employee_is_refused_and_writes_no_audit_record()
    {
        (Guid id, _) = await CreateEmployeeAsync();

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        HttpResponseMessage response = await UnarchiveAsync(id, _hr, Role.Hr, WellKnownDepartments.HrId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.not_archived");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(countBefore);
    }

    [Fact]
    public async Task A_salaried_employee_whose_phone_an_active_salaried_record_now_holds_stays_archived()
    {
        (Guid leaver, string phone) = await CreateEmployeeAsync();
        (await ArchiveAsync(leaver, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await CreateEmployeeAsync(phone);

        HttpResponseMessage response = await UnarchiveAsync(leaver, _hr, Role.Hr, WellKnownDepartments.HrId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.employee_phone_taken");
        (await ReadAsync(leaver)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task A_day_labour_worker_is_restored_even_when_the_phone_is_held_elsewhere_because_that_match_only_warns()
    {
        Guid bab = await CreateBabAsync();
        (Guid worker, string phone) = await CreateEmployeeAsync(kind: EmployeeKind.DayLabour, babId: bab);
        (await ArchiveAsync(worker, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await CreateEmployeeAsync(phone);

        (await UnarchiveAsync(worker, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadAsync(worker)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Restoring_an_employee_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await UnarchiveAsync(Guid.NewGuid(), _hr, Role.Hr, WellKnownDepartments.HrId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.employee_not_found");
    }

    [Fact]
    public async Task Only_hr_and_the_owner_may_restore_an_employee()
    {
        (Guid id, _) = await CreateEmployeeAsync();
        (await ArchiveAsync(id, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UnarchiveAsync(id, _finance, Role.Finance, WellKnownDepartments.FinanceId))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold EmployeeManage");
        (await ReadAsync(id)).IsActive.Should().BeFalse("the refused call changed nothing");

        (await UnarchiveAsync(id, _owner, Role.Owner, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Hr_reads_a_restored_day_labour_worker_among_the_active_rows()
    {
        // pinning: restore returns the row to the default list for its own population
        Guid bab = await CreateBabAsync();
        (Guid worker, _) = await CreateEmployeeAsync(kind: EmployeeKind.DayLabour, babId: bab);
        (await ArchiveAsync(worker, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync()).Should().NotContain(worker);

        (await UnarchiveAsync(worker, _hr, Role.Hr, WellKnownDepartments.HrId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync()).Should().Contain(worker);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<(Guid Id, string Phone)> CreateEmployeeAsync(
        string? phone = null, EmployeeKind kind = EmployeeKind.Salaried, Guid? babId = null)
    {
        phone ??= UniqueNames.Phone().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName = "Original Name",
                phone,
                kind = kind.ToString(),
                babId,
                acknowledgedDuplicatePhone = true,
            }),
        };

        await StampAsync(request, _hr, Role.Hr, WellKnownDepartments.HrId);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(Guid id, Guid actorId, Role actorRole, Guid? actorDepartment)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/employees/{id}/archive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> UnarchiveAsync(Guid id, Guid actorId, Role actorRole, Guid? actorDepartment)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/employees/{id}/unarchive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(UniqueNames.Code("UNA-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;
        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task<List<Guid>> ListAsync(string status = "active")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/employees?status={status}", UriKind.Relative));

        await StampAsync(request, _hr, Role.Hr, WellKnownDepartments.HrId);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return [.. body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private async Task<Employee> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Employees.SingleAsync(candidate => candidate.Id == id, Ct);
    }

    private async Task StampAsync(HttpRequestMessage request, Guid actorId, Role actorRole, Guid? actorDepartment)
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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("unarce-owner", Role.Owner);
        User hr = MakeUser("unarce-hr", Role.Hr, WellKnownDepartments.HrId);
        User finance = MakeUser("unarce-finance", Role.Finance, WellKnownDepartments.FinanceId);

        context.Users.AddRange(owner, hr, finance);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
