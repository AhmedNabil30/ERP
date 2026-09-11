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
/// decisions.md D-141 and D-146 — the warn-and-acknowledge mechanism for day labour, and the
/// partial-unique refusal for salaried staff, as one mechanism dispatched by <c>Kind</c>. KAFF-207,
/// KAFF-208.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmployeePhoneMatchTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _hr;

    public EmployeePhoneMatchTests(PostgresDatabase database) => _database = database;

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

    // ---- D-141, AC-209-C · the match is on the normalised form, day labour both sides -------------

    [Fact]
    public async Task Employee_phone_match_is_on_the_normalised_form()
    {
        Guid bab = await CreateBabAsync();

        (Guid firstId, _) = await CreateAsync(
            "First Worker", EmployeeKind.DayLabour, "+20 100 123 4567", bab, acknowledgedDuplicatePhone: false);

        HttpResponseMessage checkResponse = await CheckAsync("0020 100 1234567");
        checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument checkBody = JsonDocument.Parse(await checkResponse.Content.ReadAsStringAsync(Ct));
        JsonElement[] matches = checkBody.RootElement.GetProperty("matches").EnumerateArray().ToArray();

        matches.Should().ContainSingle(match => match.GetProperty("id").GetGuid() == firstId);

        HttpResponseMessage second = await CreateRawAsync(
            "Second Worker", EmployeeKind.DayLabour, "01001234567", bab, acknowledgedDuplicatePhone: true);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- D-141 · unacknowledged duplicate refuses and writes nothing, day labour both sides --------

    [Fact]
    public async Task An_unacknowledged_duplicate_employee_phone_is_refused_and_writes_nothing()
    {
        Guid bab = await CreateBabAsync();
        string phone = UniqueNames.Phone().ToString();

        await CreateAsync("First Worker", EmployeeKind.DayLabour, phone, bab, acknowledgedDuplicatePhone: false);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.Employees.LongCountAsync(Ct);
        long auditBefore = await before.AuditRecords.LongCountAsync(Ct);

        HttpResponseMessage refused = await CreateRawAsync(
            "Second Worker", EmployeeKind.DayLabour, phone, bab, acknowledgedDuplicatePhone: false);

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(refused)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.Employees.LongCountAsync(Ct)).Should().Be(countBefore, "the refused create wrote no row");
        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(auditBefore, "the refused create wrote no audit record");
    }

    // ---- D-141, excluding ---------------------------------------------------------------------------

    [Fact]
    public async Task Editing_an_employee_without_changing_the_phone_does_not_warn()
    {
        Guid bab = await CreateBabAsync();
        (Guid id, string phone) = await CreateAsync("Worker", EmployeeKind.DayLabour, bab, acknowledgedDuplicatePhone: false);

        HttpResponseMessage edited = await EditAsync(id, "Worker Renamed", phone, EmployeeKind.DayLabour, bab, acknowledgedDuplicatePhone: false);

        edited.StatusCode.Should().Be(
            HttpStatusCode.OK, "a record saved with its own phone unchanged must never match itself (excluding)");
    }

    // ---- D-146 test 3 · a salaried phone matching an archived salaried record is refused ------------

    [Fact]
    public async Task A_salaried_phone_matching_an_archived_salaried_record_is_refused()
    {
        (Guid firstId, string phone) = await CreateAsync(
            "First Salaried", EmployeeKind.Salaried, UniqueNames.Phone().ToString(), null, acknowledgedDuplicatePhone: false);

        (await ArchiveAsync(firstId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage second = await CreateRawAsync(
            "Second Salaried", EmployeeKind.Salaried, phone, null, acknowledgedDuplicatePhone: true);

        second.StatusCode.Should().Be(
            HttpStatusCode.Conflict, "D-146 point 1: the partial index covers archived salaried rows too");
        (await MessageKeyAsync(second)).Should().Be("errors.master.employee_phone_taken");
    }

    // ---- D-146 test 4 · editing a salaried record onto another salaried phone is refused -------------

    [Fact]
    public async Task Editing_a_salaried_record_onto_another_salaried_phone_is_refused()
    {
        (_, string firstPhone) = await CreateAsync(
            "First Salaried", EmployeeKind.Salaried, UniqueNames.Phone().ToString(), null, acknowledgedDuplicatePhone: false);

        (Guid secondId, string secondPhone) = await CreateAsync(
            "Second Salaried", EmployeeKind.Salaried, UniqueNames.Phone().ToString(), null, acknowledgedDuplicatePhone: false);

        HttpResponseMessage edited = await EditAsync(
            secondId, "Second Salaried", firstPhone, EmployeeKind.Salaried, null, acknowledgedDuplicatePhone: true);

        edited.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(edited)).Should().Be("errors.master.employee_phone_taken");

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee stored = await reader.Employees.SingleAsync(e => e.Id == secondId, Ct);
        stored.PhoneNormalised.Should().Be(
            PhoneNumber.Create(secondPhone).Value.Normalised, "the refused edit changed nothing");
    }

    // ---- D-146 test 5 · racing salaried creates leave one row ----------------------------------------

    [Fact]
    public async Task Two_salaried_creates_racing_on_one_phone_leave_one_row()
    {
        string phone = UniqueNames.Phone().ToString();

        Task<HttpResponseMessage> first = CreateRawAsync(
            "Racer A", EmployeeKind.Salaried, phone, null, acknowledgedDuplicatePhone: true);
        Task<HttpResponseMessage> second = CreateRawAsync(
            "Racer B", EmployeeKind.Salaried, phone, null, acknowledgedDuplicatePhone: true);

        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Created);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Conflict);

        HttpResponseMessage conflicted = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        (await MessageKeyAsync(conflicted)).Should().Be("errors.master.employee_phone_taken");

        string normalised = PhoneNumber.Create(phone).Value.Normalised;
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Employees.CountAsync(e => e.PhoneNormalised == normalised, Ct)).Should().Be(1);
    }

    // ---- D-146 point 4(a), reverse direction · day labour vs an archived salaried record -------------

    [Fact]
    public async Task A_day_labourer_matching_an_archived_salaried_record_warns_and_succeeds_once_acknowledged()
    {
        Guid bab = await CreateBabAsync();

        (Guid salariedId, string phone) = await CreateAsync(
            "Former Staff", EmployeeKind.Salaried, UniqueNames.Phone().ToString(), null, acknowledgedDuplicatePhone: false);

        (await ArchiveAsync(salariedId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage unacknowledged = await CreateRawAsync(
            "New Worker", EmployeeKind.DayLabour, phone, bab, acknowledgedDuplicatePhone: false);

        unacknowledged.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(unacknowledged)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage acknowledged = await CreateRawAsync(
            "New Worker", EmployeeKind.DayLabour, phone, bab, acknowledgedDuplicatePhone: true);

        acknowledged.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord auditRecord = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.EntityId == salariedId,
            Ct);

        auditRecord.EntityType.Should().Be(nameof(Employee));
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>Generates its own phone, asserts <c>201</c>, and returns the id and the phone used.</summary>
    private async Task<(Guid Id, string Phone)> CreateAsync(
        string fullName, EmployeeKind kind, Guid? babId, bool acknowledgedDuplicatePhone)
        => await CreateAsync(fullName, kind, UniqueNames.Phone().ToString(), babId, acknowledgedDuplicatePhone);

    /// <summary>Explicit phone, asserts <c>201</c>, and returns the id and the phone used.</summary>
    private async Task<(Guid Id, string Phone)> CreateAsync(
        string fullName, EmployeeKind kind, string phone, Guid? babId, bool acknowledgedDuplicatePhone)
    {
        HttpResponseMessage response = await CreateRawAsync(fullName, kind, phone, babId, acknowledgedDuplicatePhone);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<HttpResponseMessage> CreateRawAsync(
        string fullName, EmployeeKind kind, string phone, Guid? babId, bool acknowledgedDuplicatePhone)
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

    private async Task<HttpResponseMessage> EditAsync(
        Guid id, string fullName, string phone, EmployeeKind kind, Guid? babId, bool acknowledgedDuplicatePhone)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/employees/{id}", UriKind.Relative))
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

        Bab bab = Bab.Create(UniqueNames.Code("EPM-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

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

        User hr = User.Create(UniqueNames.Code("epm-hr"), "epm-hr", UniqueNames.Phone(), Role.Hr, Now, Department.Hr).Value;

        context.Users.Add(hr);
        await context.SaveChangesAsync(Ct);

        _hr = hr.Id;
    }

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
