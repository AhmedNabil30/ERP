using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Features.Clients;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-121 — <c>PUT /api/clients/{clientId}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every test here goes through HTTP. The mutation surface itself is pinned in
/// <c>Domain.Tests/ClientEditingTests.cs</c>; what can only be observed at this level is the
/// duplicate warning, the permission gate, and what the audit trail holds after somebody corrected a
/// record.
/// </para>
/// <para>
/// <b>No fixture seeds a literal <c>C-1xxxx</c> code</b>, for the reason <c>CreateClientTests</c>
/// states: <c>client_code_seq</c> starts at 10001 and shares one database with every other class in
/// the collection, so a hand-written <c>C-10005</c> collides the moment the sequence reaches it and
/// presents as an unexplained 500 in an unrelated suite (decisions.md D-107 §1).
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class EditClientTests : IAsyncLifetime
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

    public EditClientTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-121-A · a name can be corrected at all --------------------------------------------

    /// <summary>
    /// The headline behaviour, and until 2026-09-04 there was no path to it at all.
    /// </summary>
    /// <remarks>
    /// KAFF-121 finding F-09: <c>Client</c> had no name setter, so <b>a mistyped client name was
    /// permanent</b> on a record spec.md §2 requires to hold "full history". The before-state is
    /// asserted as hard as the after-state, because the whole reason §2 wants the history is to
    /// answer questions about what the file said at the time.
    /// </remarks>
    [Fact]
    public async Task A_mistyped_name_is_corrected_and_the_trail_keeps_both_spellings()
    {
        Guid id = await RegisterAsync("شركة النور للمقاولت");

        HttpResponseMessage response = await EditAsync(
            id, _marketing, Role.MarketingSales, Department.Marketing, Body("شركة النور للمقاولات"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Client edited = await ReadClientAsync(id);
        edited.Name.Should().Be("شركة النور للمقاولات");

        AuditRecord record = await ModificationOfAsync(id);

        record.EntityType.Should().Be(nameof(Client));
        record.ActorUserId.Should().Be(_marketing);
        record.ActorRole.Should().Be(Role.MarketingSales);
        record.ChangedProperties.Should().Contain(nameof(Client.Name));

        record.GrantPath.Should().BeNull(
            "ClientManage is company-wide: no project, no access policy, no path to name");

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Client.Name)).GetString().Should().Be(
            "شركة النور للمقاولت",
            "spec.md §2 wants the full history, and a correction that erases what it corrected is not one");

        after.RootElement.GetProperty(nameof(Client.Name)).GetString().Should().Be("شركة النور للمقاولات");
    }

    // ---- AC-121-B · a correction is recorded with its before-state ----------------------------

    [Fact]
    public async Task An_address_change_carries_both_values_into_the_trail()
    {
        Guid id = await RegisterAsync("شركة العنوان", address: "المعادي، القاهرة");

        (await EditAsync(
                id,
                _marketing,
                Role.MarketingSales,
                Department.Marketing,
                Body("شركة العنوان", address: "التجمع الخامس، القاهرة الجديدة")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        AuditRecord record = await ModificationOfAsync(id);

        record.ChangedProperties.Should().Contain(nameof(Client.Address));

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(Client.Address)).GetString().Should().Be("المعادي، القاهرة");
        after.RootElement.GetProperty(nameof(Client.Address)).GetString().Should().Be("التجمع الخامس، القاهرة الجديدة");
    }

    // ---- AC-121-C · changing the phone re-runs the duplicate check, and warns ------------------

    [Fact]
    public async Task Editing_a_phone_onto_another_clients_number_warns_naming_them_and_then_saves()
    {
        string held = UniqueNames.Phone().Entered;

        Guid b = await RegisterAsync("العميل صاحب الرقم", phone: held);
        Guid a = await RegisterAsync("العميل الذي يُعدَّل");

        IReadOnlyList<PhoneMatch> matches = await CheckAsync(held);

        matches.Should().ContainSingle("only B holds that number so far")
            .Which.Name.Should().Be(
                "العميل صاحب الرقم",
                "a warning that does not say whose number it is was not what was ruled");

        HttpResponseMessage asked = await EditAsync(
            a, _marketing, Role.MarketingSales, Department.Marketing, Body("العميل الذي يُعدَّل", phone: held));

        asked.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "spec.md §2, amended: the repeated number asks whether to proceed. It is not a refusal — "
            + "the same request with the flag succeeds");

        (await MessageKeyAsync(asked)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        (await ReadClientAsync(a)).PhoneEntered.Should().NotBe(held, "the asked-about edit did not save");

        HttpResponseMessage proceeded = await EditAsync(
            a,
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("العميل الذي يُعدَّل", phone: held, acknowledged: true));

        proceeded.StatusCode.Should().Be(HttpStatusCode.OK, "it does not block the save");

        (await ReadClientAsync(a)).PhoneEntered.Should().Be(held);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord acknowledgement = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged && record.EntityId == b,
            Ct);

        acknowledgement.EntityType.Should().Be(nameof(Client));
        acknowledgement.ActorUserId.Should().Be(_marketing, "a human made the call and the trail names them");

        Guid editCorrelation = (await ModificationOfAsync(a)).CorrelationId;

        acknowledgement.CorrelationId.Should().Be(
            editCorrelation,
            "one request, one save, one correlation id — the acknowledgement and the edit it "
            + "permitted are joinable without a text column");
    }

    // ---- AC-121-D · the check runs on the normalised number -----------------------------------

    [Fact]
    public async Task A_phone_edited_in_another_format_still_warns()
    {
        string national = UniqueNames.Phone().Entered;

        await RegisterAsync("العميل صاحب الرقم", phone: national);
        Guid a = await RegisterAsync("العميل الذي يُعدَّل");

        string international = "+20 " + national[1..];

        (await EditAsync(
                a,
                _marketing,
                Role.MarketingSales,
                Department.Marketing,
                Body("العميل الذي يُعدَّل", phone: international)))
            .StatusCode.Should().Be(
                HttpStatusCode.Conflict,
                "the match runs on the normalised phone — a format difference must not slip past the "
                + "only control that is left now the unique index is gone");
    }

    /// <summary>
    /// <b>The client being edited is never its own duplicate.</b>
    /// </summary>
    /// <remarks>
    /// This is the <c>excluding</c> parameter decisions.md D-107 §2 specified and KAFF-119
    /// deliberately did not build. Without it every edit that leaves the phone alone matches the row
    /// being saved, so correcting an address would demand an acknowledgement — and acknowledging it
    /// would write a <c>DuplicatePhoneAcknowledged</c> row pointing the client at itself, permanently,
    /// into an append-only table.
    /// </remarks>
    [Fact]
    public async Task A_client_is_not_a_duplicate_of_itself()
    {
        string phone = UniqueNames.Phone().Entered;

        Guid id = await RegisterAsync("عميل يعدل عنوانه", phone: phone);

        (await EditAsync(
                id,
                _marketing,
                Role.MarketingSales,
                Department.Marketing,
                Body("عميل يعدل عنوانه", phone: phone, address: "عنوان جديد")))
            .StatusCode.Should().Be(
                HttpStatusCode.OK,
                "the client's own row is not a match against itself, so an edit that keeps the phone "
                + "never asks the question");

        await using KaffDbContext reader = _database.CreateBareContext();

        bool recorded = await reader.AuditRecords.AnyAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged && record.EntityId == id,
            Ct);

        recorded.Should().BeFalse(
            "a client acknowledged as a duplicate of itself is a row that can never be deleted");
    }

    // ---- AC-121-E · the code cannot be edited --------------------------------------------------

    /// <summary>
    /// Settled structurally, and it has to be settled twice.
    /// </summary>
    /// <remarks>
    /// <c>CreateClient</c>'s equivalent test proves a code cannot be <i>supplied</i>; this one proves
    /// it cannot be <i>changed</i>, which is a claim about a different path and a different criterion.
    /// The request type carries no <c>Code</c> member, so a code in the body binds to nothing — which
    /// is stronger than any behaviour, because it cannot be weakened without deleting this test
    /// (decisions.md D-107 §4).
    /// </remarks>
    [Fact]
    public async Task A_code_in_the_edit_body_binds_to_nothing_and_the_stored_code_never_moves()
    {
        typeof(Kaff.Api.Features.Clients.EditClient.Request)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(
                "Code",
                "spec.md §2's amendment forbids later editing of a code, and the absence of the field "
                + "is what makes it unbreakable");

        Guid id = await RegisterAsync("عميل يحاول تغيير كوده");
        string original = (await ReadClientAsync(id)).Code;

        HttpResponseMessage response = await EditAsync(
            id,
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            new
            {
                code = "C-99999",
                name = "عميل يحاول تغيير كوده",
                phone = UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = false,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadClientAsync(id)).Code.Should().Be(original, "by any route through the API");
    }

    /// <summary>KAFF-121 rule 9 — editing does not archive, and archiving is not an edit.</summary>
    [Fact]
    public async Task An_edit_cannot_archive_a_client()
    {
        typeof(Kaff.Api.Features.Clients.EditClient.Request)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(
                "IsActive",
                "archiving has its own meaning in the trail and its own story, KAFF-123");

        Guid id = await RegisterAsync("عميل نشط");

        HttpResponseMessage response = await EditAsync(
            id,
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            new
            {
                isActive = false,
                name = "عميل نشط",
                phone = UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = false,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadClientAsync(id)).IsActive.Should().BeTrue();
    }

    // ---- AC-121-F · kind changes cannot smuggle a tax registration past §6.7 -------------------

    [Fact]
    public async Task A_corporate_client_cannot_become_an_individual_while_carrying_a_registration_number()
    {
        Guid id = await RegisterAsync("شركة لها رقم ضريبي", taxRegistrationNumber: "123-456-789");

        HttpResponseMessage response = await EditAsync(
            id,
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("شركة لها رقم ضريبي", kind: ClientKind.Individual, taxRegistrationNumber: "123-456-789"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await MessageKeyAsync(response)).Should().Be(
            "errors.master.individual_does_not_withhold",
            "spec.md §6.7 — an individual with a registration number is making the claim the "
            + "withholding rate used to, by another field");

        Client unchanged = await ReadClientAsync(id);
        unchanged.Kind.Should().Be(ClientKind.Corporate, "a refused edit changes neither member of the pair");
        unchanged.TaxRegistrationNumber.Should().Be("123-456-789");
    }

    [Fact]
    public async Task The_same_client_becomes_an_individual_once_the_number_goes_with_it()
    {
        Guid id = await RegisterAsync("شركة تتحول إلى فرد", taxRegistrationNumber: "123-456-789");

        (await EditAsync(
                id,
                _marketing,
                Role.MarketingSales,
                Department.Marketing,
                Body("أحمد محمود", kind: ClientKind.Individual, taxRegistrationNumber: null)))
            .StatusCode.Should().Be(
                HttpStatusCode.OK,
                "§6.7 constrains the end state, and an individual with no registration number is ordinary");

        Client edited = await ReadClientAsync(id);
        edited.Kind.Should().Be(ClientKind.Individual);
        edited.TaxRegistrationNumber.Should().BeNull();
    }

    // ---- AC-121-G · nobody outside Marketing and the Owner may edit ----------------------------

    [Fact]
    public async Task Only_marketing_and_the_owner_may_edit_a_client()
    {
        Guid id = await RegisterAsync("عميل محمي");

        (Guid Actor, Role Role, Department? Department, OperationsSubDepartment? Sub)[] refused =
        [
            (_finance, Role.Finance, Department.Finance, null),
            (_technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical),
            (_hr, Role.Hr, Department.Hr, null),
            (_siteEngineer, Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical),
        ];

        foreach ((Guid actor, Role role, Department? department, OperationsSubDepartment? sub) in refused)
        {
            (await EditAsync(id, actor, role, department, Body($"محاولة {role}"), actorSubDepartment: sub))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    "{0} does not hold ClientManage — spec.md §2, Client is owned by Marketing",
                    role);
        }

        (await EditAsync(
                id, _portalClient, Role.Client, null, Body("محاولة من البوابة"), actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "a portal user reaching this route could rewrite another client's file — spec.md §12");

        (await ReadClientAsync(id)).Name.Should().Be("عميل محمي", "not one of them changed anything");

        (await EditAsync(id, _owner, Role.Owner, null, Body("عميل عدله المالك")))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the Owner holds every company-wide row");
    }

    // ---- AC-121-H · internal notes stay internal ------------------------------------------------

    /// <summary>
    /// Notes are stored, and they are in no payload of this slice.
    /// </summary>
    /// <remarks>
    /// <b>A whitelist, not a search for the word "notes".</b> decisions.md D-106: a blocklist let a
    /// <c>decimal RetainedAmount</c> onto the wire against a green suite because the word was not one
    /// of the seven it knew. spec.md §12 is absolute — the client MUST NEVER see internal notes — so
    /// every client-shaped response in the slice is pinned to an exact member set, and a notes field
    /// added to any of them fails here whatever it is called.
    /// </remarks>
    [Fact]
    public async Task Notes_are_stored_and_appear_in_no_response_of_this_slice()
    {
        Guid id = await RegisterAsync("عميل له ملاحظات");

        HttpResponseMessage response = await EditAsync(
            id,
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("عميل له ملاحظات", notes: "تأخر في السداد مرتين"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadClientAsync(id)).Notes.Should().Be("تأخر في السداد مرتين", "the note is kept — it is internal, not forbidden");

        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain(
            "تأخر في السداد مرتين",
            "the edit that stored the note must not echo it back into a payload");

        typeof(Kaff.Api.Features.Clients.EditClient.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["Id", "Code", "Name", "Phone", "Kind", "IsActive"]);

        typeof(Kaff.Api.Features.Clients.CreateClient.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["Id", "Code", "Name", "Phone", "Kind", "IsActive"]);

        typeof(PhoneMatch).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Id", "Code", "Name", "IsArchived"]);
    }

    // ---- the route addresses a row by id, so it has to answer when the id names nobody ---------

    [Fact]
    public async Task Editing_a_client_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await EditAsync(
            Guid.NewGuid(), _marketing, Role.MarketingSales, Department.Marketing, Body("عميل غير موجود"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await MessageKeyAsync(response)).Should().Be(
            "errors.master.client_not_found",
            "a bare 404 is something the SPA can only render as \"something went wrong\"");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(
        string name,
        string? phone = null,
        ClientKind kind = ClientKind.Corporate,
        bool acknowledged = false,
        string? taxRegistrationNumber = null,
        string? address = null,
        string? notes = null) => new
        {
            name,
            phone = phone ?? UniqueNames.Phone().Entered,
            kind = kind.ToString(),
            alternatePhone = (string?)null,
            email = (string?)null,
            address,
            notes,
            taxRegistrationNumber,
            acknowledgedDuplicatePhone = acknowledged,
        };

    /// <summary>Registers a client through the real endpoint and returns its id.</summary>
    private async Task<Guid> RegisterAsync(
        string name,
        string? phone = null,
        string? taxRegistrationNumber = null,
        string? address = null)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            "/api/clients",
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body(name, phone, taxRegistrationNumber: taxRegistrationNumber, address: address),
            null,
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> EditAsync(
        Guid clientId,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        object body,
        OperationsSubDepartment? actorSubDepartment = null,
        Guid? actorClientId = null)
        => SendAsync(
            HttpMethod.Put,
            $"/api/clients/{clientId}",
            actorId,
            actorRole,
            actorDepartment,
            body,
            actorSubDepartment,
            actorClientId);

    private async Task<IReadOnlyList<PhoneMatch>> CheckAsync(string phone)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            "/api/clients/phone-check",
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            new { phone },
            null,
            null);

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

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        object body,
        OperationsSubDepartment? actorSubDepartment,
        Guid? actorClientId)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

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

        return await _client.SendAsync(request, Ct);
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

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task<Client> ReadClientAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Clients.SingleAsync(client => client.Id == id, Ct);
    }

    private async Task<AuditRecord> ModificationOfAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.AuditRecords
            .Where(record => record.EntityId == id && record.Action == AuditAction.Modified)
            .OrderByDescending(record => record.OccurredAt)
            .FirstAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("EDT-C1"),
            "عميل بوابة العملاء",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        User owner = MakeUser("edt-owner", Role.Owner);
        User marketing = MakeUser("edt-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("edt-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser(
            "edt-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User hr = MakeUser("edt-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "edt-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User portal = MakeUser("edt-portal", Role.Client, clientId: company.Id);

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
