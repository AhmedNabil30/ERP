using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-207 — <c>POST /api/employees/{employeeId}/archive</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ArchiveEmployeeTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;

    public ArchiveEmployeeTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-207-F · archived, not deleted; excluded by default, findable through the filter -------

    [Fact]
    public async Task An_employee_is_archived_the_row_survives_intact_and_the_trail_names_the_change()
    {
        (Guid id, string phone) = await CreateEmployeeAsync();

        (await ArchiveAsync(id, _hr, Role.Hr, Department.Hr)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Employee stored = await ReadAsync(id);
        stored.IsActive.Should().BeFalse();
        stored.FullName.Should().Be("Original Name", "archiving touches IsActive and nothing else");
        stored.PhoneEntered.Should().Be(phone);

        (await ListAsync()).Should().NotContain(id, "the default list excludes archived employees — AC-207-F");
        (await ListAsync(status: "archived")).Should().Contain(id, "reachable through the explicit archived filter");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.EntityType.Should().Be(nameof(Employee));
        record.ActorUserId.Should().Be(_hr);
        record.ChangedProperties.Should().Contain(nameof(Employee.IsActive));
        record.GrantPath.Should().BeNull("EmployeeManage is company-wide");
    }

    // ---- AC-207-F (second half) · archiving twice is refused, and the refusal writes nothing -------

    [Fact]
    public async Task Archiving_twice_is_refused_and_writes_no_further_audit_record()
    {
        (Guid id, _) = await CreateEmployeeAsync();

        (await ArchiveAsync(id, _hr, Role.Hr, Department.Hr)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        HttpResponseMessage again = await ArchiveAsync(id, _hr, Role.Hr, Department.Hr);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using JsonDocument problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.already_archived");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct)).Should().Be(countBefore);
    }

    [Fact]
    public async Task Archiving_an_employee_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await ArchiveAsync(Guid.NewGuid(), _hr, Role.Hr, Department.Hr);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.employee_not_found");
    }

    // ---- AC-207-F (third half) · no route deletes an employee, under any verb ---------------------

    [Fact]
    public void No_route_under_employees_deletes_anything()
    {
        Assembly shipped = typeof(PermissionRequirement).Assembly;

        List<string> employeeRoutes = [];
        List<string> deleteRoutes = [];

        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (Microsoft.AspNetCore.Http.Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint route)
                {
                    continue;
                }

                Assembly? handler = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly;

                if (handler is not null && handler != shipped)
                {
                    continue;
                }

                string pattern = route.RoutePattern.RawText ?? string.Empty;

                if (!pattern.StartsWith("/api/employees", StringComparison.Ordinal))
                {
                    continue;
                }

                employeeRoutes.Add(pattern);

                HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

                if (methods?.HttpMethods.Contains(HttpMethods.Delete, StringComparer.OrdinalIgnoreCase) == true)
                {
                    deleteRoutes.Add(pattern);
                }
            }
        }

        employeeRoutes.Should().NotBeEmpty(
            "the enumeration must find real, mapped employee routes — an empty table would pass this "
            + "test regardless of what exists (D-116, V-32-A's lesson)");

        deleteRoutes.Should().BeEmpty("an employee is archived and never deleted — CLAUDE.md, D-049 ruling 5");
    }

    // ---- AC-207-H · a role without EmployeeManage cannot archive -----------------------------------

    [Fact]
    public async Task Only_hr_and_the_owner_may_archive_an_employee()
    {
        (Guid id, _) = await CreateEmployeeAsync();

        (await ArchiveAsync(id, _finance, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance does not hold EmployeeManage");

        (await ReadAsync(id)).IsActive.Should().BeTrue("the refused call changed nothing");

        (Guid ownerTarget, _) = await CreateEmployeeAsync();

        (await ArchiveAsync(ownerTarget, _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner holds every company-wide row");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<(Guid Id, string Phone)> CreateEmployeeAsync()
    {
        string phone = UniqueNames.Phone().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName = "Original Name",
                phone,
                kind = nameof(EmployeeKind.Salaried),
            }),
        };

        await StampAsync(request, _hr, Role.Hr, Department.Hr);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(Guid id, Guid actorId, Role actorRole, Department? actorDepartment)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/employees/{id}/archive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<List<Guid>> ListAsync(string status = "active")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/employees?status={status}", UriKind.Relative));

        await StampAsync(request, _hr, Role.Hr, Department.Hr);

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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("arce-owner", Role.Owner);
        User hr = MakeUser("arce-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("arce-finance", Role.Finance, Department.Finance);

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
