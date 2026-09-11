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
using Npgsql;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-207 — <c>POST /api/employees</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CreateEmployeeTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _headOfDesign;
    private Guid _marketing;

    public CreateEmployeeTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-207-A · a salaried employee is created ------------------------------------------------

    [Fact]
    public async Task A_salaried_employee_is_created_active_with_the_values_submitted()
    {
        string phone = UniqueNames.Phone().ToString();

        HttpResponseMessage response = await CreateAsync(Body("Ahmed Ali", phone, EmployeeKind.Salaried));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement root = body.RootElement;

        root.GetProperty("fullName").GetString().Should().Be("Ahmed Ali");
        root.GetProperty("phone").GetString().Should().Be(phone);
        root.GetProperty("kind").GetString().Should().Be(nameof(EmployeeKind.Salaried));
        root.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    // ---- D-139 §7 / D-144 §2 · the staff file carries Department, free text ------------------------

    [Fact]
    public async Task A_salaried_employee_is_created_with_a_department()
    {
        HttpResponseMessage response = await CreateAsync(new
        {
            fullName = "Ahmed Ali",
            phone = UniqueNames.Phone().ToString(),
            kind = nameof(EmployeeKind.Salaried),
            department = "Finance",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("department").GetString().Should().Be("Finance");
    }

    [Fact]
    public async Task Day_labour_registration_carries_a_department_the_same_as_national_id_and_job_title()
    {
        // No rule refuses Department for day labour, the same as NationalId and JobTitle today —
        // SetStaffDetails stores whatever the request carries, regardless of Kind.
        Guid bab = await CreateBabAsync();

        HttpResponseMessage response = await CreateAsync(new
        {
            fullName = "Worker",
            phone = UniqueNames.Phone().ToString(),
            kind = nameof(EmployeeKind.DayLabour),
            babId = bab,
            department = "Site A",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("department").GetString().Should().Be(
            "Site A", "Department follows the same unguarded pattern as NationalId and JobTitle");
    }

    // ---- AC-207-G · the code is generated, never typed, never editable ----------------------------

    [Fact]
    public async Task The_employee_code_is_generated_by_the_system_and_the_request_carries_none()
    {
        HttpResponseMessage response = await CreateAsync(
            Body("Generated Code Person", UniqueNames.Phone().ToString(), EmployeeKind.Salaried));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("code").GetString().Should().MatchRegex(
            "^E-[0-9]+$", "D-130 §6: the reference number is generated, the same shape as C-10001");
    }

    // ---- AC-207-B · day labour without a باب is refused, in the entity and in the database --------

    [Fact]
    public async Task Day_labour_with_no_bab_is_refused_by_the_handler()
    {
        HttpResponseMessage response = await CreateAsync(new
        {
            fullName = "Worker",
            phone = UniqueNames.Phone().ToString(),
            kind = nameof(EmployeeKind.DayLabour),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.day_labour_requires_trade");
    }

    [Fact]
    public async Task Day_labour_with_no_bab_is_also_refused_by_the_database_check_constraint()
    {
        // Bypasses Employee.Create entirely — raw SQL, same shape as CreateBabTests' equivalent.
        // spec.md's requirement is checked at the table, not only by the entity guard.
        await using KaffDbContext context = _database.CreateContext();

        Func<Task> insert = async () => await context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO employees
                 (id, code, full_name, phone_entered, phone_normalised, kind, bab_id, is_active, created_at)
               VALUES
                 (gen_random_uuid(), {UniqueNames.Code("RAWEMP")}, 'Raw Insert', '01000000000',
                  '01000000000', 'DayLabour', NULL, TRUE, now())",
            Ct);

        (await insert.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    // ---- AC-207-D / AC-208-B · one person, one record — the unique phone index is the mechanism ----

    [Fact]
    public async Task The_same_phone_cannot_be_registered_twice_and_the_refusal_is_the_unique_index()
    {
        string phone = UniqueNames.Phone().ToString();

        (await CreateAsync(Body("First Person", phone, EmployeeKind.Salaried)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage second = await CreateAsync(Body("Second Person", phone, EmployeeKind.Salaried));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(second)).Should().Be(
            "errors.master.employee_phone_taken",
            "AC-207-D and AC-208-B share this exact mechanism — ux_employees_phone — because there is "
            + "one table for both populations");
    }

    [Fact]
    public async Task A_day_labourer_cannot_be_registered_again_as_salaried_with_the_same_phone()
    {
        Guid bab = await CreateBabAsync();
        string phone = UniqueNames.Phone().ToString();

        (await CreateAsync(new
        {
            fullName = "Worker",
            phone,
            kind = nameof(EmployeeKind.DayLabour),
            babId = bab,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage secondPopulation = await CreateAsync(Body("Worker", phone, EmployeeKind.Salaried));

        secondPopulation.StatusCode.Should().Be(
            HttpStatusCode.Conflict, "AC-208-B: one person cannot be created into both populations");
        (await MessageKeyAsync(secondPopulation)).Should().Be("errors.master.employee_phone_taken");
    }

    // ---- AC-207-H · a role without EmployeeManage reaches nothing ----------------------------------

    [Fact]
    public async Task Only_hr_and_the_owner_may_register_an_employee()
    {
        foreach ((Guid actor, Role role, Department? department) in RefusedActors())
        {
            (await CreateAsync(
                Body($"Refused {role}", UniqueNames.Phone().ToString(), EmployeeKind.Salaried), actor, role, department))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden, "{0} does not hold EmployeeManage — spec.md §2, §10, D-129 §1", role);
        }

        (await CreateAsync(Body("Owner-created", UniqueNames.Phone().ToString(), EmployeeKind.Salaried), _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the Owner holds every company-wide row — D-129 §1");
    }

    // ---- AC-207-I (creation half) · the trail names who created it --------------------------------

    [Fact]
    public async Task Creation_is_audited_and_names_the_actor()
    {
        HttpResponseMessage response = await CreateAsync(
            Body("Audited Person", UniqueNames.Phone().ToString(), EmployeeKind.Salaried));

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            record => record.EntityId == id && record.Action == AuditAction.Created, Ct);

        record.EntityType.Should().Be(nameof(Employee));
        record.ActorUserId.Should().Be(_hr);
        record.GrantPath.Should().BeNull("EmployeeManage is company-wide: no project, no path to name");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(string fullName, string phone, EmployeeKind kind) => new
    {
        fullName,
        phone,
        kind = kind.ToString(),
    };

    private IEnumerable<(Guid Actor, Role Role, Department? Department)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance);
        yield return (_technicalOffice, Role.TechnicalOffice, Department.Operations);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations);
        yield return (_marketing, Role.MarketingSales, Department.Marketing);
    }

    private Task<HttpResponseMessage> CreateAsync(
        object body, Guid? actorId = null, Role? actorRole = null, Department? actorDepartment = null)
        => SendAsync(
            HttpMethod.Post,
            "/api/employees",
            actorId ?? _hr,
            actorRole ?? Role.Hr,
            actorDepartment ?? Department.Hr,
            body);

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(
            UniqueNames.Code("CRE-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

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

        User owner = MakeUser("cre-owner", Role.Owner);
        User hr = MakeUser("cre-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("cre-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser(
            "cre-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser(
            "cre-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "cre-design", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("cre-marketing", Role.MarketingSales, Department.Marketing);

        context.Users.AddRange(owner, hr, finance, technicalOffice, siteEngineer, headOfDesign, marketing);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
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
