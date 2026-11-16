using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Projects;

/// <summary>
/// D-228 item 4 (a signed project with no daily log entry reads لم تبدأ, جاري العمل from its first entry) and the answers the
/// project frame gives after signing: its stage, and whether conversion is offered (D-228 item 2).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectWorkDisplayTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _owner;
    private Guid _clientId;

    public ProjectWorkDisplayTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Client client = Client.Create(UniqueNames.Code("WD-C"), "عميل", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = User.Create(UniqueNames.Code("wd-owner"), "wd-owner", UniqueNames.Phone(), Role.Owner, Now).Value;
        context.Clients.Add(client);
        context.Users.Add(owner);
        await context.SaveChangesAsync(Ct);
        _clientId = client.Id;
        _owner = owner.Id;

        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task A_signed_project_with_a_log_but_no_entry_has_not_started_work()
    {
        Guid project = await SeedActiveProjectAsync(withEntry: false);

        using JsonDocument read = await GetAsync($"/api/projects/{project}");
        WorkStarted(read.RootElement).Should().BeFalse();

        using JsonDocument list = await GetAsync("/api/projects");
        WorkStarted(ListRow(list, project)).Should().BeFalse();
    }

    [Fact]
    public async Task The_first_log_entry_of_any_kind_starts_work_on_the_read_and_the_list()
    {
        Guid empty = await SeedActiveProjectAsync(withEntry: false);
        Guid started = await SeedActiveProjectAsync(withEntry: true);

        using JsonDocument read = await GetAsync($"/api/projects/{started}");
        WorkStarted(read.RootElement).Should().BeTrue();

        using JsonDocument list = await GetAsync("/api/projects");
        WorkStarted(ListRow(list, started)).Should().BeTrue();
        WorkStarted(ListRow(list, empty)).Should().BeFalse("the list answers each project on its own entries");
    }

    [Fact]
    public async Task A_just_signed_project_is_on_the_execution_stage()
    {
        Guid project = await SeedProjectAsync(ContractType.LumpSum, signed: true);

        using JsonDocument read = await GetAsync($"/api/projects/{project}");

        read.RootElement.GetProperty("status").GetString().Should().Be("Active", "D-230: signing makes the project active");
        read.RootElement.GetProperty("stage").GetString().Should().Be("Execution", "signing ends the BOQ stage");
    }

    [Fact]
    public async Task An_unsigned_project_stays_on_the_boq_stage()
    {
        Guid project = await SeedProjectAsync(ContractType.LumpSum, signed: false);

        using JsonDocument read = await GetAsync($"/api/projects/{project}");

        read.RootElement.GetProperty("stage").GetString().Should().Be("Boq");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Conversion_is_offered_only_for_a_signed_design_project(bool isSigned, bool offered)
    {
        Guid project = await SeedProjectAsync(ContractType.Design, isSigned);

        using JsonDocument read = await GetAsync($"/api/projects/{project}");

        read.RootElement.GetProperty("conversionOffered").GetBoolean().Should().Be(offered);
    }

    [Fact]
    public async Task Conversion_is_not_offered_for_a_lump_sum_project()
    {
        Guid project = await SeedProjectAsync(ContractType.LumpSum, signed: true);

        using JsonDocument read = await GetAsync($"/api/projects/{project}");

        read.RootElement.GetProperty("conversionOffered").GetBoolean().Should().BeFalse();
    }

    private static bool WorkStarted(JsonElement row) => row.TryGetProperty("workStarted", out JsonElement value) && value.GetBoolean();

    private static JsonElement ListRow(JsonDocument list, Guid project) =>
        list.RootElement.GetProperty("projects").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == project);

    private async Task<Guid> SeedActiveProjectAsync(bool withEntry)
    {
        Guid id = await SeedProjectAsync(ContractType.LumpSum, signed: true);
        await using KaffDbContext context = _database.CreateContext();
        RulingSet rulings = new();
        DailyLog log = DailyLog.Open(id, _owner, Now, Now, rulings.DailyLogEntry);
        context.DailyLogs.Add(log);
        if (withEntry)
        {
            context.FieldEntries.Add(
                log.RecordField(Guid.CreateVersion7(), DailyLogField.Note, "ملاحظة", rulings.DailyLogEntry, Now, Now).Value);
        }

        await context.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> SeedProjectAsync(ContractType type, bool signed)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("WD-P"), UniqueNames.Code("WD-N"), _clientId, type, DateTimeOffset.UtcNow).Value;
        if (type == ContractType.Design && signed)
        {
            project.SetDesignTerms(100m, new Money(450m)).IsSuccess.Should().BeTrue();
        }

        if (signed)
        {
            project.Sign(new DateOnly(2026, 9, 25), type == ContractType.LumpSum ? new Money(1_000_000m) : null).IsSuccess.Should().BeTrue();
        }

        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);
        return project.Id;
    }

    private async Task<JsonDocument> GetAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users.Where(user => user.Id == _owner)
            .Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);
        request.Headers.Add(TestAuthHandler.UserIdHeader, _owner.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());
        using HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, path);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
