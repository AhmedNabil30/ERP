using System.Net;
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

/// <summary>KAFF-417 AC-417-J: an unsigned project is archived when its follow-up period ends, derived on read (D-208).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class FollowUpTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private readonly TestClock _clock = new();
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _technicalOfficeUnassigned;
    private Guid _itemId;

    public FollowUpTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database.ConnectionString, clock: _clock);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AC_417_J_a_project_is_not_archived_at_29_days_and_is_archived_at_30()
    {
        Guid project = await NewProjectAsync();

        _clock.Advance(TimeSpan.FromDays(29));
        (await GetProjectAsync(project)).GetProperty("isArchived").GetBoolean().Should().BeFalse();

        _clock.Advance(TimeSpan.FromDays(1));
        JsonElement archived = await GetProjectAsync(project);
        archived.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        archived.GetProperty("followUpEndsOn").ValueKind.Should().Be(JsonValueKind.String);
        archived.GetProperty("status").GetString().Should().Be("Setup", "archive is derived, not a new state");
    }

    [Fact]
    public async Task AC_417_J_a_signed_project_is_never_archived()
    {
        Guid project = await NewProjectAsync(withBoq: true);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { signedOn = "2026-09-30", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        _clock.Advance(TimeSpan.FromDays(400));

        JsonElement signed = await GetProjectAsync(project);
        signed.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        signed.GetProperty("followUpEndsOn").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AC_417_J_signing_an_archived_project_is_refused()
    {
        Guid project = await NewProjectAsync(withBoq: true);
        _clock.Advance(TimeSpan.FromDays(31));

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { signedOn = "2026-09-30", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.archived");
        (await LoadProjectAsync(project)).IsSigned.Should().BeFalse();
    }

    [Fact]
    public async Task AC_417_J_reopen_restarts_the_period_is_audited_and_lets_the_project_be_signed()
    {
        Guid project = await NewProjectAsync(withBoq: true);
        _clock.Advance(TimeSpan.FromDays(31));

        HttpResponseMessage reopened = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/reopen", _technicalOffice, null);

        reopened.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetProjectAsync(project)).GetProperty("isArchived").GetBoolean().Should().BeFalse();

        _clock.Advance(TimeSpan.FromDays(29));
        (await GetProjectAsync(project)).GetProperty("isArchived").GetBoolean().Should().BeFalse("the period restarted at the reopen");
        _clock.Advance(TimeSpan.FromDays(1));
        (await GetProjectAsync(project)).GetProperty("isArchived").GetBoolean().Should().BeTrue();

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            List<AuditRecord> records = await reader.AuditRecords
                .Where(r => r.EntityType == nameof(Project) && r.EntityId == project && r.Action == AuditAction.Modified)
                .ToListAsync(Ct);
            AuditRecord record = records.Single(r => r.ChangedProperties.Contains("FollowUpStartedOn"));
            record.ActorUserId.Should().Be(_technicalOffice);
        }

        // Reopened again, the project can then be signed.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/reopen", _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { signedOn = "2026-09-30", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AC_417_J_reopen_is_refused_when_the_project_is_not_archived()
    {
        Guid project = await NewProjectAsync();

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/reopen", _owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.project.not_archived");
    }

    [Fact]
    public async Task AC_417_J_the_list_hides_archived_projects_unless_asked()
    {
        Guid archived = await NewProjectAsync();
        _clock.Advance(TimeSpan.FromDays(10));
        Guid current = await NewProjectAsync();
        _clock.Advance(TimeSpan.FromDays(21)); // the first project is 31 days old, the second 21.

        string[] defaults = await ListIdsAsync("/api/projects");
        defaults.Should().Contain(current.ToString()).And.NotContain(archived.ToString());

        string[] all = await ListIdsAsync("/api/projects?includeArchived=true");
        all.Should().Contain([archived.ToString(), current.ToString()]);
    }

    [Fact]
    public async Task AC_417_J_a_technical_office_user_not_assigned_to_the_project_cannot_reopen_it()
    {
        Guid project = await NewProjectAsync();
        _clock.Advance(TimeSpan.FromDays(31));

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/reopen", _technicalOfficeUnassigned, null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetProjectAsync(project)).GetProperty("isArchived").GetBoolean().Should().BeTrue();
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private async Task<string[]> ListIdsAsync(string route)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, route, _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. body.RootElement.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("id").GetString()!)];
    }

    private async Task<JsonElement> GetProjectAsync(Guid project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/projects/{project}", _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private async Task<Guid> NewProjectAsync(bool withBoq = false)
    {
        await using KaffDbContext context = _database.CreateContext();

        Project project = Project.Create(UniqueNames.Code("FU-P"), UniqueNames.Code("مشروع المتابعة"), _clientId, ContractType.LumpSum, _clock.GetUtcNow()).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        User technicalOffice = await context.Users.SingleAsync(user => user.Id == _technicalOffice, Ct);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(project.Id, technicalOffice, AssignmentLevel.Standard, _owner, _clock.GetUtcNow()).Value);
        await context.SaveChangesAsync(Ct);

        if (withBoq)
        {
            (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project.Id}/boq/lines", _owner,
                new { catalogueItemId = _itemId, quantityEstimated = 5m, quantitySurveyed = 4m }))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        }

        return project.Id;
    }

    private async Task<Project> LoadProjectAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.SingleAsync(project => project.Id == id, Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Client client = Client.Create(UniqueNames.Code("FU-C"), "عميل المتابعة", UniqueNames.Phone(), ClientKind.Corporate, now).Value;
        User owner = User.Create(UniqueNames.Code("fu-owner"), "fu-owner", UniqueNames.Phone(), Role.Owner, now).Value;
        User technicalOffice = User.Create(
            UniqueNames.Code("fu-tech"), "fu-tech", UniqueNames.Phone(), Role.TechnicalOffice, now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        User technicalOfficeUnassigned = User.Create(
            UniqueNames.Code("fu-tech-un"), "fu-tech-un", UniqueNames.Phone(), Role.TechnicalOffice, now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        Bab bab = Bab.Create(UniqueNames.Code("FUB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code("FUI"), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;

        context.Clients.Add(client);
        context.Users.AddRange(owner, technicalOffice, technicalOfficeUnassigned);
        context.Babs.Add(bab);
        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _technicalOfficeUnassigned = technicalOfficeUnassigned.Id;
        _itemId = item.Id;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
