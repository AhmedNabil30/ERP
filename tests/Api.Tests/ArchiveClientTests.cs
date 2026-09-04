using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Features.Clients;
using Kaff.Api.Features.Clients.ListClients;
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
/// KAFF-123 — <c>POST /api/clients/{clientId}/archive</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ArchiveClientTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _marketing;
    private Guid _finance;
    private Guid _hr;
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public ArchiveClientTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-123-A · leaves the working list, not the database ---------------------------------

    [Fact]
    public async Task An_archived_client_leaves_the_default_list_and_the_row_survives()
    {
        string nonce = UniqueNames.Code("ARV");
        (Guid id, string code) = await RegisterAsync($"شركة {nonce} للمقاولات");

        (await ArchiveAsync(id, _marketing, Role.MarketingSales, Department.Marketing))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SearchAsync(nonce)).Select(client => client.Code).Should().NotContain(
            code, "archived clients leave the working list — KAFF-124 rule 2");

        (await SearchAsync(nonce, status: "archived")).Select(client => client.Code).Should().Contain(
            code,
            "spec.md §2 requires full history and §3 attaches a reopened opportunity to the SAME "
            + "client — both are impossible if the row can disappear");

        Client stored = await ReadAsync(id);
        stored.IsActive.Should().BeFalse();
        stored.Name.Should().Be($"شركة {nonce} للمقاولات", "archiving touches IsActive and nothing else");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.EntityType.Should().Be(nameof(Client));
        record.ActorUserId.Should().Be(_marketing, "the trail names who took them off the list");
        record.ChangedProperties.Should().Contain(nameof(Client.IsActive));
        record.GrantPath.Should().BeNull("ClientManage is company-wide — no project, no path to name");

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Client.IsActive)).GetBoolean().Should().BeTrue();
        after.RootElement.GetProperty(nameof(Client.IsActive)).GetBoolean().Should().BeFalse();
    }

    // ---- AC-123-B · the archived client still surfaces in the duplicate check ------------------

    /// <summary>
    /// The criterion tests the wording and the archived flag, not a status code — and that is the
    /// point of it.
    /// </summary>
    /// <remarks>
    /// It used to read <i>"then it is refused as a duplicate"</i>. Karim reversed that on 2026-08-21
    /// and the unique index was dropped, so spec.md §3's <i>"never create a duplicate client"</i> is
    /// no longer held by the database across time — <b>it is held by an operator reading a
    /// warning</b>. KAFF-123 rule 2b: a real reduction, made knowingly. Which makes the warning
    /// naming the archived client the whole of the remaining control.
    /// </remarks>
    [Fact]
    public async Task An_archived_client_still_warns_and_still_says_it_is_archived()
    {
        string phone = UniqueNames.Phone().Entered;
        (Guid id, string code) = await RegisterAsync("العميل المؤرشف", phone: phone);

        await ArchiveAsync(id, _marketing, Role.MarketingSales, Department.Marketing);

        IReadOnlyList<PhoneMatch> matches = await CheckAsync(phone);

        PhoneMatch match = matches.Should().ContainSingle().Subject;
        match.Code.Should().Be(code);
        match.Name.Should().Be("العميل المؤرشف", "a warning that does not say whose number it is was not what was ruled");
        match.IsArchived.Should().BeTrue("the operator has to know the match is one they archived");

        (await RegisterResponseAsync("عميل جديد بنفس الرقم", phone: phone, acknowledged: true))
            .StatusCode.Should().Be(
                HttpStatusCode.Created,
                "the save is not blocked — D-049 ruling 8, and archiving does not change that");
    }

    // ---- AC-123-C · archiving twice is refused --------------------------------------------------

    [Fact]
    public async Task Archiving_a_client_twice_is_refused()
    {
        (Guid id, _) = await RegisterAsync("عميل يؤرشف مرتين");

        (await ArchiveAsync(id, _marketing, Role.MarketingSales, Department.Marketing))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage again = await ArchiveAsync(id, _marketing, Role.MarketingSales, Department.Marketing);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using JsonDocument problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be(
            "errors.master.already_archived",
            "the refusal is Client.Archive's — a second copy of the rule in the handler is the copy "
            + "that drifts from the entity every other caller goes through");
    }

    [Fact]
    public async Task Archiving_a_client_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await ArchiveAsync(
            Guid.NewGuid(), _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.client_not_found");
    }

    // ---- AC-123-D · no delete exists -------------------------------------------------------------

    /// <summary>
    /// Proved against the routes the host actually mapped, never against source text.
    /// </summary>
    /// <remarks>
    /// <b>An absence proved by grepping for the word "delete" is proved about the word.</b> That is
    /// <c>V-32-A</c>'s shape, and `TC-1-183` was rewritten away from it on 2026-09-04. This enumerates
    /// every endpoint this assembly mapped and asserts none of them answers <c>DELETE</c> — so a
    /// delete route added under any name, in any feature folder, fails here. spec.md §2 and §3 make
    /// the client row permanent; KAFF-123 rule 1.
    /// </remarks>
    [Fact]
    public void No_endpoint_in_the_application_deletes_anything()
    {
        Assembly shipped = typeof(PermissionRequirement).Assembly;

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

                HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

                if (methods?.HttpMethods.Contains(HttpMethods.Delete, StringComparer.OrdinalIgnoreCase) == true)
                {
                    deleteRoutes.Add(route.RoutePattern.RawText ?? "(no pattern)");
                }
            }
        }

        deleteRoutes.Should().BeEmpty(
            "a client is archived and never deleted (spec.md §2, §3), postings are append-only "
            + "(CLAUDE.md), and this asserts it against what the host mapped rather than against the "
            + "word \"delete\" appearing in a file");
    }

    // ---- AC-123-E · nobody outside Marketing and the Owner may archive --------------------------

    [Fact]
    public async Task Only_marketing_and_the_owner_may_archive_a_client()
    {
        (Guid id, _) = await RegisterAsync("عميل محمي من الأرشفة");

        (await ArchiveAsync(id, _finance, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await ArchiveAsync(id, _hr, Role.Hr, Department.Hr))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await ArchiveAsync(id, _portalClient, Role.Client, null, actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "spec.md §12 — a portal user archiving another client is not a smaller breach for "
                + "being destructive rather than nosy");

        (await ReadAsync(id)).IsActive.Should().BeTrue("not one of the three changed anything");

        (await ArchiveAsync(id, _owner, Role.Owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "the Owner holds every company-wide row");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<(Guid Id, string Code)> RegisterAsync(string name, string? phone = null)
    {
        HttpResponseMessage response = await RegisterResponseAsync(name, phone, acknowledged: false);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), body.RootElement.GetProperty("code").GetString()!);
    }

    private async Task<HttpResponseMessage> RegisterResponseAsync(string name, string? phone, bool acknowledged)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/clients", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name,
                phone = phone ?? UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = acknowledged,
            }),
        };

        await StampAsync(request, _marketing, Role.MarketingSales, Department.Marketing, null);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> ArchiveAsync(
        Guid clientId,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/clients/{clientId}/archive", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<IReadOnlyList<ClientSummary>> SearchAsync(string search, string status = "active")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"/api/clients?status={status}&search={Uri.EscapeDataString(search)}", UriKind.Relative));

        await StampAsync(request, _marketing, Role.MarketingSales, Department.Marketing, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return
        [
            .. body.RootElement.GetProperty("clients").EnumerateArray().Select(element => new ClientSummary(
                element.GetProperty("id").GetGuid(),
                element.GetProperty("code").GetString()!,
                element.GetProperty("name").GetString()!,
                element.GetProperty("phone").GetString()!,
                Enum.Parse<ClientKind>(element.GetProperty("kind").GetString()!),
                element.GetProperty("isActive").GetBoolean())),
        ];
    }

    private async Task<IReadOnlyList<PhoneMatch>> CheckAsync(string phone)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/clients/phone-check", UriKind.Relative))
        {
            Content = JsonContent.Create(new { phone }),
        };

        await StampAsync(request, _marketing, Role.MarketingSales, Department.Marketing, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return
        [
            .. body.RootElement.GetProperty("matches").EnumerateArray().Select(element => new PhoneMatch(
                element.GetProperty("id").GetGuid(),
                element.GetProperty("code").GetString()!,
                element.GetProperty("name").GetString()!,
                element.GetProperty("isArchived").GetBoolean())),
        ];
    }

    private async Task StampAsync(
        HttpRequestMessage request,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        if (actorClientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, actorClientId.Value.ToString());
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

    private async Task<Client> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Clients.SingleAsync(client => client.Id == id, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("ARV-C1"),
            "عميل بوابة العملاء",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        User owner = MakeUser("arv-owner", Role.Owner);
        User marketing = MakeUser("arv-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("arv-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("arv-hr", Role.Hr, Department.Hr);
        User portal = MakeUser("arv-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(owner, marketing, finance, hr, portal);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _owner = owner.Id;
        _marketing = marketing.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(string userName, Role role, Department? department = null, Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, null, clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
