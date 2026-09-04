using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Features.Clients;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-119 — <c>POST /api/clients</c> and <c>POST /api/clients/phone-check</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every test here goes through HTTP. The domain guards are pinned in <c>Domain.Tests</c>; what can
/// only be observed at this level is the generator, the permission gate, and what the audit trail
/// actually contains after a human clicked through a warning.
/// </para>
/// <para>
/// <b>No fixture in this file seeds a literal <c>C-1xxxx</c> code.</b> The generator draws from
/// <c>client_code_seq</c>, which starts at 10001 and shares one database with every other class in
/// the collection — a hand-written <c>C-10005</c> would collide with the sequence the moment it
/// reached that value and would present as an unexplained 500 in an unrelated suite. Seeded clients
/// take <c>UniqueNames.Code</c> (decisions.md D-107 §1).
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class CreateClientTests : IAsyncLifetime
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

    public CreateClientTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-119-A · a client is registered, and the system names them -------------------------

    [Fact]
    public async Task Marketing_registers_a_client_and_the_trail_names_the_operator()
    {
        HttpResponseMessage response = await RegisterAsync(
            _marketing, Role.MarketingSales, Department.Marketing, Body("شركة النور للمقاولات"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid id = await IdOfAsync(response);
        Client created = await ReadClientAsync(id);

        created.IsActive.Should().BeTrue("Client.Create sets IsActive true");
        created.Kind.Should().Be(ClientKind.Corporate);

        created.Code.Should().MatchRegex(
            CodeShape,
            "spec.md §2's amendment fixes the format: sequential, of the form C-10001");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            candidate => candidate.EntityId == id && candidate.Action == AuditAction.Created,
            Ct);

        record.EntityType.Should().Be(nameof(Client));
        record.ActorUserId.Should().Be(_marketing);
        record.ActorRole.Should().Be(Role.MarketingSales);
        record.BeforeJson.Should().BeNull("nothing existed before a registration");

        record.GrantPath.Should().BeNull(
            "ClientManage is company-wide: no project, no access policy, no path to name");

        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        after.RootElement.GetProperty(nameof(Client.Code)).GetString().Should().Be(created.Code);
        after.RootElement.GetProperty(nameof(Client.Kind)).GetString().Should().Be(nameof(ClientKind.Corporate));
    }

    // ---- AC-119-B · the codes run in sequence and are never typed ----------------------------

    /// <summary>
    /// Format plus strict successor, inside one test.
    /// </summary>
    /// <remarks>
    /// The criterion is worded as literal values — "the last client carries <c>C-10001</c> … then it
    /// carries <c>C-10002</c>" — and that is not assertable against a database shared by every class
    /// in the collection, whose sequence is wherever previous tests left it. What the criterion
    /// actually asserts is that the second code is the first one's successor, and that is checkable
    /// only if both registrations happen here, back to back, with nothing between them
    /// (decisions.md D-107 §1).
    /// </remarks>
    [Fact]
    public async Task The_next_client_registered_takes_the_next_code_in_the_sequence()
    {
        Client first = await ReadClientAsync(await IdOfAsync(
            await RegisterAsync(_marketing, Role.MarketingSales, Department.Marketing, Body("عميل التسلسل الأول"))));

        Client second = await ReadClientAsync(await IdOfAsync(
            await RegisterAsync(_marketing, Role.MarketingSales, Department.Marketing, Body("عميل التسلسل الثاني"))));

        first.Code.Should().MatchRegex(CodeShape);
        second.Code.Should().MatchRegex(CodeShape);

        NumberIn(second.Code).Should().Be(
            NumberIn(first.Code) + 1,
            "the codes are sequential — C-10001 is followed by C-10002");
    }

    /// <summary>
    /// <c>AC-119-B</c>'s second half, settled structurally rather than by behaviour.
    /// </summary>
    /// <remarks>
    /// <i>"Ignored or refused"</i> is two behaviours one test cannot both assert. The request type
    /// carries no <c>Code</c> member at all, so a supplied code binds to nothing and no code path in
    /// the slice could store one — which is stronger than either behaviour, because it cannot be
    /// weakened without deleting this test. Both halves are asserted: the type, and a live request
    /// that really does send <c>code</c>. See decisions.md D-107 §4.
    /// </remarks>
    [Fact]
    public async Task A_code_in_the_request_body_binds_to_nothing_and_is_never_stored()
    {
        typeof(Kaff.Api.Features.Clients.CreateClient.Request)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(
                "Code",
                "the create request must not carry a code member — spec.md §2's amendment forbids "
                + "manual entry, and the absence of the field is what makes it unbreakable");

        HttpResponseMessage response = await RegisterAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            new
            {
                code = "C-99999",
                name = "عميل حاول كتابة الكود",
                phone = UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = false,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Client created = await ReadClientAsync(await IdOfAsync(response));

        created.Code.Should().NotBe("C-99999", "under no circumstances stored");
        created.Code.Should().MatchRegex(CodeShape);
    }

    // ---- AC-119-C · the same phone in three formats warns once, about the same client ---------

    [Fact]
    public async Task The_same_number_in_three_formats_warns_about_the_same_client()
    {
        const string Name = "شركة النور";
        string national = UniqueNames.Phone().Entered;
        string bare = national[1..];

        (await RegisterAsync(
                _marketing, Role.MarketingSales, Department.Marketing, Body(Name, phone: national)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (string typed in new[] { national, "+20 " + bare, "0020 " + bare })
        {
            IReadOnlyList<PhoneMatch> matches = await CheckAsync(
                _marketing, Role.MarketingSales, Department.Marketing, typed);

            matches.Should().ContainSingle(
                "the match runs on the normalised phone, so +20 10…, 0020 10… and 010… are one "
                + "number — KAFF-119 rule 5, and this is now the whole of the control")
                .Which.Name.Should().Be(Name, "a warning that does not say whose number it is was not what was ruled");
        }
    }

    // ---- AC-119-D · the warning does not block the save ---------------------------------------

    [Fact]
    public async Task A_duplicate_phone_is_asked_about_once_and_then_saved()
    {
        string phone = UniqueNames.Phone().Entered;

        Guid firstId = await IdOfAsync(await RegisterAsync(
            _marketing, Role.MarketingSales, Department.Marketing, Body("العميل الأصلي", phone: phone)));

        HttpResponseMessage asked = await RegisterAsync(
            _marketing, Role.MarketingSales, Department.Marketing, Body("المدير التنفيذي", phone: phone));

        asked.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "an unacknowledged duplicate is a question, not a refusal — the same request with the "
            + "flag succeeds");

        (await MessageKeyAsync(asked)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage proceeded = await RegisterAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("المدير التنفيذي", phone: phone, acknowledged: true));

        proceeded.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "spec.md §2, amended: it does not block the save");

        Guid secondId = await IdOfAsync(proceeded);

        Client first = await ReadClientAsync(firstId);
        Client second = await ReadClientAsync(secondId);

        second.Id.Should().NotBe(first.Id, "both clients exist");
        second.Code.Should().NotBe(first.Code, "each with its own code");
        second.PhoneNormalised.Should().Be(first.PhoneNormalised);
    }

    // ---- AC-119-E · the decision is in the trail ----------------------------------------------

    [Fact]
    public async Task The_acknowledgement_is_recorded_and_names_the_client_it_matched()
    {
        string phone = UniqueNames.Phone().Entered;

        Guid matchedId = await IdOfAsync(await RegisterAsync(
            _marketing, Role.MarketingSales, Department.Marketing, Body("العميل المطابق", phone: phone)));

        Guid createdId = await IdOfAsync(await RegisterAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("العميل الثاني", phone: phone, acknowledged: true)));

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord acknowledgement = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.EntityId == matchedId,
            Ct);

        acknowledgement.Action.Should().Be(AuditAction.Occurred);
        acknowledgement.EntityType.Should().Be(nameof(Client));
        acknowledgement.ActorUserId.Should().Be(_marketing, "a human made the call and the trail names them");

        AuditRecord creation = await reader.AuditRecords.SingleAsync(
            record => record.EntityId == createdId && record.Action == AuditAction.Created,
            Ct);

        acknowledgement.CorrelationId.Should().Be(
            creation.CorrelationId,
            "one request, one save, one correlation id — the acknowledgement and the client it "
            + "created are joinable without a text column");
    }

    /// <summary>
    /// D-107 §2: <i>"no match, the flag is ignored — never record a duplicate that was not there."</i>
    /// </summary>
    [Fact]
    public async Task An_acknowledgement_of_nothing_is_not_recorded()
    {
        Guid id = await IdOfAsync(await RegisterAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("عميل بلا تطابق", acknowledged: true)));

        await using KaffDbContext reader = _database.CreateBareContext();

        Guid correlationId = await reader.AuditRecords
            .Where(record => record.EntityId == id && record.Action == AuditAction.Created)
            .Select(record => record.CorrelationId)
            .SingleAsync(Ct);

        bool recorded = await reader.AuditRecords.AnyAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.CorrelationId == correlationId,
            Ct);

        recorded.Should().BeFalse(
            "the flag says the operator saw a warning; this handler decides whether there was one, "
            + "and a duplicate that was never there must not enter an append-only table");
    }

    // ---- AC-119-F · an archived match still warns, and says it is archived --------------------

    [Fact]
    public async Task An_archived_client_still_warns_and_is_marked_archived()
    {
        string phone = UniqueNames.Phone().Entered;

        Guid archivedId = await IdOfAsync(await RegisterAsync(
            _marketing, Role.MarketingSales, Department.Marketing, Body("عميل مؤرشف", phone: phone)));

        await ArchiveAsync(archivedId);

        IReadOnlyList<PhoneMatch> matches = await CheckAsync(
            _marketing, Role.MarketingSales, Department.Marketing, phone);

        matches.Should().ContainSingle(
            "an archived client is still a client, and spec.md §3 attaches a reopened opportunity to "
            + "the original")
            .Which.IsArchived.Should().BeTrue("the warning must say the match is archived");

        (await RegisterAsync(
                _marketing,
                Role.MarketingSales,
                Department.Marketing,
                Body("عميل جديد بنفس الرقم", phone: phone, acknowledged: true)))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the save is still permitted");
    }

    // ---- AC-119-G · a portal client cannot reach the client master ----------------------------

    [Fact]
    public async Task A_portal_client_is_refused_by_both_client_endpoints()
    {
        (await RegisterAsync(
                _portalClient, Role.Client, null, Body("محاولة من البوابة"), actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "spec.md §12 — no client ever sees another client's data");

        (await CheckResponseAsync(
                _portalClient, Role.Client, null, "01000000000", actorClientId: _portalClientCompany))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "phone-check returns client NAMES. A route called 'check' reads as innocuous and is "
                + "exactly where Role.Client gets forgotten");
    }

    // ---- AC-119-H · nobody outside Marketing and the Owner may register one -------------------

    [Fact]
    public async Task Only_marketing_and_the_owner_may_register_a_client()
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
            (await RegisterAsync(actor, role, department, Body($"محاولة {role}"), actorSubDepartment: sub))
                .StatusCode.Should().Be(
                    HttpStatusCode.Forbidden,
                    "{0} does not hold ClientManage — spec.md §2, Client is owned by Marketing",
                    role);
        }

        (await RegisterAsync(_owner, Role.Owner, null, Body("عميل سجله المالك")))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the Owner holds every company-wide row");
    }

    // ---- AC-119-I and AC-119-J · no money and no withholding category ------------------------

    /// <summary>
    /// The entity, the API contract and the table, each pinned to an exact member set.
    /// </summary>
    /// <remarks>
    /// <b>A whitelist, not a search for suspect words.</b> decisions.md D-106: a seven-word blocklist
    /// let a <c>decimal RetainedAmount</c> onto the wire against a green 241/241 suite, because
    /// <c>Amount</c> was not one of the seven — and several of the words it missed are spec.md §14's
    /// own mandated vocabulary. Any added member fails this, whatever it is called, which is what
    /// makes <c>AC-119-I</c> and <c>AC-119-J</c> hold against a field nobody predicted.
    /// </remarks>
    [Fact]
    public async Task The_client_carries_no_money_and_no_withholding_category_in_entity_contract_or_table()
    {
        typeof(Client).GetProperties().Select(property => property.Name).Should().BeEquivalentTo(
            [
                nameof(Client.Code), nameof(Client.Name), nameof(Client.PhoneEntered),
                nameof(Client.PhoneNormalised), nameof(Client.AlternatePhone), nameof(Client.Email),
                nameof(Client.Address), nameof(Client.Kind), nameof(Client.TaxRegistrationNumber),
                nameof(Client.Notes), nameof(Client.IsActive), nameof(Client.CreatedAt),
                nameof(Client.Phone), nameof(Client.Id),
            ],
            "spec.md §6.1 and CLAUDE.md — no stored balance, no credit limit; and D-049 ruling 9 "
            + "moved the withholding category to the contract, where Project.WithholdingCategory is");

        typeof(Kaff.Api.Features.Clients.CreateClient.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["Id", "Code", "Name", "Phone", "Kind", "IsActive"]);

        typeof(PhoneMatch).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Id", "Code", "Name", "IsArchived"]);

        // AC-120-F, "and no endpoint accepts one". Until 2026-09-04 the two request types were
        // guarded only by blocklists — NotContain("Code") here, NotContain("IsActive") in
        // EditClientTests — which is the shape D-106 already caught letting a decimal RetainedAmount
        // onto the wire past a green suite. What a request accepts is the same kind of claim as what
        // a response emits, so it is pinned the same way.
        string[] writable =
        [
            "Name", "Phone", "Kind", "AlternatePhone", "Email", "Address",
            "TaxRegistrationNumber", "AcknowledgedDuplicatePhone",
        ];

        typeof(Kaff.Api.Features.Clients.CreateClient.Request)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                writable,
                "a withholding category, a rate, a balance or a credit limit added to the create "
                + "request fails here, whatever it is named — D-049 ruling 9 put the category on the "
                + "contract, and spec.md §6.7 gives the client no rate to carry");

        typeof(Kaff.Api.Features.Clients.EditClient.Request)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [.. writable, "Notes"],
                "the edit request is the create request plus internal notes — KAFF-121 rule 8 — and "
                + "nothing else; the code and IsActive absences are asserted with their own reasons "
                + "in EditClientTests");

        await using KaffDbContext reader = _database.CreateBareContext();

        List<string> columns = await reader.Database
            .SqlQuery<string>(
                $"""
                 SELECT column_name::text AS "Value" FROM information_schema.columns
                 WHERE table_name = 'clients'
                 """)
            .ToListAsync(Ct);

        columns.Should().BeEquivalentTo(
            [
                "id", "code", "name", "phone_entered", "phone_normalised", "alternate_phone",
                "email", "address", "kind", "tax_registration_number", "notes", "is_active",
                "created_at",
            ],
            "a money column or a withholding_category column added to this table fails here, "
            + "whatever it is named");
    }

    // ---- AC-119-K · an individual may not carry a tax registration number ---------------------

    [Fact]
    public async Task An_individual_cannot_be_given_a_tax_registration_number()
    {
        HttpResponseMessage response = await RegisterAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            Body("عميل فرد", kind: ClientKind.Individual, taxRegistrationNumber: "123-456-789"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await MessageKeyAsync(response)).Should().Be(
            "errors.master.individual_does_not_withhold",
            "spec.md §6.7 — an individual with a registration number is making the claim the "
            + "withholding rate used to, by another field");

        (await RegisterAsync(
                _marketing,
                Role.MarketingSales,
                Department.Marketing,
                Body("عميل فرد بلا رقم ضريبي", kind: ClientKind.Individual)))
            .StatusCode.Should().Be(HttpStatusCode.Created, "an individual without one is ordinary");
    }

    /// <summary>KAFF-119 rule 8 — a client is either Individual or Corporate, and never neither.</summary>
    [Fact]
    public async Task A_registration_that_names_no_kind_is_refused()
    {
        HttpResponseMessage response = await RegisterAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            new { name = "عميل بلا نوع", phone = UniqueNames.Phone().Entered });

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "an absent kind binds to the enum's zero, which is not a member, and the enum-as-string "
            + "convention would store the literal text \"0\"");

        (await MessageKeyAsync(response)).Should().Be("errors.master.client_kind_required");
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>Sequential, of the form <c>C-10001</c>, no zero padding. spec.md §2, amended.</summary>
    private const string CodeShape = @"^C-\d{5,}$";

    private static long NumberIn(string code) =>
        long.Parse(code[2..], CultureInfo.InvariantCulture);

    private static object Body(
        string name,
        string? phone = null,
        ClientKind kind = ClientKind.Corporate,
        bool acknowledged = false,
        string? taxRegistrationNumber = null) => new
        {
            name,
            phone = phone ?? UniqueNames.Phone().Entered,
            kind = kind.ToString(),
            alternatePhone = (string?)null,
            email = (string?)null,
            address = (string?)null,
            taxRegistrationNumber,
            acknowledgedDuplicatePhone = acknowledged,
        };

    private Task<HttpResponseMessage> RegisterAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        object body,
        OperationsSubDepartment? actorSubDepartment = null,
        Guid? actorClientId = null)
        => SendAsync("/api/clients", actorId, actorRole, actorDepartment, body, actorSubDepartment, actorClientId);

    private async Task<IReadOnlyList<PhoneMatch>> CheckAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        string phone)
    {
        HttpResponseMessage response = await CheckResponseAsync(actorId, actorRole, actorDepartment, phone);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the warning is a 200 body: a ProblemDetails could not name the matched client");

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

    private Task<HttpResponseMessage> CheckResponseAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        string phone,
        Guid? actorClientId = null)
        => SendAsync(
            "/api/clients/phone-check", actorId, actorRole, actorDepartment, new { phone }, null, actorClientId);

    private async Task<HttpResponseMessage> SendAsync(
        string route,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        object body,
        OperationsSubDepartment? actorSubDepartment,
        Guid? actorClientId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative))
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

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
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
            UniqueNames.Code("CLI-C1"),
            "عميل بوابة العملاء",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        User owner = MakeUser("cli-owner", Role.Owner);
        User marketing = MakeUser("cli-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("cli-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser(
            "cli-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User hr = MakeUser("cli-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "cli-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User portal = MakeUser("cli-portal", Role.Client, clientId: company.Id);

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
