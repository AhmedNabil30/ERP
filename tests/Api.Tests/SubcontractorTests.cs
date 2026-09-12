using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-211 — the subcontractor master's own endpoints: create, edit, list, get, archive and
/// phone-check. decisions.md D-139 §§1/4/5, D-141, D-147. The tax registration mechanism D-147 split
/// off has its own file, <c>SubcontractorTaxRegistrationTests</c>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SubcontractorTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _hr;
    private Guid _siteEngineer;
    private Guid _marketing;

    public SubcontractorTests(PostgresDatabase database) => _database = database;

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

    // ---- TC-2-127 / AC-211-A -------------------------------------------------------------------

    [Fact]
    public async Task A_subcontractor_is_created_with_a_trade_and_the_default_retention()
    {
        Guid bab = await CreateBabAsync();

        HttpResponseMessage response = await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("مقاول الحفر", tradeBabId: bab));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Subcontractor created = await ReadAsync(await IdOfAsync(response));

        created.RetentionRate.Fraction.Should().Be(0.05m, "spec.md §5.1 — 5% by default");
        created.IsActive.Should().BeTrue();
        created.TradeBabId.Should().Be(bab);
    }

    [Fact]
    public async Task The_owner_may_also_register_a_subcontractor()
    {
        (await CreateRawAsync(_owner, Role.Owner, null, null, Body("مقاول سجله المالك")))
            .StatusCode.Should().Be(HttpStatusCode.Created, "D-129 §1 — the Owner keeps SubcontractorManage");
    }

    // ---- TC-2-128 / AC-211-B --------------------------------------------------------------------

    [Fact]
    public async Task Retention_is_zeroed_for_one_firm_and_no_other()
    {
        Guid firstId = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("الأول")));

        Guid secondId = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("الثاني")));

        Subcontractor first = await ReadAsync(firstId);

        HttpResponseMessage edited = await EditRawAsync(
            firstId,
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            EditBody(first, retentionRate: 0m));

        edited.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadAsync(firstId)).RetentionRate.Fraction.Should().Be(0m);
        (await ReadAsync(secondId)).RetentionRate.Fraction.Should().Be(
            0.05m, "zeroing one firm must change nothing for another");
    }

    // ---- TC-2-135 / AC-211-I — warn-and-acknowledge, never refused ------------------------------

    [Fact]
    public async Task A_repeated_subcontractor_phone_warns_and_is_acknowledged_never_refused()
    {
        string phone = UniqueNames.Phone().Entered;

        Guid firstId = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("الشركة الأولى", phone: phone)));

        HttpResponseMessage refused = await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("الشركة الثانية", phone: phone));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(refused)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage proceeded = await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("الشركة الثانية", phone: phone, acknowledged: true));

        proceeded.StatusCode.Should().Be(HttpStatusCode.Created, "D-139 §1 — warn, never block");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord acknowledgement = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.EntityId == firstId,
            Ct);

        acknowledgement.EntityType.Should().Be(nameof(Subcontractor));
        acknowledgement.ActorUserId.Should().Be(_technicalOffice);
    }

    [Fact]
    public async Task An_unacknowledged_duplicate_subcontractor_phone_writes_nothing()
    {
        string phone = UniqueNames.Phone().Entered;

        await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("الأصلي", phone: phone));

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.Subcontractors.LongCountAsync(Ct);
        long auditBefore = await before.AuditRecords.LongCountAsync(Ct);

        HttpResponseMessage refused = await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("محاولة", phone: phone));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.Subcontractors.LongCountAsync(Ct)).Should().Be(countBefore, "the refused create wrote no row");
        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(auditBefore, "and no audit record");
    }

    [Fact]
    public async Task Editing_a_subcontractor_without_changing_the_phone_does_not_warn()
    {
        Guid id = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("شركة")));

        Subcontractor subcontractor = await ReadAsync(id);

        HttpResponseMessage edited = await EditRawAsync(
            id,
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            EditBody(subcontractor));

        edited.StatusCode.Should().Be(
            HttpStatusCode.OK, "a record saved with its own phone unchanged must never match itself");
    }

    [Fact]
    public async Task Subcontractor_phone_match_is_on_the_normalised_form()
    {
        string national = UniqueNames.Phone().Entered;
        string bare = national[1..];

        await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("شركة الرقم", phone: national));

        HttpResponseMessage check = await PhoneCheckAsync("+20 " + bare);
        check.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await check.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("matches").EnumerateArray().Should().ContainSingle(
            "+20…, 0020… and 010… all reduce to the same normalised key");
    }

    // ---- TC-2-136 / AC-211-J — only SubcontractorManage reaches this, Finance included ----------

    [Fact]
    public async Task A_role_without_SubcontractorManage_reaches_nothing_finance_included()
    {
        Guid bab = await CreateBabAsync();
        Guid seedId = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("شركة محمية")));

        Subcontractor seed = await ReadAsync(seedId);

        (Guid Actor, Role Role, Department? Department, OperationsSubDepartment? Sub)[] refused =
        [
            (_finance, Role.Finance, Department.Finance, null),
            (_hr, Role.Hr, Department.Hr, null),
            (_marketing, Role.MarketingSales, Department.Marketing, null),
            (_siteEngineer, Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical),
        ];

        foreach ((Guid actor, Role role, Department? department, OperationsSubDepartment? sub) in refused)
        {
            (await CreateRawAsync(actor, role, department, sub, Body($"محاولة {role}", tradeBabId: bab)))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} does not hold SubcontractorManage", role);

            (await EditRawAsync(seedId, actor, role, department, sub, EditBody(seed)))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} edit", role);

            (await ArchiveRawAsync(seedId, actor, role, department, sub))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} archive", role);
        }

        (await ReadAsync(seedId)).IsActive.Should().BeTrue("none of the refused calls changed anything");
    }

    // ---- TC-2-134 / AC-211-H — no rate card of any kind ------------------------------------------

    [Fact]
    public void The_create_and_edit_requests_carry_no_rate_card_or_tax_registration_field()
    {
        string[] createMembers =
            [.. typeof(Kaff.Api.Features.Subcontractors.CreateSubcontractor.Request).GetProperties()
                .Select(property => property.Name)];

        createMembers.Should().BeEquivalentTo(
            ["Name", "Phone", "TradeBabId", "RetentionRate", "AcknowledgedDuplicatePhone"],
            "D-139 §4 — no rate-card field, ever; D-147 point 3 — no TaxRegistrationNumber member");

        string[] editMembers =
            [.. typeof(Kaff.Api.Features.Subcontractors.EditSubcontractor.Request).GetProperties()
                .Select(property => property.Name)];

        editMembers.Should().BeEquivalentTo(createMembers, "the same allow-list, AC-211-N");
    }

    // ---- TC-2-138 / AC-211-L — nothing here is a bid ----------------------------------------------

    [Fact]
    public void Nothing_in_the_subcontractor_slice_names_a_bid_a_quotation_or_an_rfq()
    {
        string[] forbidden = ["bid", "quote", "quotation", "rfq", "comparison"];

        string[] routes =
        [
            Kaff.Api.Features.Subcontractors.CreateSubcontractor.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.EditSubcontractor.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.ListSubcontractors.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.GetSubcontractor.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.ArchiveSubcontractor.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.PhoneCheck.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.SetTaxRegistration.Endpoint.Route,
            Kaff.Api.Features.Subcontractors.ListTaxRegistrations.Endpoint.Route,
        ];

        foreach (string route in routes)
        {
            string lowered = route.ToLowerInvariant();

            foreach (string word in forbidden)
            {
                lowered.Should().NotContain(word);
            }
        }
    }

    // ---- V-38-H / decisions.md D-151 — the rate is a fraction, typed Percentage, on the wire -----

    [Fact]
    public async Task Retention_rate_survives_a_read_then_write_round_trip()
    {
        Guid id = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("شركة الجولة الكاملة")));

        Subcontractor seed = await ReadAsync(id);

        string firstRate = await RetentionRateStringAsync(await GetRawAsync(id));

        HttpResponseMessage edited = await EditRawAsync(
            id,
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            RawEditBody(seed, firstRate));

        edited.StatusCode.Should().Be(HttpStatusCode.OK);

        string secondRate = await RetentionRateStringAsync(await GetRawAsync(id));

        secondRate.Should().Be(
            firstRate,
            "D-151 — the wire unit is the fraction in both directions; echoing a read back must not " +
            "divide it by 100 a second time");
    }

    [Fact]
    public async Task Retention_rate_on_the_wire_is_the_fraction_in_both_directions()
    {
        Guid fivePercentId = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            RawCreateBody("شركة نص العشرة", "0.05")));

        (await ReadAsync(fivePercentId)).RetentionRate.Fraction.Should().Be(0.05m);
        (await RetentionRateStringAsync(await GetRawAsync(fivePercentId))).Should().Be("0.050000");

        Guid fiveHundredPercentId = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            RawCreateBody("شركة الخمسة الكاملة", "5")));

        (await ReadAsync(fiveHundredPercentId)).RetentionRate.Fraction.Should().Be(
            5m, "'5' on the wire is the fraction 5 — 500% — and must never be silently read as 5%");
    }

    [Fact]
    public async Task A_put_without_a_retention_rate_is_refused_not_zeroed()
    {
        Guid id = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("شركة بلا نسبة استقطاع")));

        Subcontractor seed = await ReadAsync(id);

        var body = new JsonObject
        {
            ["name"] = seed.Name,
            ["phone"] = seed.PhoneEntered,
            ["tradeBabId"] = seed.TradeBabId,
            ["acknowledgedDuplicatePhone"] = false,
        };

        HttpResponseMessage response = await EditRawAsync(
            id,
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.retention_rate_required");

        (await ReadAsync(id)).RetentionRate.Fraction.Should().Be(
            0.05m, "an omitted rate must be refused, never treated as zero");
    }

    [Fact]
    public async Task A_negative_retention_rate_is_a_400_carrying_a_message_key()
    {
        HttpResponseMessage response = await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            RawCreateBody("شركة نسبة سالبة", "-0.05"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.wire.malformed_body");
    }

    // ---- not found ---------------------------------------------------------------------------

    [Fact]
    public async Task Editing_a_subcontractor_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await EditRawAsync(
            Guid.NewGuid(),
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            new { name = "لا يوجد", phone = UniqueNames.Phone().Entered, retentionRate = 5m, acknowledgedDuplicatePhone = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.master.subcontractor_not_found");
    }

    [Fact]
    public async Task Archiving_replaces_deletion_and_an_already_archived_firm_is_refused()
    {
        Guid id = await IdOfAsync(await CreateRawAsync(
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            Body("شركة تُؤرشف")));

        (await ArchiveRawAsync(
                id, _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadAsync(id)).IsActive.Should().BeFalse();

        (await ArchiveRawAsync(
                id, _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "already archived");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(
        string name, string? phone = null, Guid? tradeBabId = null, bool acknowledged = false) => new
        {
            name,
            phone = phone ?? UniqueNames.Phone().Entered,
            tradeBabId,
            acknowledgedDuplicatePhone = acknowledged,
        };

    private static object EditBody(Subcontractor subcontractor, decimal? retentionRate = null) => new
    {
        name = subcontractor.Name,
        phone = subcontractor.PhoneEntered,
        tradeBabId = subcontractor.TradeBabId,
        retentionRate = retentionRate ?? subcontractor.RetentionRate.Fraction,
        acknowledgedDuplicatePhone = false,
    };

    private Task<HttpResponseMessage> CreateRawAsync(
        Guid actorId, Role actorRole, Department? department, OperationsSubDepartment? sub, object body)
        => SendAsync(HttpMethod.Post, "/api/subcontractors", actorId, actorRole, department, sub, body);

    private Task<HttpResponseMessage> EditRawAsync(
        Guid id, Guid actorId, Role actorRole, Department? department, OperationsSubDepartment? sub, object body)
        => SendAsync(HttpMethod.Put, $"/api/subcontractors/{id}", actorId, actorRole, department, sub, body);

    private Task<HttpResponseMessage> GetRawAsync(Guid id)
        => SendAsync(
            HttpMethod.Get, $"/api/subcontractors/{id}",
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical, null);

    /// <summary>Reads <c>retentionRate</c> as a raw JSON string — no arithmetic, no reparse as decimal.</summary>
    private static async Task<string> RetentionRateStringAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("retentionRate").GetString()!;
    }

    /// <summary>A create body carrying <paramref name="retentionRate"/> exactly as written — no unit conversion.</summary>
    private static JsonObject RawCreateBody(string name, string retentionRate) => new()
    {
        ["name"] = name,
        ["phone"] = UniqueNames.Phone().Entered,
        ["retentionRate"] = retentionRate,
        ["acknowledgedDuplicatePhone"] = false,
    };

    /// <summary>An edit body carrying <paramref name="retentionRate"/> exactly as written — no unit conversion.</summary>
    private static JsonObject RawEditBody(Subcontractor subcontractor, string retentionRate) => new()
    {
        ["name"] = subcontractor.Name,
        ["phone"] = subcontractor.PhoneEntered,
        ["tradeBabId"] = subcontractor.TradeBabId,
        ["retentionRate"] = retentionRate,
        ["acknowledgedDuplicatePhone"] = false,
    };

    private Task<HttpResponseMessage> ArchiveRawAsync(
        Guid id, Guid actorId, Role actorRole, Department? department, OperationsSubDepartment? sub)
        => SendAsync(HttpMethod.Post, $"/api/subcontractors/{id}/archive", actorId, actorRole, department, sub, null);

    private async Task<HttpResponseMessage> PhoneCheckAsync(string phone)
        => await SendAsync(
            HttpMethod.Post, "/api/subcontractors/phone-check",
            _technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical,
            new { phone });

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string route,
        Guid actorId,
        Role actorRole,
        Department? department,
        OperationsSubDepartment? sub,
        object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
        }

        if (sub is not null)
        {
            request.Headers.Add(TestAuthHandler.SubDepartmentHeader, sub.Value.ToString());
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

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task<Subcontractor> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Subcontractors.SingleAsync(candidate => candidate.Id == id, Ct);
    }

    private async Task<Guid> CreateBabAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Bab bab = Bab.Create(UniqueNames.Code("SCT-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        context.Babs.Add(bab);
        await context.SaveChangesAsync(Ct);

        return bab.Id;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("sct-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "sct-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("sct-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("sct-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "sct-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("sct-marketing", Role.MarketingSales, Department.Marketing);

        context.Users.AddRange(owner, technicalOffice, finance, hr, siteEngineer, marketing);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _marketing = marketing.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
