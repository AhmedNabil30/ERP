using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Boqs;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Portal;

/// <summary>
/// The portal's server side (KAFF-810, KAFF-811 leak tests, R5-A, R5-B, R5-C, KAFF-812 change-order half) over HTTP, as the seeded people.
/// </summary>
/// <remarks>
/// Two clients, each with a Lump Sum project of his own. Every portal route is called as a staff role (refused), as the other client
/// (refused, and nothing of the first client's appears in the answer) and as the owner of the project (answered).
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed partial class PortalEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientA;
    private Guid _clientB;
    private Guid _portalA;
    private Guid _portalB;
    private Guid _babId;
    private Guid _safeId;
    private Guid _owner;
    private Guid _finance;
    private Guid _office1;
    private Guid _office2;
    private Guid _office3;
    private Guid _prep;
    private Guid _supervisor;
    private Guid _hr;
    private Project _projectA = null!;
    private Project _projectB = null!;
    private string _projectBName = null!;
    private string _clientBName = null!;

    public PortalEndpointTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
        _projectA = await NewProjectAsync(_clientA);
        _projectB = await NewProjectAsync(_clientB);
        _projectBName = _projectB.Name;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- KAFF-810: role x ownership, hitting every route directly --------------------------------------------------

    [Fact]
    public async Task Every_project_route_of_the_portal_refuses_every_staff_role()
    {
        foreach (Guid staff in new[] { _owner, _finance, _office1, _supervisor, _prep, _hr })
        {
            foreach ((HttpMethod method, string route, object? body) in ProjectRoutes(_projectA.Id, Guid.NewGuid(), Guid.NewGuid()))
            {
                HttpResponseMessage response = await SendAsync(method, route, staff, body);
                response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route} as a staff role: the portal is for clients");
            }
        }
    }

    [Fact]
    public async Task The_list_of_projects_refuses_staff()
    {
        foreach (Guid staff in new[] { _owner, _finance, _office1, _supervisor, _hr })
        {
            (await SendAsync(HttpMethod.Get, "/api/portal/projects", staff, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task One_client_calling_every_route_with_the_other_clients_ids_is_refused_and_learns_nothing()
    {
        foreach ((HttpMethod method, string route, object? body) in ProjectRoutes(_projectB.Id, Guid.NewGuid(), Guid.NewGuid()))
        {
            HttpResponseMessage response = await SendAsync(method, route, _portalA, body);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");

            string text = await response.Content.ReadAsStringAsync(Ct);
            text.Should().NotContain(_projectBName).And.NotContain(_clientBName).And.NotContain(_projectB.Id.ToString());
        }
    }

    [Fact]
    public async Task A_client_lists_his_own_projects_and_never_the_others()
    {
        JsonElement mine = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/portal/projects", _portalA, null));

        mine.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Should().BeEquivalentTo([_projectA.Id]);
        mine[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "name", "status", "contractType", "paid", "remaining", "pendingCount"]);
        mine[0].GetProperty("paid").GetString().Should().Be("250000.00", "the same rule as the home");
        mine[0].GetProperty("remaining").GetString().Should().Be("750000.00");
        mine[0].GetProperty("pendingCount").GetInt32().Should().Be(0);
        mine.GetRawText().Should().NotContain(_projectBName);
    }

    [Fact]
    public async Task A_client_user_with_a_password_to_change_is_refused_every_read_until_he_does()
    {
        Guid temporary = await NewPortalUserAsync(_clientA, mustChangePassword: true);

        HttpResponseMessage list = await SendAsync(HttpMethod.Get, "/api/portal/projects", temporary, null);
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(list)).Should().Be("errors.auth.password_change_required");

        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", temporary, null);
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(read)).Should().Be("errors.auth.password_change_required");
    }

    // ---- R5-B: the home ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_home_shows_progress_paid_and_remaining_as_two_decimal_strings_and_no_cost_field()
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string text = await response.Content.ReadAsStringAsync(Ct);
        JsonElement home = JsonDocument.Parse(text).RootElement;

        home.GetProperty("contractValue").GetString().Should().Be("1000000.00");
        home.GetProperty("paid").GetString().Should().Be("250000.00", "the advance is client cash");
        home.GetProperty("remaining").GetString().Should().Be("750000.00");
        home.GetProperty("pendingApprovals").GetInt32().Should().Be(0);
        home.GetProperty("sections").GetArrayLength().Should().BeGreaterThan(0);
        home.GetProperty("completion").GetString().Should().MatchRegex(@"^\d+\.\d{2}$");

        foreach (string banned in new[] { "cost", "margin", "markup", "profit", "supplier", "subcontractor", "internal", "audit" })
        {
            text.Should().NotContainEquivalentOf(banned);
        }

        Moneys().Matches(text).Select(match => match.Groups[1].Value).Should().OnlyContain(value => value.Contains('.'));
    }

    [Fact]
    public async Task Paid_follows_the_section_15_worked_example_after_extract_2()
    {
        // spec.md section 15: advance 250,000 + extract 1 cash 240,000 + extract 2 cash 120,000 = 610,000 paid.
        await using (KaffDbContext context = _database.CreateContext())
        {
            Account receivable = await context.Accounts.FirstAsync(a => a.ProjectId == _projectA.Id && a.Type == AccountType.ClientReceivable, Ct);
            Account safe = await context.Accounts.SingleAsync(a => a.Id == _safeId, Ct);
            foreach (decimal cash in new[] { 240_000m, 120_000m })
            {
                context.Postings.Add(Posting.Create(
                    receivable, safe, new Money(cash), PostingType.ClientCollection,
                    new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EX"),
                    new DateOnly(2026, 9, 25), _owner, Now, _projectA.Id).Value);
            }

            await context.SaveChangesAsync(Ct);
        }

        JsonElement home = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null));
        home.GetProperty("paid").GetString().Should().Be("610000.00");
        home.GetProperty("remaining").GetString().Should().Be("390000.00");
    }

    [Fact]
    public async Task Paid_includes_the_tax_withheld_at_source_on_a_collection()
    {
        await using (KaffDbContext context = _database.CreateContext())
        {
            Account receivable = await context.Accounts.FirstAsync(a => a.ProjectId == _projectA.Id && a.Type == AccountType.ClientReceivable, Ct);
            Account safe = await context.Accounts.SingleAsync(a => a.Id == _safeId, Ct);
            Account tax = Account.Create(
                AccountType.TaxWithheldAtSource, UniqueNames.Code("PTWHT"), "ضريبة مخصومة", "Tax withheld", Currency.Egp, new DateOnly(2026, 1, 1)).Value;
            context.Accounts.Add(tax);
            await context.SaveChangesAsync(Ct);
            var source = new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EX");
            context.Postings.Add(Posting.Create(
                receivable, safe, new Money(99_000m), PostingType.ClientCollection, source, new DateOnly(2026, 9, 25), _owner, Now, _projectA.Id).Value);
            context.Postings.Add(Posting.Create(
                receivable, tax, new Money(1_000m), PostingType.TaxWithheldAtSource, source, new DateOnly(2026, 9, 25), _owner, Now, _projectA.Id).Value);
            await context.SaveChangesAsync(Ct);
        }

        JsonElement home = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null));
        home.GetProperty("paid").GetString().Should().Be("350000.00", "250,000 advance + 99,000 cash + 1,000 withheld");
        home.GetProperty("remaining").GetString().Should().Be("650000.00");
    }

    [Fact]
    public async Task A_reversed_advance_receipt_is_not_counted_as_paid()
    {
        await using (KaffDbContext context = _database.CreateContext())
        {
            Posting receipt = await context.Postings.SingleAsync(p => p.ProjectId == _projectA.Id && p.Type == PostingType.ClientAdvanceReceipt && p.ReversesId == null, Ct);
            Account from = await context.Accounts.SingleAsync(a => a.Id == receipt.FromAccountId, Ct);
            Account to = await context.Accounts.SingleAsync(a => a.Id == receipt.ToAccountId, Ct);
            context.Postings.Add(Posting.Reverse(receipt, from, to, new DateOnly(2026, 9, 25), _owner, Now).Value);
            await context.SaveChangesAsync(Ct);
        }

        JsonElement home = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null));
        home.GetProperty("paid").GetString().Should().Be("0.00");
        home.GetProperty("remaining").GetString().Should().Be("1000000.00");
    }

    // ---- R5-B: extracts ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_extract_shows_only_once_it_is_issued()
    {
        string route = $"/api/portal/projects/{_projectA.Id}/extracts";

        Guid extract = await PrepareExtractAsync(_projectA);
        (await ReadAsync(await SendAsync(HttpMethod.Get, route, _portalA, null))).GetArrayLength().Should().Be(0, "a draft is Kaff's own business");

        await StepExtractAsync(_projectA, extract);

        JsonElement listed = await ReadAsync(await SendAsync(HttpMethod.Get, route, _portalA, null));
        listed.GetArrayLength().Should().Be(1);
        listed[0].GetProperty("id").GetGuid().Should().Be(extract);
        listed[0].GetProperty("isPaid").GetBoolean().Should().BeFalse();
        listed[0].GetProperty("paid").GetString().Should().Be("0.00");
        listed[0].GetProperty("netPayable").GetString().Should().MatchRegex(@"^-?\d+\.\d{2}$");
        listed[0].GetProperty("certifiedWork").GetString().Should().MatchRegex(@"^-?\d+\.\d{2}$");
    }

    // ---- R5-C: publish a photo -------------------------------------------------------------------------------------

    [Fact]
    public async Task A_photo_reaches_the_client_only_when_the_supervising_engineer_or_the_technical_office_publishes_it()
    {
        string photos = $"/api/portal/projects/{_projectA.Id}/photos";
        string reference = await SeedPhotoAsync(_projectA);
        string publish = $"/api/projects/{_projectA.Id}/daily-logs/photos/{reference}/publish";

        (await ReadAsync(await SendAsync(HttpMethod.Get, photos, _portalA, null))).GetArrayLength().Should().Be(0, "unpublished");
        (await SendAsync(HttpMethod.Get, $"{photos}/{reference}", _portalA, null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        foreach (Guid refused in new[] { _prep, _finance, _owner, _hr, _portalA })
        {
            (await SendAsync(HttpMethod.Post, publish, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        HttpResponseMessage published = await SendAsync(HttpMethod.Post, publish, _supervisor, null);
        published.StatusCode.Should().Be(HttpStatusCode.NoContent, await published.Content.ReadAsStringAsync(Ct));
        (await SendAsync(HttpMethod.Post, publish, _office1, null)).StatusCode.Should().Be(HttpStatusCode.NoContent, "publishing twice is not an error");

        HttpResponseMessage after = await SendAsync(HttpMethod.Get, photos, _portalA, null);
        JsonElement listed = await ReadAsync(after);
        listed.GetArrayLength().Should().Be(1);
        listed[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "caption", "takenAt"], "no location, author or log reference");

        await using KaffDbContext reader = _database.CreateBareContext();
        Guid entry = await reader.PhotoEntries.Where(p => p.FileReference == reference).Select(p => p.Id).SingleAsync(Ct);
        (await reader.Set<AuditRecord>().CountAsync(r => r.EntityId == entry && r.Action == AuditAction.Modified, Ct))
            .Should().BeGreaterThan(0, "publishing is a state change and is audited");

        (await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectB.Id}/photos", _portalB, null)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectB.Id}/photos", _portalB, null)))
            .GetArrayLength().Should().Be(0, "another client's published photo is not his");
    }

    [Fact]
    public async Task A_supervisor_not_assigned_to_the_project_cannot_publish_its_photo()
    {
        string reference = await SeedPhotoAsync(_projectA);
        Guid stranger = await NewStaffAsync("pt-stranger", Role.SiteEngineer);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{_projectA.Id}/daily-logs/photos/{reference}/publish", stranger, null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- KAFF-812, change-order half --------------------------------------------------------------------------------

    [Fact]
    public async Task A_change_order_shows_to_the_client_only_from_his_own_gate_onward()
    {
        string route = $"/api/portal/projects/{_projectA.Id}/change-orders";
        Guid id = await CreateChangeOrderAsync(_projectA);

        (await ReadAsync(await SendAsync(HttpMethod.Get, route, _portalA, null))).GetArrayLength().Should().Be(0, "draft");

        await DriveAsync(_projectA, id, ChangeOrderStatus.TechnicalReview);
        (await ReadAsync(await SendAsync(HttpMethod.Get, route, _portalA, null))).GetArrayLength().Should().Be(0, "technical review");

        await DriveAsync(_projectA, id, ChangeOrderStatus.ClientApproval);
        JsonElement listed = await ReadAsync(await SendAsync(HttpMethod.Get, route, _portalA, null));
        listed.GetArrayLength().Should().Be(1);
        listed[0].GetProperty("canDecide").GetBoolean().Should().BeTrue();
        listed[0].GetProperty("total").GetString().Should().Be("100000.00");
        listed[0].GetProperty("lines")[0].GetProperty("unitPrice").GetString().Should().Be("10000.00");

        JsonElement home = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null));
        home.GetProperty("pendingApprovals").GetInt32().Should().Be(1);

        JsonElement mine = await ReadAsync(await SendAsync(HttpMethod.Get, "/api/portal/projects", _portalA, null));
        mine[0].GetProperty("pendingCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task One_change_order_reads_with_when_it_reached_the_client_and_the_contract_value_before_and_after()
    {
        Guid id = await CreateChangeOrderAsync(_projectA);
        string route = $"/api/portal/projects/{_projectA.Id}/change-orders/{id}";

        (await SendAsync(HttpMethod.Get, route, _portalA, null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "a draft is not his to see");
        await DriveAsync(_projectA, id, ChangeOrderStatus.ClientApproval);

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, route, _portalA, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string text = await response.Content.ReadAsStringAsync(Ct);
        JsonElement order = JsonDocument.Parse(text).RootElement;
        order.GetProperty("total").GetString().Should().Be("100000.00");
        order.GetProperty("canDecide").GetBoolean().Should().BeTrue();
        order.GetProperty("contractValueBefore").GetString().Should().Be("1000000.00");
        order.GetProperty("contractValueAfter").GetString().Should().Be("1100000.00");
        order.GetProperty("submittedAt").ValueKind.Should().Be(JsonValueKind.String);
        order.GetProperty("lines").GetArrayLength().Should().Be(1);
        foreach (string banned in new[] { "cost", "margin", "markup", "profit", "supplier", "subcontractor", "internal", "audit" })
        {
            text.Should().NotContainEquivalentOf(banned);
        }

        (await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}/change-orders/{Guid.NewGuid()}", _portalA, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_home_carries_the_planned_dates_and_derives_stalled_from_them()
    {
        Project late = await NewProjectAsync(_clientA, "2019-01-01", "2020-01-01");
        Project onTime = await NewProjectAsync(_clientA, "2019-01-01", "2099-01-01");

        JsonElement stalled = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{late.Id}", _portalA, null));
        stalled.GetProperty("plannedStart").GetString().Should().Be("2019-01-01");
        stalled.GetProperty("plannedEnd").GetString().Should().Be("2020-01-01");
        stalled.GetProperty("isStalled").GetBoolean().Should().BeTrue("past the planned end and under 100 percent");

        JsonElement fine = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{onTime.Id}", _portalA, null));
        fine.GetProperty("isStalled").GetBoolean().Should().BeFalse();

        JsonElement none = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null));
        none.TryGetProperty("plannedEnd", out _).Should().BeFalse("no planned end was entered");
        none.GetProperty("isStalled").GetBoolean().Should().BeFalse("no planned end, never stalled");

        JsonElement staff = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{late.Id}", _owner, null));
        staff.GetProperty("plannedStart").GetString().Should().Be("2019-01-01");
        staff.GetProperty("plannedEnd").GetString().Should().Be("2020-01-01");
    }

    [Fact]
    public async Task The_client_approves_and_the_actor_is_the_client_user_and_the_contract_value_does_not_move()
    {
        Guid id = await CreateChangeOrderAsync(_projectA);
        await DriveAsync(_projectA, id, ChangeOrderStatus.ClientApproval);

        HttpResponseMessage approved = await SendAsync(HttpMethod.Post, ApproveRoute(_projectA.Id, id), _portalA, null);

        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(Ct));
        (await ReadAsync(approved)).GetProperty("status").GetString().Should().Be("OwnerApproval");

        await using KaffDbContext reader = _database.CreateBareContext();
        ChangeOrderGateRecord gate = await reader.ChangeOrderGateRecords
            .SingleAsync(g => g.ChangeOrderId == id && g.Gate == ChangeOrderGate.ClientApproval, Ct);
        gate.ActorUserId.Should().Be(_portalA);
        gate.Outcome.Should().Be(ChangeOrderGateOutcome.Passed);

        JsonElement home = await ReadAsync(await SendAsync(HttpMethod.Get, $"/api/portal/projects/{_projectA.Id}", _portalA, null));
        home.GetProperty("contractValue").GetString().Should().Be("1000000.00", "the Owner has not approved yet");

        (await reader.Set<AuditRecord>().CountAsync(r => r.EntityId == id && r.ActorUserId == _portalA, Ct))
            .Should().BeGreaterThan(0, "the client's decision is audited with the client as actor");

        (await SendAsync(HttpMethod.Post, ApproveRoute(_projectA.Id, id), _portalA, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the second approval finds it past his gate");
    }

    [Fact]
    public async Task A_rejection_needs_a_reason_and_ends_the_change_order_with_the_reason_stored()
    {
        Guid id = await CreateChangeOrderAsync(_projectA);
        await DriveAsync(_projectA, id, ChangeOrderStatus.ClientApproval);

        HttpResponseMessage without = await SendAsync(HttpMethod.Post, RejectRoute(_projectA.Id, id), _portalA, new { reason = "  " });
        without.IsSuccessStatusCode.Should().BeFalse();
        (await StatusAsync(id)).Should().Be(ChangeOrderStatus.ClientApproval);

        HttpResponseMessage rejected = await SendAsync(HttpMethod.Post, RejectRoute(_projectA.Id, id), _portalA, new { reason = "Too dear" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync(Ct));
        (await ReadAsync(rejected)).GetProperty("status").GetString().Should().Be("Rejected");

        await using KaffDbContext reader = _database.CreateBareContext();
        ChangeOrderGateRecord gate = await reader.ChangeOrderGateRecords
            .SingleAsync(g => g.ChangeOrderId == id && g.Outcome == ChangeOrderGateOutcome.Rejected, Ct);
        gate.Reason.Should().Be("Too dear");
        gate.ActorUserId.Should().Be(_portalA);
    }

    [Fact]
    public async Task A_client_cannot_decide_a_change_order_that_is_not_at_his_gate_or_not_his_projects()
    {
        Guid draft = await CreateChangeOrderAsync(_projectA);
        (await SendAsync(HttpMethod.Post, ApproveRoute(_projectA.Id, draft), _portalA, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "a draft does not exist as far as the client can tell");
        (await StatusAsync(draft)).Should().Be(ChangeOrderStatus.Draft);

        Guid atGate = await CreateChangeOrderAsync(_projectA);
        await DriveAsync(_projectA, atGate, ChangeOrderStatus.ClientApproval);

        (await SendAsync(HttpMethod.Post, ApproveRoute(_projectA.Id, atGate), _portalB, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, ApproveRoute(_projectB.Id, atGate), _portalB, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "that change order is not on his project");
        (await StatusAsync(atGate)).Should().Be(ChangeOrderStatus.ClientApproval);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private static string ApproveRoute(Guid project, Guid id) => $"/api/portal/projects/{project}/change-orders/{id}/approve";

    private static string RejectRoute(Guid project, Guid id) => $"/api/portal/projects/{project}/change-orders/{id}/reject";

    private static (HttpMethod Method, string Route, object? Body)[] ProjectRoutes(Guid project, Guid photo, Guid order) =>
    [
        (HttpMethod.Get, $"/api/portal/projects/{project}", null),
        (HttpMethod.Get, $"/api/portal/projects/{project}/extracts", null),
        (HttpMethod.Get, $"/api/portal/projects/{project}/photos", null),
        (HttpMethod.Get, $"/api/portal/projects/{project}/photos/{photo}", null),
        (HttpMethod.Get, $"/api/portal/projects/{project}/change-orders", null),
        (HttpMethod.Get, $"/api/portal/projects/{project}/change-orders/{order}", null),
        (HttpMethod.Post, ApproveRoute(project, order), null),
        (HttpMethod.Post, RejectRoute(project, order), new { reason = "no" }),
    ];

    [GeneratedRegex("\"(?:contractValue|paid|remaining|total|netPayable|certifiedWork|unitPrice)\":\"(-?[0-9.]+)\"")]
    private static partial Regex Moneys();

    private async Task<ChangeOrderStatus> StatusAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.ChangeOrders.Where(o => o.Id == id).Select(o => o.Status).SingleAsync(Ct);
    }

    private async Task<Guid> CreateChangeOrderAsync(Project project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/change-orders", _office1, new
        {
            title = "Extra partitions",
            lines = new[] { new { description = "Partition wall", unit = "m2", quantity = 10m, unitPrice = "10000" } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Moves a change order forward to <paramref name="target"/> with a different actor at each staff gate.</summary>
    private async Task DriveAsync(Project project, Guid id, ChangeOrderStatus target)
    {
        ChangeOrderStatus current = await StatusAsync(id);
        (ChangeOrderStatus Reaches, string Route, Guid Actor)[] chain =
        [
            (ChangeOrderStatus.TechnicalReview, "submit", _office1),
            (ChangeOrderStatus.ClientApproval, "technical-review", _office2),
        ];

        foreach ((ChangeOrderStatus reaches, string route, Guid actor) in chain)
        {
            if (reaches <= current || reaches > target)
            {
                continue;
            }

            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/change-orders/{id}/{route}", actor, null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");
        }
    }

    private async Task<Guid> PrepareExtractAsync(Project project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/extracts", _prep, new
        {
            periodStart = "2026-09-01",
            periodEnd = "2026-09-30",
            materialValueOnSite = "100000",
            materialInstalled = "0",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task StepExtractAsync(Project project, Guid extract)
    {
        (string Route, Guid Actor, object? Body)[] chain =
        [
            ("qc-report", _supervisor, new { materialValueOnSite = "100000", materialInstalled = "0" }),
            ("technical-approval", _office1, new { }),
            ("deductions", _finance, null),
            ("owner-approval", _owner, null),
            ("issue", _owner, null),
        ];

        foreach ((string route, Guid actor, object? body) in chain)
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/extracts/{extract}/{route}", actor, body);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");
        }
    }

    private async Task<string> SeedPhotoAsync(Project project)
    {
        string reference = Guid.NewGuid().ToString();
        await using KaffDbContext context = _database.CreateContext();
        DailyLog log = DailyLog.Open(project.Id, _supervisor, Now, Now, DailyLogEntryRule.Default);
        Result<PhotoEntry> photo = log.RecordPhoto(Guid.NewGuid(), reference, "Slab poured", DailyLogEntryRule.Default, Now, Now);
        photo.IsSuccess.Should().BeTrue();
        context.DailyLogs.Add(log);
        context.PhotoEntries.Add(photo.Value);
        await context.SaveChangesAsync(Ct);
        return reference;
    }

    private async Task<Guid> NewPortalUserAsync(Guid clientId, bool mustChangePassword)
    {
        await using KaffDbContext context = _database.CreateContext();
        User user = User.Create(UniqueNames.Code("pt-temp"), "عميل مؤقت", UniqueNames.Phone(), Role.Client, Now, clientId: clientId).Value;
        if (mustChangePassword)
        {
            user.SetTemporaryPassword(Kaff.Infrastructure.Identity.PasswordHasher.Hash("a-temporary-one")).IsSuccess.Should().BeTrue();
        }

        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<Guid> NewStaffAsync(string name, Role role)
    {
        await using KaffDbContext context = _database.CreateContext();
        User user = Make(name, role, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        JsonElement body = await ReadAsync(response);
        return body.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
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

    private async Task ExecuteAsync(Project project, decimal cumulativeQuantity)
    {
        await using KaffDbContext context = _database.CreateContext();
        BoqLine line = await context.Set<BoqLine>().SingleAsync(candidate => candidate.ProjectId == project.Id, Ct);
        line.ApplyExecutedChange(cumulativeQuantity - line.QuantityExecuted).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);
    }

    private async Task<Project> NewProjectAsync(Guid clientId, string? plannedStart = null, string? plannedEnd = null)
    {
        Project project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            project = Project.Create(UniqueNames.Code("PT-P"), UniqueNames.Code("مشروع البوابة"), clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
            project.SetLumpSumTerms(
                Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
                Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
            context.Projects.Add(project);
            await context.SaveChangesAsync(Ct);

            Bab bab = await context.Babs.SingleAsync(candidate => candidate.Id == _babId, Ct);
            Boq boq = Boq.Open(project, Now).Value;
            boq.AddCustomLine(
                null, "خرسانة", "Concrete", "m3", bab, new Money(800m), new Money(1_000m), 1_000m, 1_000m, null, null)
                .IsSuccess.Should().BeTrue();
            context.Boqs.Add(boq);

            foreach ((Guid userId, AssignmentLevel level) in new[]
            {
                (_finance, AssignmentLevel.Standard),
                (_office1, AssignmentLevel.Standard),
                (_office2, AssignmentLevel.Standard),
                (_office3, AssignmentLevel.Standard),
                (_prep, AssignmentLevel.Junior),
                (_supervisor, AssignmentLevel.Supervisor),
            })
            {
                User user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Ct);
                context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, level, _owner, Now).Value);
            }

            IReadOnlyList<Account> accounts = ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), new DateOnly(2026, 9, 24)).Value;
            context.Accounts.AddRange(accounts);
            await context.SaveChangesAsync(Ct);
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/sign", _owner, new { signedOn = "2026-09-24", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25", plannedStart, plannedEnd }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext activate = _database.CreateContext())
        {
            Account advance = await activate.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.ClientAdvance, Ct);
            Account safe = await activate.Accounts.SingleAsync(a => a.Id == _safeId, Ct);
            activate.Postings.Add(Posting.Create(
                advance, safe, new Money(250_000m), PostingType.ClientAdvanceReceipt,
                new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "SIGNING"),
                new DateOnly(2026, 9, 24), _owner, Now, project.Id).Value);
            await activate.SaveChangesAsync(Ct);
        }

        await ExecuteAsync(project, 300m);
        return project;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client a = Client.Create(UniqueNames.Code("PT-CA"), "عميل البوابة الأول", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Client b = Client.Create(UniqueNames.Code("PT-CB"), "عميل البوابة الثاني", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Bab bab = Bab.Create(UniqueNames.Code("PTB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(0m)).Value;
        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("PTSAFE"), "خزنة", "Safe", Currency.Egp, new DateOnly(2026, 1, 1)).Value;

        User owner = Make("pt-owner", Role.Owner);
        User finance = Make("pt-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User office1 = Make("pt-to1", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User office2 = Make("pt-to2", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User office3 = Make("pt-to3", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User prep = Make("pt-prep", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("pt-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = Make("pt-hr", Role.Hr, WellKnownDepartments.HrId);
        User portalA = User.Create(UniqueNames.Code("pt-client-a"), "عميل أ", UniqueNames.Phone(), Role.Client, Now, clientId: a.Id).Value;
        User portalB = User.Create(UniqueNames.Code("pt-client-b"), "عميل ب", UniqueNames.Phone(), Role.Client, Now, clientId: b.Id).Value;

        context.Clients.AddRange(a, b);
        context.Babs.Add(bab);
        context.Accounts.Add(safe);
        context.Users.AddRange(owner, finance, office1, office2, office3, prep, supervisor, hr, portalA, portalB);
        await context.SaveChangesAsync(Ct);

        _clientA = a.Id;
        _clientB = b.Id;
        _clientBName = b.Name;
        _babId = bab.Id;
        _safeId = safe.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _office1 = office1.Id;
        _office2 = office2.Id;
        _office3 = office3.Id;
        _prep = prep.Id;
        _supervisor = supervisor.Id;
        _hr = hr.Id;
        _portalA = portalA.Id;
        _portalB = portalB.Id;
    }

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;
}
