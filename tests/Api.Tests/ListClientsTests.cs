using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Features.Clients.ListClients;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-124 — <c>GET /api/clients</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every search here is scoped to this class's own clients.</b> The database is shared by every
/// class in the collection, so an assertion like "the search returns exactly two" is a claim about
/// whatever else happens to be in the table. Each test searches a nonce that only its own fixtures
/// carry, and asserts on the codes it created — which is also what makes <c>AC-124-B</c>'s "both come
/// back, neither preferred" checkable at all.
/// </para>
/// <para>
/// No fixture seeds a literal <c>C-1xxxx</c> code: <c>client_code_seq</c> starts at 10001 and a
/// hand-written collision presents as an unexplained 500 in an unrelated suite (decisions.md
/// D-107 §1). Clients here are registered through <c>POST /api/clients</c>, so their codes are the
/// generator's.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ListClientsTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _marketing;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _hr;
    private Guid _siteEngineer;
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public ListClientsTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-124-A · a phone in any format finds the client ------------------------------------

    [Fact]
    public async Task A_phone_typed_in_any_of_three_formats_finds_the_same_client()
    {
        string national = UniqueNames.Phone().Entered;
        string bare = national[1..];

        string code = await RegisterAsync("شركة البحث بالهاتف", phone: national);

        foreach (string typed in new[] { national, "+20 " + bare, "0020 " + bare })
        {
            IReadOnlyList<ClientSummary> found = await SearchAsync(typed);

            found.Select(client => client.Code).Should().Contain(
                code,
                "the search runs on the normalised phone, so +20 10…, 0020 10… and 010… are one "
                + "number — comparing the typed text would find only the format used at registration");
        }
    }

    // ---- AC-124-B · two clients with one number both come back --------------------------------

    [Fact]
    public async Task Two_clients_sharing_a_number_are_both_returned_and_neither_is_preferred()
    {
        string shared = UniqueNames.Phone().Entered;

        string first = await RegisterAsync("شركة النيل", phone: shared);
        string second = await RegisterAsync("مدير شركة النيل", phone: shared, acknowledged: true);

        IReadOnlyList<ClientSummary> found = await SearchAsync(shared);

        found.Select(client => client.Code).Should().BeEquivalentTo(
            [first, second],
            "duplicates are permitted (D-049 ruling 8), so a phone search is a list and never \"the "
            + "client with this number\" — KAFF-124 rule 1b");
    }

    // ---- AC-124-C · the generated code finds the client ---------------------------------------

    /// <summary>
    /// Both cases, and the lower-case one holds for a reason worth naming.
    /// </summary>
    /// <remarks>
    /// <c>c-10001</c> finds <c>C-10001</c> because <c>Client.Create</c> upper-cases what it stores and
    /// the handler upper-cases the term to meet it. That is the entity's normalisation doing the work,
    /// not the query's — so if the entity ever stops upper-casing, this is the test that says so.
    /// </remarks>
    [Fact]
    public async Task A_generated_code_finds_its_client_in_either_case()
    {
        string code = await RegisterAsync("شركة البحث بالكود");

        (await SearchAsync(code)).Select(client => client.Code).Should().ContainSingle()
            .Which.Should().Be(code);

        (await SearchAsync(code.ToLowerInvariant())).Select(client => client.Code).Should().ContainSingle()
            .Which.Should().Be(code, "the stored code is upper-cased by Client.Create, and the term is met there");
    }

    // ---- AC-124-D · partial name search works in Arabic ---------------------------------------

    [Fact]
    public async Task A_substring_of_an_arabic_name_finds_the_client()
    {
        string nonce = UniqueNames.Code("BHT");
        string code = await RegisterAsync($"مؤسسة {nonce} للتشطيبات المتكاملة");

        (await SearchAsync($"{nonce} للتشطيبات")).Select(client => client.Code).Should().ContainSingle()
            .Which.Should().Be(code, "Marketing types part of a name, not the whole of one");
    }

    /// <summary>
    /// A search box must not double as a query language.
    /// </summary>
    /// <remarks>
    /// <c>%</c> and <c>_</c> are <c>ILIKE</c> wildcards. Unescaped, searching <c>%</c> returns every
    /// client in Kaff and searching <c>_</c> matches any single character — the operator gets results
    /// they did not ask for and has no way to tell.
    /// </remarks>
    [Fact]
    public async Task Wildcards_typed_into_the_search_box_are_matched_literally()
    {
        string nonce = UniqueNames.Code("WLD");
        string withSign = await RegisterAsync($"شركة {nonce} 100% للتنفيذ");
        string plain = await RegisterAsync($"شركة {nonce} بدون علامة");

        (await SearchAsync($"{nonce} 100%")).Select(client => client.Code).Should().BeEquivalentTo(
            [withSign],
            "the per cent sign is a character the operator typed, not a wildcard");

        IReadOnlyList<ClientSummary> bareWildcard = await SearchAsync("%");

        bareWildcard.Select(client => client.Code).Should().NotContain(
            plain,
            "a bare wildcard must not return the whole client master — unescaped it would match every "
            + "name in Kaff");

        bareWildcard.Select(client => client.Code).Should().Contain(
            withSign,
            "and it must still find the clients whose names really do contain a per cent sign, which "
            + "is what makes the escaping literal rather than merely restrictive");
    }

    // ---- AC-124-E · archived clients are hidden by default and findable on request -------------

    [Fact]
    public async Task An_archived_client_is_hidden_by_default_and_returned_when_asked_for()
    {
        string nonce = UniqueNames.Code("ARC");

        string active = await RegisterAsync($"عميل {nonce} نشط");
        string archivedCode = await RegisterAsync($"عميل {nonce} مؤرشف");

        await ArchiveAsync(archivedCode);

        (await SearchAsync(nonce)).Select(client => client.Code).Should().BeEquivalentTo(
            [active],
            "the default list excludes archived clients — KAFF-124 rule 2");

        IReadOnlyList<ClientSummary> withArchived = await SearchAsync(nonce, status: "all");

        withArchived.Select(client => client.Code).Should().BeEquivalentTo(
            [active, archivedCode],
            "spec.md §3 attaches a reopened opportunity to the ORIGINAL client, so an archived one "
            + "that could not be found again would force the duplicate this feature exists to prevent");

        withArchived.Single(client => client.Code == archivedCode)
            .IsActive.Should().BeFalse("the row says which of the two it is");

        (await SearchAsync(nonce, status: "archived")).Select(client => client.Code).Should().BeEquivalentTo(
            [archivedCode],
            "S-011 draws three chips — All, Active, Archived — and the third is archived ALONE, "
            + "which a boolean includeArchived could never express (decisions.md D-111 §3)");
    }

    /// <summary>
    /// An unknown filter is refused rather than quietly treated as the default.
    /// </summary>
    /// <remarks>
    /// A mistyped <c>?status=archvied</c> defaulted to "active" returns a list of active clients to
    /// somebody who asked for archived ones — <b>indistinguishable from an empty archive</b>, and the
    /// operator concludes there is nothing there. Absent is a default; wrong is a mistake, and the two
    /// must not produce the same list.
    /// </remarks>
    [Fact]
    public async Task A_filter_this_list_does_not_know_is_refused_and_not_defaulted()
    {
        HttpResponseMessage response = await SendAsync(
            _marketing, Role.MarketingSales, Department.Marketing, null, "archvied");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be(
            "errors.master.client_list_filter_unknown");
    }

    /// <summary>The three chips of <c>ux/slice-1-flows.md</c> -&gt; <c>S-011</c>, and nothing else.</summary>
    [Fact]
    public void The_filter_has_exactly_the_three_states_the_screen_draws()
    {
        Enum.GetNames<ClientListFilter>().Should().BeEquivalentTo(
            ["Active", "Archived", "All"],
            "a fourth state here is a chip nobody drew, and a missing one is a chip that cannot work");
    }

    // ---- AC-124-F · a portal client cannot list clients ---------------------------------------

    [Fact]
    public async Task A_portal_client_is_refused_and_no_client_name_reaches_the_body()
    {
        string nonce = UniqueNames.Code("PRT");
        await RegisterAsync($"شركة {nonce} السرية");

        foreach ((string? search, string status) in new (string?, string)[]
                 { (null, "active"), (nonce, "active"), (nonce, "all") })
        {
            HttpResponseMessage response = await SendAsync(
                _portalClient, Role.Client, null, search, status, actorClientId: _portalClientCompany);

            response.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "this endpoint returns EVERY client in Kaff, and spec.md §12 is absolute — "
                + "KAFF-124 rule 4 says \"under any circumstances\"");

            (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain(
                nonce,
                "a refusal that still names a client has refused nothing that matters");
        }
    }

    // ---- AC-124-G · no money in the payload ---------------------------------------------------

    /// <summary>
    /// The four client-shaped payloads of this slice, each pinned to an exact member set.
    /// </summary>
    /// <remarks>
    /// <b>A whitelist, not a search for suspect words</b> — decisions.md D-106, where a seven-word
    /// blocklist let a <c>decimal RetainedAmount</c> onto the wire against a green suite. KAFF-124
    /// rule 5 is specifically about what a list must not <i>join</i>: the entity has no balance to
    /// project, so the risk is a later hand adding "total billed" because a list screen is where
    /// somebody will want it. Any added member fails this, whatever it is called.
    /// </remarks>
    [Fact]
    public void No_client_payload_in_this_slice_carries_money()
    {
        string[] expected = ["Id", "Code", "Name", "Phone", "Kind", "IsActive"];

        typeof(ClientSummary).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(expected);

        typeof(Kaff.Api.Features.Clients.CreateClient.Response).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(expected);

        typeof(Kaff.Api.Features.Clients.EditClient.Response).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(expected);

        typeof(Kaff.Api.Features.Clients.PhoneMatch).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Id", "Code", "Name", "IsArchived"]);

        typeof(Response).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Clients"], "the wrapper carries the list and nothing else");
    }

    /// <summary>KAFF-121 rule 8 and spec.md §12 — internal notes are in no payload of this slice.</summary>
    [Fact]
    public async Task Internal_notes_never_reach_the_list()
    {
        string nonce = UniqueNames.Code("NTE");
        string code = await RegisterAsync($"شركة {nonce} لها ملاحظات");

        await SetNotesAsync(code, "تأخر في السداد مرتين");

        HttpResponseMessage response = await SendAsync(
            _marketing, Role.MarketingSales, Department.Marketing, nonce, "active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain(
            "تأخر في السداد مرتين",
            "spec.md §12 — the client MUST NEVER see internal notes, and this is the payload with "
            + "the widest reach in the slice");
    }

    // ---- AC-124-H · an empty search says so (the API half) -------------------------------------

    /// <summary>
    /// The half of <c>AC-124-H</c> that exists without a screen.
    /// </summary>
    /// <remarks>
    /// The criterion is written about rendering — <i>"then <c>clients.list.empty</c> is displayed"</i>
    /// — and there is no client list screen, so the render half is Frontend's and is <b>not</b>
    /// discharged here. What this pins is the contract the empty state is rendered from: a
    /// <c>200</c> carrying an empty array, never a <c>404</c> and never a null the caller has to guard.
    /// A screen cannot show an honest empty state against a payload that is sometimes absent.
    /// </remarks>
    [Fact]
    public async Task A_search_matching_nothing_is_an_empty_list_and_not_an_error()
    {
        HttpResponseMessage response = await SendAsync(
            _marketing, Role.MarketingSales, Department.Marketing, UniqueNames.Code("NONE"), "active");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "nothing found is not something gone wrong");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.TryGetProperty("clients", out JsonElement clients).Should().BeTrue();
        clients.ValueKind.Should().Be(JsonValueKind.Array, "never null — a screen cannot render an empty state from an absent field");
        clients.GetArrayLength().Should().Be(0);
    }

    // ---- rule 3 · nobody outside Marketing and the Owner may list ------------------------------

    [Fact]
    public async Task Only_marketing_and_the_owner_may_list_clients()
    {
        (Guid Actor, Role Role, Department? Department, OperationsSubDepartment? Sub)[] refused =
        [
            (_finance, Role.Finance, Department.Finance, null),
            (_technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical),
            (_hr, Role.Hr, Department.Hr, null),
            (_siteEngineer, Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical),
        ];

        foreach ((Guid actor, Role role, Department? department, OperationsSubDepartment? sub) in refused)
        {
            (await SendAsync(actor, role, department, null, "active", actorSubDepartment: sub))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    "{0} does not hold ClientManage — spec.md §2, Client is owned by Marketing",
                    role);
        }

        (await SendAsync(_owner, Role.Owner, null, null, "active"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>Registers a client through the real endpoint and returns its generated code.</summary>
    private async Task<string> RegisterAsync(string name, string? phone = null, bool acknowledged = false)
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

        await StampAsync(request, _marketing, Role.MarketingSales, Department.Marketing, null, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("code").GetString()!;
    }

    private async Task<IReadOnlyList<ClientSummary>> SearchAsync(string? search, string status = "active")
    {
        HttpResponseMessage response = await SendAsync(
            _marketing, Role.MarketingSales, Department.Marketing, search, status);

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

    private async Task<HttpResponseMessage> SendAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        string? search,
        string status,
        OperationsSubDepartment? actorSubDepartment = null,
        Guid? actorClientId = null)
    {
        string route = "/api/clients?status=" + status;

        if (search is not null)
        {
            route += "&search=" + Uri.EscapeDataString(search);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorSubDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(
        HttpRequestMessage request,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        OperationsSubDepartment? actorSubDepartment,
        Guid? actorClientId)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        if (actorSubDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.SubDepartmentHeader, actorSubDepartment.Value.ToString());
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

    private async Task ArchiveAsync(string code)
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = await context.Clients.SingleAsync(candidate => candidate.Code == code, Ct);

        client.Archive().IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(Ct);
    }

    private async Task SetNotesAsync(string code, string notes)
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = await context.Clients.SingleAsync(candidate => candidate.Code == code, Ct);

        client.SetContactDetails(client.AlternatePhone, client.Email, client.Address, notes);

        await context.SaveChangesAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("LST-C1"),
            "عميل بوابة العملاء",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        User owner = MakeUser("lst-owner", Role.Owner);
        User marketing = MakeUser("lst-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("lst-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser(
            "lst-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User hr = MakeUser("lst-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "lst-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User portal = MakeUser("lst-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(owner, marketing, finance, technicalOffice, hr, siteEngineer, portal);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _owner = owner.Id;
        _marketing = marketing.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment,
            clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
