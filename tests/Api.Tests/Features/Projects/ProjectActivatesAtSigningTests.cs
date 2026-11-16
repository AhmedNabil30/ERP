using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Projects;

/// <summary>
/// D-230: signing makes the project Active in the same save. The whole chain runs as real requests: create, BOQ, sign, project
/// read, assignment, check-in, quantity, project read. Before the fix the check-in was refused with the Setup refusal.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectActivatesAtSigningTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _clientId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _engineer;
    private Guid _itemId;

    public ProjectActivatesAtSigningTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Client client = Client.Create(UniqueNames.Code("AS-C"), "عميل", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = User.Create(UniqueNames.Code("as-owner"), "as-owner", UniqueNames.Phone(), Role.Owner, Now).Value;
        User technicalOffice = User.Create(
            UniqueNames.Code("as-tech"), "as-tech", UniqueNames.Phone(), Role.TechnicalOffice, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        User engineer = User.Create(
            UniqueNames.Code("as-eng"), "as-eng", UniqueNames.Phone(), Role.SiteEngineer, Now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        Bab bab = Bab.Create(UniqueNames.Code("ASB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code("AS-CON-1"), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;
        context.Clients.Add(client);
        context.Users.AddRange(owner, technicalOffice, engineer);
        context.Babs.Add(bab);
        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);
        _clientId = client.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _engineer = engineer.Id;
        _itemId = item.Id;

        // The server clock sits on the day the entries are written, so "today" is 2026-09-25.
        _factory = new KaffApiFactory(
            _database.ConnectionString,
            clock: new TestClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero) - DateTimeOffset.UtcNow));
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task A_lump_sum_project_created_then_signed_is_active_and_takes_a_site_engineers_first_entry()
    {
        Guid project = await CreateAsync(ContractType.LumpSum);
        HttpResponseMessage line = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines", _technicalOffice,
            new { catalogueItemId = _itemId, quantityEstimated = 100m, quantitySurveyed = 100m });
        line.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid lineId = (await ReadAsync(line)).GetProperty("id").GetGuid();

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice,
                new { signedOn = "2026-09-25", holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement signed = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{project}", _owner, null));
        signed.GetProperty("status").GetString().Should().Be("Active", "D-230: signing makes the project active");
        signed.GetProperty("stage").GetString().Should().Be("Execution");
        signed.TryGetProperty("workStarted", out _).Should().BeFalse("no daily log entry exists yet, so the word stays لم تبدأ");

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/assignments", _owner,
                new { userId = _engineer, level = nameof(AssignmentLevel.Junior) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        string logs = $"/api/projects/{project}/daily-logs";
        HttpResponseMessage checkIn = await SendAsync(
            HttpMethod.Post, $"{logs}/check-ins", _engineer,
            new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0444, longitude = 31.2357, occurredAt = "2026-09-25T07:00:00+00:00" });
        checkIn.StatusCode.Should().Be(HttpStatusCode.Created, await checkIn.Content.ReadAsStringAsync(Ct));

        (await SendAsync(
                HttpMethod.Post, $"{logs}/quantities", _engineer,
                new { entryId = Guid.NewGuid(), boqLineId = lineId, quantity = 10m, occurredAt = "2026-09-25T09:00:00+00:00" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement worked = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{project}", _owner, null));
        worked.GetProperty("workStarted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_cost_plus_project_created_then_signed_is_active_and_takes_a_site_engineers_check_in()
    {
        Guid project = await CreateAsync(ContractType.CostPlus);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await LoadAsync(project)).Status.Should().Be(ProjectStatus.Active);

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/assignments", _owner,
                new { userId = _engineer, level = nameof(AssignmentLevel.Junior) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/daily-logs/check-ins", _engineer,
                new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0444, longitude = 31.2357, occurredAt = "2026-09-25T07:00:00+00:00" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_design_project_signed_with_its_area_becomes_active_by_the_design_table()
    {
        Guid project = await CreateAsync(ContractType.Design);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, new { areaSquareMetres = 200m }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Project stored = await LoadAsync(project);
        stored.Status.Should().Be(ProjectStatus.Active, "the design table allows Setup to Active, and signing is the trigger");
        stored.StartedOn.Should().Be(new DateOnly(2026, 9, 25));
    }

    [Fact]
    public async Task Signing_audits_the_status_change_from_setup_to_active_on_the_project()
    {
        Guid project = await CreateAsync(ContractType.CostPlus);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, new { signedOn = "2026-09-24" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        AuditRecord record = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Project) && r.EntityId == project && r.Action == AuditAction.Modified, Ct);
        record.ChangedProperties.Should().Contain(["Status", "SignedOn", "StartedOn"]);
        record.BeforeJson.Should().Contain("Setup");
        record.AfterJson.Should().Contain("Active");
        (await LoadAsync(project)).StartedOn.Should().Be(new DateOnly(2026, 9, 24), "activation takes the signature date");
    }

    [Fact]
    public async Task A_refused_signature_does_not_activate()
    {
        Guid project = await CreateAsync(ContractType.LumpSum);

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice,
                new { holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "no BOQ");

        (await LoadAsync(project)).Status.Should().Be(ProjectStatus.Setup);
    }

    private async Task<Guid> CreateAsync(ContractType type)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, "/api/projects", _technicalOffice,
            new { name = UniqueNames.Code("مشروع التفعيل"), clientId = _clientId, contractType = type.ToString() });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Project> LoadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.SingleAsync(project => project.Id == id, Ct);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users.Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());
        return await _client.SendAsync(request, Ct);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
