using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-321 — department master data. <c>GET/POST/PUT /api/departments</c>,
/// <c>POST /api/departments/{id}/archive</c>, <c>POST /api/departments/{id}/unarchive</c>.
/// </summary>
/// <remarks>
/// No hard-delete path exists — decisions.md, Nabil's ruling 2026-09-15: archive-never-delete, same
/// shape as every other master record (catalogue items KAFF-206, babs KAFF-213, clients Q66/Q39).
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class DepartmentCrudTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _finance;

    public DepartmentCrudTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-321-A · five departments exist after seeding --------------------------------------

    [Fact]
    public async Task Five_departments_exist_after_seeding()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        // Not an equivalence assertion over the whole table: this fixture's database is shared across
        // every test in DatabaseCollection.Name (same shape as ArchiveBabTests and its siblings), and
        // other tests in the run add their own rows. What AC-321-A actually claims is that the five
        // named seed rows exist at their fixed ids — not that the table holds nothing else.
        List<string> names = await reader.Departments.Select(department => department.NameEn).ToListAsync(Ct);

        names.Should().Contain(
            ["Finance", "Technical Office", "Operations", "Procurement", "HR"],
            "decisions.md D-162 (Q85) names exactly these five");

        (await reader.Departments.SingleAsync(d => d.Id == WellKnownDepartments.FinanceId, Ct))
            .NameEn.Should().Be("Finance");
        (await reader.Departments.SingleAsync(d => d.Id == WellKnownDepartments.HrId, Ct))
            .NameEn.Should().Be("HR");
        (await reader.Departments.SingleAsync(d => d.Id == WellKnownDepartments.OperationsId, Ct))
            .NameEn.Should().Be("Operations");
    }

    // ---- AC-321-B · an admin creates a department ----------------------------------------------

    [Fact]
    public async Task An_admin_creates_a_department()
    {
        HttpResponseMessage response = await CreateAsync("التسويق", "Marketing");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        IReadOnlyList<(string NameAr, string NameEn)> listed = await ListAsync("active");

        listed.Should().Contain(row => row.NameEn == "Marketing" && row.NameAr == "التسويق");

        await using KaffDbContext reader = _database.CreateBareContext();
        Department created = await reader.Departments.SingleAsync(d => d.Id == id, Ct);
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Creating_a_department_with_a_blank_name_is_refused()
    {
        (await CreateAsync("", "Marketing")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreateAsync("التسويق", "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- AC-321-C · an admin edits a department, and it is audited -----------------------------

    [Fact]
    public async Task An_admin_edits_a_department_and_the_change_is_audited()
    {
        Guid id = await CreateDepartmentAsync("قسم قديم", "Old Department");

        HttpResponseMessage response = await EditAsync(id, "قسم جديد", "New Department");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Department edited = await reader.Departments.SingleAsync(d => d.Id == id, Ct);
        edited.NameEn.Should().Be("New Department");
        edited.NameAr.Should().Be("قسم جديد");

        AuditRecord record = await reader.AuditRecords
            .Where(r => r.EntityId == id && r.Action == AuditAction.Modified)
            .SingleAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(Department.NameEn));
        record.ChangedProperties.Should().Contain(nameof(Department.NameAr));
    }

    // ---- AC-321-D · a department with assigned staff is archived, never deleted ----------------

    [Fact]
    public async Task A_department_with_assigned_staff_can_be_archived()
    {
        Guid id = await CreateDepartmentAsync("قسم مشغول", "Busy Department");
        await AssignStaffAsync(id);

        HttpResponseMessage archiveResponse = await ArchiveAsync(id);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Departments.SingleAsync(d => d.Id == id, Ct)).IsActive.Should().BeFalse();
    }

    // ---- AC-321-E · an archived department stays valid on historical records --------------------

    [Fact]
    public async Task An_archived_department_stays_on_historical_records_but_is_excluded_from_new_assignment()
    {
        Guid id = await CreateDepartmentAsync("قسم للأرشفة", "To Archive");
        Guid staffId = await AssignStaffAsync(id);

        (await ArchiveAsync(id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Users.SingleAsync(u => u.Id == staffId, Ct)).DepartmentId.Should().Be(
            id, "the existing assignment is untouched by archiving");

        IReadOnlyList<(string NameAr, string NameEn)> activeOnly = await ListAsync("active");
        activeOnly.Should().NotContain(row => row.NameEn == "To Archive");

        IReadOnlyList<(string NameAr, string NameEn)> archived = await ListAsync("archived");
        archived.Should().Contain(row => row.NameEn == "To Archive");

        // A new assignment into the archived department is refused.
        HttpResponseMessage moveResponse = await MoveIntoDepartmentAsync(await CreateSecondStaffAsync(), id);
        moveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(moveResponse)).Should().Be("errors.master.department_is_archived");
    }

    // ---- AC-321-F · an archived department can be brought back with unarchive -------------------

    [Fact]
    public async Task An_archived_department_can_be_unarchived()
    {
        Guid id = await CreateDepartmentAsync("قسم فارغ", "Empty Department");
        (await ArchiveAsync(id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage unarchiveResponse = await UnarchiveAsync(id);
        unarchiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Departments.SingleAsync(d => d.Id == id, Ct)).IsActive.Should().BeTrue();

        IReadOnlyList<(string NameAr, string NameEn)> activeOnly = await ListAsync("active");
        activeOnly.Should().Contain(row => row.NameEn == "Empty Department");
    }

    [Fact]
    public async Task Unarchiving_a_department_that_is_not_archived_is_refused()
    {
        Guid id = await CreateDepartmentAsync("قسم نشط", "Active Department");

        HttpResponseMessage unarchiveResponse = await UnarchiveAsync(id);

        unarchiveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(unarchiveResponse)).Should().Be("errors.master.not_archived");
    }

    // ---- permission — a role without DepartmentManage is refused --------------------------------

    [Fact]
    public async Task A_role_without_department_manage_is_refused_every_department_act()
    {
        (await SendAsync(HttpMethod.Get, "/api/departments", _finance, Role.Finance, null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold DepartmentManage");

        (await CreateAsync("قسم", "Department", actorId: _finance, actorRole: Role.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Guid id = await CreateDepartmentAsync("قسم آخر", "Another Department");

        (await EditAsync(id, "معدل", "Edited", actorId: _finance, actorRole: Role.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await ArchiveAsync(id, actorId: _finance, actorRole: Role.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await UnarchiveAsync(id, actorId: _finance, actorRole: Role.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Departments.SingleAsync(d => d.Id == id, Ct)).IsActive.Should().BeTrue(
            "every refused attempt above changed nothing");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<Guid> CreateDepartmentAsync(string nameAr, string nameEn)
    {
        HttpResponseMessage response = await CreateAsync(nameAr, nameEn);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> CreateAsync(
        string nameAr, string nameEn, Guid? actorId = null, Role? actorRole = null)
        => SendAsync(HttpMethod.Post, "/api/departments", actorId ?? _owner, actorRole ?? Role.Owner,
            new { nameAr, nameEn });

    private Task<HttpResponseMessage> EditAsync(
        Guid id, string nameAr, string nameEn, Guid? actorId = null, Role? actorRole = null)
        => SendAsync(HttpMethod.Put, $"/api/departments/{id}", actorId ?? _owner, actorRole ?? Role.Owner,
            new { nameAr, nameEn });

    private Task<HttpResponseMessage> ArchiveAsync(Guid id, Guid? actorId = null, Role? actorRole = null)
        => SendAsync(HttpMethod.Post, $"/api/departments/{id}/archive", actorId ?? _owner, actorRole ?? Role.Owner, null);

    private Task<HttpResponseMessage> UnarchiveAsync(Guid id, Guid? actorId = null, Role? actorRole = null)
        => SendAsync(HttpMethod.Post, $"/api/departments/{id}/unarchive", actorId ?? _owner, actorRole ?? Role.Owner, null);

    private async Task<IReadOnlyList<(string NameAr, string NameEn)>> ListAsync(string status)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, $"/api/departments?status={status}", _owner, Role.Owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return [.. body.RootElement.GetProperty("items").EnumerateArray()
            .Select(element => (
                element.GetProperty("nameAr").GetString()!,
                element.GetProperty("nameEn").GetString()!))];
    }

    /// <summary>Registers a new staff user pointed at <paramref name="departmentId"/>. Returns the user id.</summary>
    private async Task<Guid> AssignStaffAsync(Guid departmentId)
    {
        await using KaffDbContext context = _database.CreateContext();

        User staff = User.Create(
            UniqueNames.Code("dept-staff"), "Department Staff", UniqueNames.Phone(),
            Role.MarketingSales, Now, departmentId).Value;

        context.Users.Add(staff);
        await context.SaveChangesAsync(Ct);

        return staff.Id;
    }

    private async Task<Guid> CreateSecondStaffAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User staff = User.Create(
            UniqueNames.Code("dept-staff2"), "Second Staff", UniqueNames.Phone(), Role.MarketingSales, Now).Value;

        context.Users.Add(staff);
        await context.SaveChangesAsync(Ct);

        return staff.Id;
    }

    private async Task<HttpResponseMessage> MoveIntoDepartmentAsync(Guid userId, Guid departmentId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put, new Uri($"/api/users/{userId}/department", UriKind.Relative))
        {
            Content = JsonContent.Create(new { departmentId, operationsSubDepartment = (string?)null }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, _owner.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, Role.Owner.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(_owner));

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        return await _client.SendAsync(request, Ct);
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users.Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp).SingleAsync(Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = User.Create(
            UniqueNames.Code("dept-owner"), "dept-owner", UniqueNames.Phone(), Role.Owner, Now).Value;
        User finance = User.Create(
            UniqueNames.Code("dept-finance"), "dept-finance", UniqueNames.Phone(), Role.Finance, Now,
            WellKnownDepartments.FinanceId).Value;

        context.Users.AddRange(owner, finance);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _finance = finance.Id;
    }

    private static DateTimeOffset Now => new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
