using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-212 — the supplier master's own endpoints: create, edit, list, get, archive and phone-check.
/// decisions.md D-139 §§1/5/6, D-141, D-147. Unlike the subcontractor, there is no split tax
/// registration endpoint — D-147 point 5 confirmed <c>SupplierManage</c> already owns the whole
/// record, so both the profile and the tax registration number are edited by the same route.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SupplierTests : IAsyncLifetime
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

    public SupplierTests(PostgresDatabase database) => _database = database;

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

    // ---- TC-2-142 / AC-212-A ---------------------------------------------------------------------

    [Fact]
    public async Task A_supplier_is_created_once_and_carries_no_project_field()
    {
        HttpResponseMessage response = await CreateRawAsync(
            _finance, Role.Finance, Department.Finance, Body("مورد الحديد", address: "القاهرة"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Supplier created = await ReadAsync(await IdOfAsync(response));

        created.Name.Should().Be("مورد الحديد");
        created.Address.Should().Be("القاهرة");
        created.IsActive.Should().BeTrue();

        // AC-212-A: the record has no project field to carry one at all.
        typeof(Supplier).GetProperty("ProjectId").Should().BeNull();
    }

    [Fact]
    public async Task The_owner_may_also_register_a_supplier()
    {
        (await CreateRawAsync(_owner, Role.Owner, null, Body("مورد سجله المالك")))
            .StatusCode.Should().Be(HttpStatusCode.Created, "D-129 §1 — the Owner keeps SupplierManage");
    }

    // ---- TC-2-148 / AC-212-G — warn-and-acknowledge, never refused ------------------------------

    [Fact]
    public async Task A_repeated_supplier_phone_warns_and_is_acknowledged_never_refused()
    {
        string phone = UniqueNames.Phone().Entered;

        Guid firstId = await IdOfAsync(await CreateRawAsync(
            _finance, Role.Finance, Department.Finance, Body("المورد الأول", phone: phone)));

        HttpResponseMessage refused = await CreateRawAsync(
            _finance, Role.Finance, Department.Finance, Body("المورد الثاني", phone: phone));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(refused)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage proceeded = await CreateRawAsync(
            _finance, Role.Finance, Department.Finance, Body("المورد الثاني", phone: phone, acknowledged: true));

        proceeded.StatusCode.Should().Be(HttpStatusCode.Created, "D-139 §1 — warn, never block");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord acknowledgement = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.EntityId == firstId,
            Ct);

        acknowledgement.EntityType.Should().Be(nameof(Supplier));
        acknowledgement.ActorUserId.Should().Be(_finance);
    }

    [Fact]
    public async Task An_unacknowledged_duplicate_supplier_phone_writes_nothing()
    {
        string phone = UniqueNames.Phone().Entered;

        await CreateRawAsync(_finance, Role.Finance, Department.Finance, Body("الأصلي", phone: phone));

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.Suppliers.LongCountAsync(Ct);
        long auditBefore = await before.AuditRecords.LongCountAsync(Ct);

        HttpResponseMessage refused = await CreateRawAsync(
            _finance, Role.Finance, Department.Finance, Body("محاولة", phone: phone));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.Suppliers.LongCountAsync(Ct)).Should().Be(countBefore, "the refused create wrote no row");
        (await after.AuditRecords.LongCountAsync(Ct)).Should().Be(auditBefore, "and no audit record");
    }

    [Fact]
    public async Task Editing_a_supplier_without_changing_the_phone_does_not_warn()
    {
        Guid id = await IdOfAsync(await CreateRawAsync(_finance, Role.Finance, Department.Finance, Body("مورد")));

        Supplier supplier = await ReadAsync(id);

        HttpResponseMessage edited = await EditRawAsync(
            id, _finance, Role.Finance, Department.Finance, EditBody(supplier));

        edited.StatusCode.Should().Be(
            HttpStatusCode.OK, "a record saved with its own phone unchanged must never match itself");
    }

    [Fact]
    public async Task Supplier_phone_match_is_on_the_normalised_form()
    {
        string national = UniqueNames.Phone().Entered;
        string bare = national[1..];

        await CreateRawAsync(_finance, Role.Finance, Department.Finance, Body("مورد الرقم", phone: national));

        HttpResponseMessage check = await PhoneCheckAsync("+20 " + bare);
        check.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await check.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("matches").EnumerateArray().Should().ContainSingle(
            "+20…, 0020… and 010… all reduce to the same normalised key");
    }

    // ---- TC-2-149 / AC-212-H — only SupplierManage reaches this, Technical Office included -------

    [Fact]
    public async Task A_role_without_SupplierManage_reaches_nothing_technical_office_included()
    {
        Guid seedId = await IdOfAsync(
            await CreateRawAsync(_finance, Role.Finance, Department.Finance, Body("مورد محمي")));

        Supplier seed = await ReadAsync(seedId);

        (Guid Actor, Role Role, Department? Department)[] refused =
        [
            (_technicalOffice, Role.TechnicalOffice, Department.Operations),
            (_hr, Role.Hr, Department.Hr),
            (_marketing, Role.MarketingSales, Department.Marketing),
            (_siteEngineer, Role.SiteEngineer, Department.Operations),
        ];

        foreach ((Guid actor, Role role, Department? department) in refused)
        {
            (await CreateRawAsync(actor, role, department, Body($"محاولة {role}")))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} does not hold SupplierManage", role);

            (await EditRawAsync(seedId, actor, role, department, EditBody(seed)))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} edit", role);

            (await ArchiveRawAsync(seedId, actor, role, department))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} archive", role);
        }

        (await ReadAsync(seedId)).IsActive.Should().BeTrue("none of the refused calls changed anything");
    }

    // ---- TC-2-147 / AC-212-F — no withholding member on the wire ---------------------------------

    [Fact]
    public void The_create_and_edit_requests_carry_no_withholding_or_project_field()
    {
        string[] createMembers =
            [.. typeof(Kaff.Api.Features.Suppliers.CreateSupplier.Request).GetProperties()
                .Select(property => property.Name)];

        createMembers.Should().BeEquivalentTo(
            ["Name", "Phone", "Address", "TaxRegistrationNumber", "AcknowledgedDuplicatePhone"],
            "D-139 §5 — no withholding rate or category member, ever; rule 2 — no project member");

        string[] editMembers =
            [.. typeof(Kaff.Api.Features.Suppliers.EditSupplier.Request).GetProperties()
                .Select(property => property.Name)];

        editMembers.Should().BeEquivalentTo(createMembers, "the same allow-list");
    }

    // ---- TC-2-152 / AC-212-K — nothing here is a bid ----------------------------------------------

    [Fact]
    public void Nothing_in_the_supplier_slice_names_a_bid_a_quotation_or_an_rfq()
    {
        string[] forbidden = ["bid", "quote", "quotation", "rfq", "comparison"];

        string[] routes =
        [
            Kaff.Api.Features.Suppliers.CreateSupplier.Endpoint.Route,
            Kaff.Api.Features.Suppliers.EditSupplier.Endpoint.Route,
            Kaff.Api.Features.Suppliers.ListSuppliers.Endpoint.Route,
            Kaff.Api.Features.Suppliers.GetSupplier.Endpoint.Route,
            Kaff.Api.Features.Suppliers.ArchiveSupplier.Endpoint.Route,
            Kaff.Api.Features.Suppliers.PhoneCheck.Endpoint.Route,
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

    // ---- TC-2-151 / AC-212-J — audit before and after, both fields in one request -----------------

    [Fact]
    public async Task Editing_address_and_tax_registration_together_writes_one_audit_record_with_both_fields()
    {
        Guid id = await IdOfAsync(await CreateRawAsync(
            _finance, Role.Finance, Department.Finance, Body("مورد التدقيق", address: "القاهرة")));

        Supplier before = await ReadAsync(id);

        HttpResponseMessage edited = await EditRawAsync(
            id, _finance, Role.Finance, Department.Finance,
            new
            {
                name = before.Name,
                phone = before.PhoneEntered,
                address = "الإسكندرية",
                taxRegistrationNumber = "222-999",
                acknowledgedDuplicatePhone = false,
            });

        edited.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord modified = await reader.AuditRecords
            .Where(record => record.Action == AuditAction.Modified && record.EntityId == id)
            .OrderByDescending(record => record.OccurredAt)
            .FirstAsync(Ct);

        modified.ChangedProperties.Should().Contain("Address");
        modified.ChangedProperties.Should().Contain("TaxRegistrationNumber");
    }

    // ---- not found / archive -----------------------------------------------------------------

    [Fact]
    public async Task Editing_a_supplier_that_does_not_exist_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await EditRawAsync(
            Guid.NewGuid(), _finance, Role.Finance, Department.Finance,
            new
            {
                name = "لا يوجد",
                phone = UniqueNames.Phone().Entered,
                address = (string?)null,
                taxRegistrationNumber = (string?)null,
                acknowledgedDuplicatePhone = false,
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.master.supplier_not_found");
    }

    [Fact]
    public async Task Archiving_replaces_deletion_and_an_already_archived_supplier_is_refused()
    {
        Guid id = await IdOfAsync(
            await CreateRawAsync(_finance, Role.Finance, Department.Finance, Body("مورد يُؤرشف")));

        (await ArchiveRawAsync(id, _finance, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadAsync(id)).IsActive.Should().BeFalse();

        (await ArchiveRawAsync(id, _finance, Role.Finance, Department.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "already archived");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(string name, string? phone = null, string? address = null, bool acknowledged = false) => new
    {
        name,
        phone = phone ?? UniqueNames.Phone().Entered,
        address,
        taxRegistrationNumber = (string?)null,
        acknowledgedDuplicatePhone = acknowledged,
    };

    private static object EditBody(Supplier supplier) => new
    {
        name = supplier.Name,
        phone = supplier.PhoneEntered,
        address = supplier.Address,
        taxRegistrationNumber = supplier.TaxRegistrationNumber,
        acknowledgedDuplicatePhone = false,
    };

    private Task<HttpResponseMessage> CreateRawAsync(
        Guid actorId, Role actorRole, Department? department, object body)
        => SendAsync(HttpMethod.Post, "/api/suppliers", actorId, actorRole, department, body);

    private Task<HttpResponseMessage> EditRawAsync(
        Guid id, Guid actorId, Role actorRole, Department? department, object body)
        => SendAsync(HttpMethod.Put, $"/api/suppliers/{id}", actorId, actorRole, department, body);

    private Task<HttpResponseMessage> ArchiveRawAsync(Guid id, Guid actorId, Role actorRole, Department? department)
        => SendAsync(HttpMethod.Post, $"/api/suppliers/{id}/archive", actorId, actorRole, department, null);

    private async Task<HttpResponseMessage> PhoneCheckAsync(string phone)
        => await SendAsync(
            HttpMethod.Post, "/api/suppliers/phone-check", _finance, Role.Finance, Department.Finance, new { phone });

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, Department? department, object? body)
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

    private async Task<Supplier> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Suppliers.SingleAsync(candidate => candidate.Id == id, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User owner = MakeUser("sup-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "sup-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("sup-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("sup-hr", Role.Hr, Department.Hr);
        User siteEngineer = MakeUser(
            "sup-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("sup-marketing", Role.MarketingSales, Department.Marketing);

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
