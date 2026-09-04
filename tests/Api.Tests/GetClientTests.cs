using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/clients/{clientId}</c> — the read S-014 loads its form from. KAFF-126, decisions.md D-113.
/// </summary>
/// <remarks>
/// Built because writing the screen found that nothing could load the record `PUT /api/clients/{id}`
/// edits: KAFF-124 shipped a list whose rows carry six fields, KAFF-121 shipped an edit taking nine,
/// and S-014 is reachable by URL so router state cannot stand in.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class GetClientTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _marketing;
    private Guid _finance;
    private Guid _hr;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _headOfDesign;
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public GetClientTests(PostgresDatabase database) => _database = database;

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
    public async Task The_whole_editable_file_comes_back_including_the_fields_the_list_row_omits()
    {
        Guid id = await RegisterAsync("شركة النيل للتطوير");

        await SetDetailsAsync(id, notes: "تأخر في السداد مرتين", address: "التجمع الخامس");

        HttpResponseMessage response = await GetAsync(id, _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("name").GetString().Should().Be("شركة النيل للتطوير");
        body.RootElement.GetProperty("address").GetString().Should().Be("التجمع الخامس");
        body.RootElement.GetProperty("notes").GetString().Should().Be(
            "تأخر في السداد مرتين",
            "the edit form has to load the note it is going to save back — this is the one payload in "
            + "the slice that carries them, and it is gated ClientManage");
        body.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("code").GetString().Should().MatchRegex(@"^C-\d{5,}$");
    }

    /// <summary>An archived client is still readable by id — S-014 was asked for that client.</summary>
    [Fact]
    public async Task An_archived_client_is_still_readable_by_id()
    {
        Guid id = await RegisterAsync("عميل مؤرشف");

        await ArchiveAsync(id);

        HttpResponseMessage response = await GetAsync(id, _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse(
            "the list hides archived clients by default; a screen reached by id asked for this one, "
            + "and spec.md §3 attaches a reopened opportunity to the original");
    }

    /// <summary>
    /// The payload is pinned, because it is the widest client payload in the slice.
    /// </summary>
    /// <remarks>
    /// A whitelist, not a search for suspect words (decisions.md D-106). No balance, no credit limit,
    /// no withholding category — and the fields that are here are here because S-014 edits them.
    /// </remarks>
    [Fact]
    public void The_payload_carries_the_editable_file_and_no_money()
    {
        typeof(Kaff.Api.Features.Clients.GetClient.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [
                    "Id", "Code", "Name", "Phone", "Kind", "AlternatePhone", "Email", "Address",
                    "TaxRegistrationNumber", "Notes", "IsActive",
                ],
                "a money column or a withholding category added here fails, whatever it is named");
    }

    /// <summary>
    /// Every role that is not Marketing or the Owner is refused — all six of them, not the two that
    /// were easy to write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Widened 2026-09-05 to close `V-33-B` (MEDIUM), `qa/slice-1/verification-2026-09-05.md`.</b>
    /// This test asserted <b>2 of the 6 refused roles</b> — Finance and the portal client. The
    /// Verifier granted <c>ClientManage</c> to <c>Role.TechnicalOffice</c> and watched three of the
    /// six client endpoints redden while this one stayed green: <i>"the same defect is caught or
    /// missed depending on which endpoint the attacker uses"</i>, and this is the endpoint it is
    /// worst to miss on, because it is the only payload in the slice carrying internal notes.
    /// </para>
    /// <para>
    /// <b>All six, from the enum rather than from a hand-written list.</b> A literal list is a list
    /// that stays at six when a tenth role is added; deriving the refused set as *every role except
    /// the two granted* means a new role arrives here refused by default, which is the direction a
    /// permission system has to fail in. <c>Role.Subcontractor</c> is excluded because spec.md §9 says
    /// *"record only, no login"* — it cannot hold a session to be refused with, and
    /// <c>No_permission_is_granted_to_a_subcontractor</c> pins that catalogue-wide.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Nobody_outside_marketing_and_the_owner_may_read_a_client_file()
    {
        Guid id = await RegisterAsync("عميل محمي");

        await SetDetailsAsync(id, notes: "ملاحظة داخلية", address: null);

        foreach ((Guid actorId, Role role, Department? department, Guid? clientId) in RefusedActors())
        {
            HttpResponseMessage refused = await GetAsync(id, actorId, role, department, clientId);

            refused.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                $"{role} holds no ClientManage, and this is the one payload carrying internal notes — "
                + "spec.md §12 forbids a client ever seeing them, and on a read there is no audit "
                + "constraint to fail behind the gate (D-110 §2)");

            (await refused.Content.ReadAsStringAsync(Ct)).Should().NotContain(
                "ملاحظة داخلية",
                $"a refusal to {role} must not carry the note in its body either");
        }
    }

    /// <summary>
    /// Every role that must be refused, derived rather than listed. See the remarks above.
    /// </summary>
    private IEnumerable<(Guid ActorId, Role Role, Department? Department, Guid? ClientId)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance, null);
        yield return (_hr, Role.Hr, Department.Hr, null);
        yield return (_technicalOffice, Role.TechnicalOffice, Department.Operations, null);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations, null);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations, null);
        yield return (_portalClient, Role.Client, null, _portalClientCompany);
    }

    /// <summary>
    /// The list above is every signing-in role except the two that hold <c>ClientManage</c>.
    /// </summary>
    /// <remarks>
    /// Asserted rather than trusted, because the list is hand-written and the enum is not. A tenth
    /// role added to <c>Role</c> fails here, which is the notice that the loop above has stopped
    /// being exhaustive — <c>V-33-A</c> is what happens when a role exists and nothing mentions it.
    /// </remarks>
    [Fact]
    public void The_refused_list_is_every_role_that_can_sign_in_and_is_not_granted()
    {
        IEnumerable<Role> covered = RefusedActors().Select(actor => actor.Role);

        IEnumerable<Role> shouldBeRefused = Enum.GetValues<Role>()
            .Except([Role.Owner, Role.MarketingSales, Role.Subcontractor]);

        covered.Should().BeEquivalentTo(
            shouldBeRefused,
            "Owner and MarketingSales hold ClientManage; Subcontractor cannot sign in at all "
            + "(spec.md §9, 'record only, no login'). Everything else must appear in the loop");
    }

    [Fact]
    public async Task An_unknown_id_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await GetAsync(
            Guid.NewGuid(), _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.client_not_found");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<Guid> RegisterAsync(string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/clients", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name,
                phone = UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = false,
            }),
        };

        await StampAsync(request, _marketing, Role.MarketingSales, Department.Marketing, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> GetAsync(
        Guid clientId,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/clients/{clientId}", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
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

    private async Task SetDetailsAsync(Guid id, string? notes, string? address)
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = await context.Clients.SingleAsync(candidate => candidate.Id == id, Ct);

        client.SetContactDetails(null, null, address, notes);

        await context.SaveChangesAsync(Ct);
    }

    private async Task ArchiveAsync(Guid id)
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = await context.Clients.SingleAsync(candidate => candidate.Id == id, Ct);

        client.Archive().IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("GET-C1"),
            "عميل بوابة العملاء",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        User marketing = MakeUser("get-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("get-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("get-hr", Role.Hr, Department.Hr);
        User technicalOffice = MakeUser(
            "get-techoffice", Role.TechnicalOffice, Department.Operations,
            subDepartment: OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser(
            "get-siteeng", Role.SiteEngineer, Department.Operations,
            subDepartment: OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "get-headdesign", Role.HeadOfDesign, Department.Operations,
            subDepartment: OperationsSubDepartment.Technical);
        User portal = MakeUser("get-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(
            marketing, finance, hr, technicalOffice, siteEngineer, headOfDesign, portal);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _marketing = marketing.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _technicalOffice = technicalOffice.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        Guid? clientId = null,
        OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department,
            subDepartment, clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
