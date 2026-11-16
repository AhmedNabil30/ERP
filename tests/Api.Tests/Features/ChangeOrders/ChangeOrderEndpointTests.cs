using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Features.ChangeOrders;

/// <summary>
/// KAFF-508 and KAFF-509 over HTTP. Permissions first (one assertion per role about what it cannot reach, hitting the
/// endpoint directly), then the chain, the derived contract value, the stored rejection reason, and the billing of an
/// approved change order on an extract.
/// </summary>
/// <remarks>
/// Every project here is its own: a signed Lump Sum project whose frozen BOQ prices 1,000 units at 1,000 each
/// (contract value 1,000,000), 300 executed, a 250,000 client advance and a 100,000 change order of 10 units at
/// 10,000. The chain needs three Technical Office users and the Owner, because nobody who acted earlier may act at a
/// later gate (ruling ChangeOrderApprovers).
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ChangeOrderEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _babId;
    private Guid _safeId;
    private Guid _owner;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _technicalOffice2;
    private Guid _technicalOffice3;
    private Guid _technicalOfficeUnassigned;
    private Guid _prepEngineer;
    private Guid _supervisor;
    private Guid _hr;

    public ChangeOrderEndpointTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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

    // ---- permissions: role x assignment, hitting every route directly -----------------------------

    [Fact]
    public void Each_change_order_route_requires_the_permission_the_catalogue_names_and_the_route_project()
    {
        Dictionary<string, Permission> expected = new()
        {
            ["POST /api/projects/{projectId:guid}/change-orders"] = Permission.ChangeOrderPrepare,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/submit"] = Permission.ChangeOrderPrepare,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/withdrawal"] = Permission.ChangeOrderPrepare,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/log"] = Permission.ChangeOrderPrepare,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/technical-review"] = Permission.ChangeOrderReview,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/technical-rejection"] = Permission.ChangeOrderReview,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/client-approval"] = Permission.ChangeOrderClientApprovalRecord,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/client-rejection"] = Permission.ChangeOrderClientApprovalRecord,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/owner-approval"] = Permission.ChangeOrderApprove,
            ["POST /api/projects/{projectId:guid}/change-orders/{id:guid}/owner-rejection"] = Permission.ChangeOrderApprove,
            ["GET /api/projects/{projectId:guid}/change-orders"] = Permission.ProjectRead,
            ["GET /api/projects/{projectId:guid}/change-orders/{id:guid}"] = Permission.ProjectRead,
        };

        Dictionary<string, PermissionRequirement> actual = [];
        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (RouteEndpoint route in source.Endpoints.OfType<RouteEndpoint>())
            {
                string? pattern = route.RoutePattern.RawText;
                if (pattern is null || !pattern.Contains("/change-orders", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string method in route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
                {
                    foreach (IAuthorizeData data in route.Metadata.OfType<IAuthorizeData>())
                    {
                        if (PermissionRequirement.TryParse(data.Policy ?? string.Empty, out PermissionRequirement? requirement)
                            && requirement is not null)
                        {
                            actual[$"{method} {pattern}"] = requirement;
                        }
                    }
                }
            }
        }

        actual.Keys.Should().BeEquivalentTo(expected.Keys, "every change-order route is gated, and no other exists");
        foreach ((string key, Permission permission) in expected)
        {
            actual[key].Permission.Should().Be(permission, key);
            actual[key].Scope.Should().Be(ProjectScope.FromRoute(), $"{key} checks the assignment to the route's project");
        }
    }

    [Fact]
    public async Task Creating_is_for_an_assigned_technical_office_user_and_nobody_else()
    {
        Project project = await NewProjectAsync();
        string route = ChangeOrders(project.Id);

        foreach (Guid refused in new[] { _owner, _finance, _prepEngineer, _supervisor, _hr, _technicalOfficeUnassigned })
        {
            (await SendAsync(HttpMethod.Post, route, refused, PricedBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _technicalOffice, PricedBody())).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Each_gate_is_held_by_its_own_permission_and_a_role_without_assignment_is_refused()
    {
        Project project = await NewProjectAsync();

        (string Step, ChangeOrderStatus At, Guid Holder)[] gates =
        [
            ("submit", ChangeOrderStatus.Draft, _technicalOffice),
            ("technical-review", ChangeOrderStatus.TechnicalReview, _technicalOffice2),
            ("client-approval", ChangeOrderStatus.ClientApproval, _technicalOffice3),
            ("owner-approval", ChangeOrderStatus.OwnerApproval, _owner),
        ];

        foreach ((string step, ChangeOrderStatus at, Guid holder) in gates)
        {
            Guid id = await DriveAsync(project, at);
            string route = Step(project.Id, id, step);

            foreach (Guid refused in new[] { _finance, _prepEngineer, _supervisor, _hr, _technicalOfficeUnassigned }
                .Concat(holder == _owner ? [_technicalOffice, _technicalOffice2, _technicalOffice3] : [_owner]))
            {
                (await SendAsync(HttpMethod.Post, route, refused, null)).StatusCode
                    .Should().Be(HttpStatusCode.Forbidden, $"{step} must refuse a caller who lacks the permission or the assignment");
            }

            (await StatusOfAsync(id)).Should().Be(at, "a refused call moves nothing");
            (await SendAsync(HttpMethod.Post, route, holder, null)).StatusCode.Should().Be(HttpStatusCode.OK, step);
        }
    }

    /// <summary>AC-509-E. Finance and the Technical Office both hold no right to the Owner approval, hitting it directly.</summary>
    [Fact]
    public async Task Finance_and_the_technical_office_cannot_give_the_owner_approval()
    {
        Project project = await NewProjectAsync();
        Guid id = await DriveAsync(project, ChangeOrderStatus.OwnerApproval);
        long before = await PostingCountAsync(project.Id);

        foreach (Guid refused in new[] { _finance, _technicalOffice, _technicalOffice2, _technicalOffice3 })
        {
            (await SendAsync(HttpMethod.Post, Step(project.Id, id, "owner-approval"), refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        (await StatusOfAsync(id)).Should().Be(ChangeOrderStatus.OwnerApproval);
        (await PostingCountAsync(project.Id)).Should().Be(before);
    }

    [Fact]
    public async Task Reading_needs_project_read_and_an_assignment()
    {
        Project project = await NewProjectAsync();
        Guid id = await CreateAsync(project);

        (await SendAsync(HttpMethod.Get, ChangeOrders(project.Id), _technicalOfficeUnassigned, null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, Single(project.Id, id), _technicalOfficeUnassigned, null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, Single(project.Id, id), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_site_engineer_reads_a_change_order_without_any_money()
    {
        Project project = await NewProjectAsync();
        Guid id = await CreateAsync(project);

        JsonElement asEngineer = await ReadAsync(project.Id, id, _prepEngineer);
        asEngineer.GetProperty("lines").ValueKind.Should().Be(JsonValueKind.Null);
        asEngineer.GetProperty("total").ValueKind.Should().Be(JsonValueKind.Null);

        JsonElement asOffice = await ReadAsync(project.Id, id, _technicalOffice);
        ParseDecimal(asOffice.GetProperty("total")).Should().Be(100_000m);
        asOffice.GetProperty("lines").GetArrayLength().Should().Be(1);
    }

    // ---- the chain and the derived contract value (AC-509-C, AC-509-J) -------------------------------

    [Fact]
    public async Task An_approved_change_order_raises_the_contract_value_and_leaves_the_signed_boq_exactly_as_signed()
    {
        Project project = await NewProjectAsync();
        BoqSnapshot signed = await BoqOfAsync(project.Id);

        JsonElement before = await ProjectAsync(project.Id);
        ParseDecimal(before.GetProperty("contractValue")).Should().Be(1_000_000m);

        Guid id = await DriveAsync(project, ChangeOrderStatus.Approved);

        JsonElement after = await ProjectAsync(project.Id);
        ParseDecimal(after.GetProperty("contractValue")).Should().Be(1_100_000m, "the revised value leads (ChangeOrderDisplayedValue)");
        ParseDecimal(after.GetProperty("originalContractValue")).Should().Be(1_000_000m);
        ParseDecimal(after.GetProperty("approvedChangeOrderTotal")).Should().Be(100_000m);

        (await BoqOfAsync(project.Id)).Should().Be(signed, "a change order never reaches a signed BOQ line");

        await using KaffDbContext reader = _database.CreateBareContext();
        Project stored = await reader.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct);
        stored.ContractValue.Should().Be(new Money(1_000_000m), "the signed value is not edited: the revised one is derived");

        JsonElement read = await ReadAsync(project.Id, id, _owner);
        read.GetProperty("status").GetString().Should().Be("Approved");
        read.GetProperty("approvedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task A_change_order_that_is_not_approved_does_not_move_the_contract_value()
    {
        Project project = await NewProjectAsync();

        foreach (ChangeOrderStatus at in new[] { ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.ClientApproval, ChangeOrderStatus.OwnerApproval })
        {
            await DriveAsync(project, at);
        }

        ParseDecimal((await ProjectAsync(project.Id)).GetProperty("contractValue")).Should().Be(1_000_000m);
    }

    [Fact]
    public async Task The_project_list_shows_the_same_revised_value()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ChangeOrderStatus.Approved);

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/projects", _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement row = document.RootElement.GetProperty("projects").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == project.Id);

        ParseDecimal(row.GetProperty("contractValue")).Should().Be(1_100_000m);
        ParseDecimal(row.GetProperty("originalContractValue")).Should().Be(1_000_000m);
    }

    [Fact]
    public async Task An_illegal_step_is_refused_with_the_state_machine_error_and_moves_nothing()
    {
        Project project = await NewProjectAsync();
        Guid id = await CreateAsync(project);

        HttpResponseMessage skipped = await SendAsync(HttpMethod.Post, Step(project.Id, id, "client-approval"), _technicalOffice3, null);
        skipped.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(skipped)).Should().Be("change_order.illegal_transition");

        (await SendAsync(HttpMethod.Post, Step(project.Id, id, "owner-approval"), _owner, null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StatusOfAsync(id)).Should().Be(ChangeOrderStatus.Draft);
    }

    [Fact]
    public async Task A_change_order_is_refused_on_a_project_that_is_not_active()
    {
        Project project = await NewProjectAsync();
        await SetStatusAsync(project.Id, ProjectStatus.Stopped);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, ChangeOrders(project.Id), _technicalOffice, PricedBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("change_order.project_state_not_allowed");
    }

    // ---- separation of duties (AC-509-F) --------------------------------------------------------------

    [Fact]
    public async Task The_creator_is_refused_at_a_later_gate_and_the_read_model_leaves_the_control_out()
    {
        Project project = await NewProjectAsync();
        Guid id = await DriveAsync(project, ChangeOrderStatus.TechnicalReview);

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Step(project.Id, id, "technical-review"), _technicalOffice, null);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(refused)).Should().Be(AuthorizationErrors.SameActorCreatedAndApproved.Code);
        (await StatusOfAsync(id)).Should().Be(ChangeOrderStatus.TechnicalReview);

        (await ActionsAsync(project.Id, id, _technicalOffice))["approveTechnicalReview"].Should().BeFalse("the control is absent, not disabled");
        (await ActionsAsync(project.Id, id, _technicalOffice))["callerMayDecide"].Should().BeFalse();
        (await ActionsAsync(project.Id, id, _technicalOffice2))["approveTechnicalReview"].Should().BeTrue();
        (await ActionsAsync(project.Id, id, _technicalOffice2))["rejectAtTechnicalReview"].Should().BeTrue();
        (await ActionsAsync(project.Id, id, _owner))["approveAsOwner"].Should().BeFalse("it is not at the Owner's gate");
    }

    // ---- rejection with a stored reason (AC-509-G) -----------------------------------------------------

    [Fact]
    public async Task A_rejection_needs_a_reason_at_every_gate_and_the_reason_is_stored_and_audited()
    {
        Project project = await NewProjectAsync();

        (string Route, ChangeOrderStatus At, Guid Rejecter)[] gates =
        [
            ("technical-rejection", ChangeOrderStatus.TechnicalReview, _technicalOffice2),
            ("client-rejection", ChangeOrderStatus.ClientApproval, _technicalOffice3),
            ("owner-rejection", ChangeOrderStatus.OwnerApproval, _owner),
        ];

        foreach ((string route, ChangeOrderStatus at, Guid rejecter) in gates)
        {
            Guid id = await DriveAsync(project, at);

            foreach (object blank in new object[] { new { reason = "  " }, new { reason = "" }, new { } })
            {
                HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Step(project.Id, id, route), rejecter, blank);
                refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                (await ErrorCodeAsync(refused)).Should().Be("change_order.reason_required");
            }

            (await StatusOfAsync(id)).Should().Be(at, "a refused rejection moves nothing");

            HttpResponseMessage accepted = await SendAsync(
                HttpMethod.Post, Step(project.Id, id, route), rejecter, new { reason = "price not agreed" });
            accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync(Ct));

            await using KaffDbContext reader = _database.CreateBareContext();
            (await StatusOfAsync(id)).Should().Be(ChangeOrderStatus.Rejected);
            ChangeOrderGateRecord record = await reader.Set<ChangeOrderGateRecord>()
                .SingleAsync(candidate => candidate.ChangeOrderId == id && candidate.Outcome == ChangeOrderGateOutcome.Rejected, Ct);
            record.Reason.Should().Be("price not agreed");
            record.ActorUserId.Should().Be(rejecter);
            (await reader.AuditRecords.AnyAsync(
                audit => audit.EntityType == nameof(ChangeOrderGateRecord) && audit.EntityId == record.Id, Ct))
                .Should().BeTrue("a rejection writes audit: who, when and why");
            (await reader.AuditRecords.AnyAsync(
                audit => audit.EntityType == nameof(ChangeOrder) && audit.EntityId == id, Ct))
                .Should().BeTrue("the state change is audited");
        }
    }

    [Fact]
    public async Task A_rejection_route_refuses_a_change_order_that_is_not_waiting_at_its_gate()
    {
        Project project = await NewProjectAsync();
        Guid id = await DriveAsync(project, ChangeOrderStatus.TechnicalReview);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, Step(project.Id, id, "owner-rejection"), _owner, new { reason = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("change_order.not_waiting_at_this_gate");
        (await StatusOfAsync(id)).Should().Be(ChangeOrderStatus.TechnicalReview);
    }

    // ---- Cost Plus and Design only log (AC-509-H) ---------------------------------------------------------

    [Fact]
    public async Task A_cost_plus_change_order_only_logs_carries_no_price_and_posts_nothing()
    {
        Project project = await NewCostPlusProjectAsync();
        long before = await PostingCountAsync(project.Id);

        HttpResponseMessage priced = await SendAsync(HttpMethod.Post, ChangeOrders(project.Id), _technicalOffice, PricedBody());
        priced.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(priced)).Should().Be("change_order.lines_not_allowed");

        HttpResponseMessage created = await SendAsync(HttpMethod.Post, ChangeOrders(project.Id), _technicalOffice, new { title = "Extra scope" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();
        body.RootElement.GetProperty("isPriced").GetBoolean().Should().BeFalse();

        (await SendAsync(HttpMethod.Post, Step(project.Id, id, "submit"), _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, Step(project.Id, id, "log"), _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(id)).Should().Be(ChangeOrderStatus.Logged);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Set<ChangeOrderLine>().CountAsync(line => line.ChangeOrderId == id, Ct)).Should().Be(0);
        (await PostingCountAsync(project.Id)).Should().Be(before);
        (await reader.Postings.AnyAsync(posting => posting.SourceDocumentType == SourceDocumentType.ChangeOrder && posting.ProjectId == project.Id, Ct))
            .Should().BeFalse();
    }

    // ---- billing on the extract (KAFF-508) -----------------------------------------------------------------

    /// <summary>
    /// AC-508-A, AC-508-C, AC-508-F. 300,000 of BOQ work and the 100,000 change order, on the default Q102
    /// ruling: the change order is its own row at its own price, the hold is 20% of 400,000, the advance recovery is
    /// 25% of 400,000 (still within the 250,000 advance), and the printed column foots.
    /// </summary>
    [Fact]
    public async Task An_approved_change_order_is_billed_in_its_own_section_and_the_figures_foot()
    {
        Project project = await NewProjectAsync();
        Guid changeOrder = await DriveAsync(project, ChangeOrderStatus.Approved);

        Guid extract = await PrepareAsync(project);
        JsonElement read = await ExtractAsync(project.Id, extract, _finance);
        JsonElement section = read.GetProperty("changeOrders").EnumerateArray().Single();
        section.GetProperty("changeOrderId").GetGuid().Should().Be(changeOrder);
        ParseDecimal(section.GetProperty("amount")).Should().Be(100_000m);
        (await ExtractAsync(project.Id, extract, _prepEngineer)).GetProperty("changeOrders").ValueKind
            .Should().Be(JsonValueKind.Null, "a site engineer sees no money");

        await StepAsync(project, extract, ("qc-report", _supervisor), ("technical-approval", _technicalOffice), ("deductions", _finance));
        JsonElement document = (await ExtractAsync(project.Id, extract, _finance)).GetProperty("document");

        JsonElement figures = document.GetProperty("figures");
        Decimal(figures, "workValue").Should().Be(300_000m, "no original BOQ line carries the change order");
        Decimal(figures, "changeOrderValue").Should().Be(100_000m);
        Decimal(figures, "holdThisPeriod").Should().Be(80_000m);
        Decimal(figures, "advanceRecovered").Should().Be(100_000m);
        Decimal(figures, "netPayable").Should().Be(295_000m, "400,000 less 80,000 less 100,000 plus the 75,000 material advance");

        document.GetProperty("column").EnumerateArray().Select(row => row.GetProperty("key").GetString())
            .Should().Contain(["workValue", "changeOrderValue"]);
        document.GetProperty("foots").GetBoolean().Should().BeTrue("the visible column foots to the printed total");
        ParseDecimal(document.GetProperty("columnSum")).Should().Be(295_000m);
    }

    /// <summary>
    /// The stage each posting is written at, not only the amount: nothing is posted at deductions, the revenue posting
    /// (work plus change order) and the hold appear when the Owner approves, and the proof read agrees column by column.
    /// </summary>
    [Fact]
    public async Task The_change_order_value_posts_at_the_ruled_stage_and_the_proof_agrees()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ChangeOrderStatus.Approved);
        Guid extract = await PrepareAsync(project);
        long start = await PostingCountAsync(project.Id);

        await StepAsync(project, extract, ("qc-report", _supervisor), ("technical-approval", _technicalOffice), ("deductions", _finance));
        (await PostingCountAsync(project.Id)).Should().Be(start, "the default accrual moment is the Owner's approval: deductions post nothing");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(0m);

        await StepAsync(project, extract, ("owner-approval", _owner));
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(80_000m);

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            List<Posting> posted = await reader.Postings
                .Where(posting => posting.SourceDocumentId == extract && posting.SourceDocumentType == SourceDocumentType.Extract)
                .ToListAsync(Ct);
            posted.Single(posting => posting.Type == PostingType.RevenueRecognition).Amount.Should().Be(new Money(400_000m));
            posted.Single(posting => posting.Type == PostingType.HoldAccrual).Amount.Should().Be(new Money(80_000m));
            posted.Should().OnlyContain(posting => posting.ReversesId == null);
        }

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/projects/{project.Id}/treasury/proof", _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument proof = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement item = proof.RootElement.GetProperty("events").EnumerateArray()
            .Single(candidate => candidate.GetProperty("sourceDocumentId").GetGuid() == extract);
        string[] columns = ["work", "hold", "advance", "materialAdvance", "collection"];
        columns.Select(column => ParseDecimal(item.GetProperty("expected").GetProperty(column)))
            .Should().Equal(400_000m, -80_000m, -100_000m, 75_000m, 0m);
        columns.Select(column => ParseDecimal(item.GetProperty("difference").GetProperty(column)))
            .Should().Equal(0m, 0m, 0m, 0m, 0m);
    }

    /// <summary>AC-508-B. A change order in any state before Approved is never offered and cannot be added.</summary>
    [Fact]
    public async Task A_change_order_before_approval_is_never_billed()
    {
        Project project = await NewProjectAsync();
        foreach (ChangeOrderStatus at in new[]
        {
            ChangeOrderStatus.Draft, ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.ClientApproval,
            ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Rejected, ChangeOrderStatus.Withdrawn,
        })
        {
            await DriveAsync(project, at);
        }

        Guid extract = await PrepareAsync(project);

        (await ExtractAsync(project.Id, extract, _finance)).GetProperty("changeOrders").GetArrayLength().Should().Be(0);
    }

    /// <summary>AC-508-D. Billed once: the next extract offers nothing, and the database refuses a second billing.</summary>
    [Fact]
    public async Task An_approved_change_order_is_billed_on_one_extract_only()
    {
        Project project = await NewProjectAsync();
        Guid changeOrder = await DriveAsync(project, ChangeOrderStatus.Approved);
        Guid first = await PrepareAsync(project);
        await StepAsync(
            project, first, ("qc-report", _supervisor), ("technical-approval", _technicalOffice), ("deductions", _finance),
            ("owner-approval", _owner), ("issue", _owner));

        await ExecuteAsync(project, 500m);
        Guid second = await PrepareAsync(project);

        (await ExtractAsync(project.Id, second, _finance)).GetProperty("changeOrders").GetArrayLength().Should().Be(0);

        await using KaffDbContext context = _database.CreateContext();
        Extract other = await context.Extracts.SingleAsync(candidate => candidate.Id == second, Ct);
        context.Set<ExtractChangeOrderLine>().Add(
            (ExtractChangeOrderLine)Activator.CreateInstance(
                typeof(ExtractChangeOrderLine), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, [other.Id, changeOrder, new Money(100_000m)], null)!);
        Func<Task> second_billing = () => context.SaveChangesAsync(Ct);
        await second_billing.Should().ThrowAsync<DbUpdateException>("the unique index refuses a second billing of one change order");
    }

    /// <summary>
    /// The repricing ruling, proved over HTTP: approving a change order after an extract is issued changes nothing
    /// on that extract, writes no posting, and leaves the hold exactly where it was.
    /// </summary>
    [Fact]
    public async Task Approving_a_change_order_after_an_extract_is_issued_does_not_reprice_it()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);
        await StepAsync(
            project, extract, ("qc-report", _supervisor), ("technical-approval", _technicalOffice), ("deductions", _finance),
            ("owner-approval", _owner), ("issue", _owner));

        string before = (await ExtractAsync(project.Id, extract, _finance)).GetProperty("document").GetRawText();
        long postings = await PostingCountAsync(project.Id);
        decimal hold = await BalanceAsync(project.Id, AccountType.Hold);
        decimal advance = await BalanceAsync(project.Id, AccountType.ClientAdvance);
        hold.Should().Be(60_000m);

        await DriveAsync(project, ChangeOrderStatus.Approved);

        (await ExtractAsync(project.Id, extract, _finance)).GetProperty("document").GetRawText().Should().Be(before);
        (await ExtractAsync(project.Id, extract, _finance)).GetProperty("changeOrders").GetArrayLength().Should().Be(0);
        (await PostingCountAsync(project.Id)).Should().Be(postings, "an approval writes no posting");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(hold, "the hold only grows and a change order never touches it");
        (await BalanceAsync(project.Id, AccountType.ClientAdvance)).Should().Be(advance);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------

    private static string ChangeOrders(Guid project) => $"/api/projects/{project}/change-orders";

    private static string Single(Guid project, Guid id) => $"/api/projects/{project}/change-orders/{id}";

    private static string Step(Guid project, Guid id, string step) => $"/api/projects/{project}/change-orders/{id}/{step}";

    private static string ExtractStep(Guid project, Guid extract, string step) => $"/api/projects/{project}/extracts/{extract}/{step}";

    private static object PricedBody() => new
    {
        title = "Extra partitions",
        lines = new[] { new { description = "Partition wall", unit = "m2", quantity = 10m, unitPrice = "10000" } },
    };

    private static decimal ParseDecimal(JsonElement element) =>
        decimal.Parse(element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText(), CultureInfo.InvariantCulture);

    private static decimal Decimal(JsonElement figures, string key) =>
        ParseDecimal(figures.EnumerateArray().Single(figure => figure.GetProperty("key").GetString() == key).GetProperty("amount"));

    private async Task<Guid> CreateAsync(Project project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, ChangeOrders(project.Id), _technicalOffice, PricedBody());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Drives a fresh priced change order to <paramref name="target"/> with a different actor at each gate.</summary>
    private async Task<Guid> DriveAsync(Project project, ChangeOrderStatus target)
    {
        Guid id = await CreateAsync(project);
        if (target == ChangeOrderStatus.Draft)
        {
            return id;
        }

        if (target == ChangeOrderStatus.Withdrawn)
        {
            (await SendAsync(HttpMethod.Post, Step(project.Id, id, "withdrawal"), _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
            return id;
        }

        (ChangeOrderStatus Reaches, string Route, Guid Actor, ChangeOrderStatus RejectsAt, string RejectRoute)[] chain =
        [
            (ChangeOrderStatus.TechnicalReview, "submit", _technicalOffice, ChangeOrderStatus.Draft, string.Empty),
            (ChangeOrderStatus.ClientApproval, "technical-review", _technicalOffice2, ChangeOrderStatus.TechnicalReview, "technical-rejection"),
            (ChangeOrderStatus.OwnerApproval, "client-approval", _technicalOffice3, ChangeOrderStatus.ClientApproval, "client-rejection"),
            (ChangeOrderStatus.Approved, "owner-approval", _owner, ChangeOrderStatus.OwnerApproval, "owner-rejection"),
        ];

        foreach ((ChangeOrderStatus reaches, string route, Guid actor, _, _) in chain)
        {
            if (target == ChangeOrderStatus.Rejected && reaches == ChangeOrderStatus.ClientApproval)
            {
                (await SendAsync(HttpMethod.Post, Step(project.Id, id, "technical-rejection"), _technicalOffice2, new { reason = "no" }))
                    .StatusCode.Should().Be(HttpStatusCode.OK);
                return id;
            }

            HttpResponseMessage response = await SendAsync(HttpMethod.Post, Step(project.Id, id, route), actor, null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");

            if (reaches == target)
            {
                return id;
            }
        }

        return id;
    }

    private async Task<Guid> PrepareAsync(Project project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/extracts", _prepEngineer, new
        {
            periodStart = "2026-09-01",
            periodEnd = "2026-09-30",
            materialValueOnSite = "100000",
            materialInstalled = "0",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task StepAsync(Project project, Guid extract, params (string Route, Guid Actor)[] steps)
    {
        foreach ((string route, Guid actor) in steps)
        {
            HttpResponseMessage response = await SendAsync(
                HttpMethod.Post, ExtractStep(project.Id, extract, route), actor, route == "technical-approval" ? new { } : null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");
        }
    }

    private async Task<JsonElement> ExtractAsync(Guid project, Guid extract, Guid actor)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/projects/{project}/extracts/{extract}", actor, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> ReadAsync(Guid project, Guid id, Guid actor)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, Single(project, id), actor, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<Dictionary<string, bool>> ActionsAsync(Guid project, Guid id, Guid actor)
    {
        JsonElement read = await ReadAsync(project, id, actor);
        return read.GetProperty("actions").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetBoolean());
    }

    private async Task<JsonElement> ProjectAsync(Guid project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/projects/{project}", _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
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

    private async Task<ChangeOrderStatus> StatusOfAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.ChangeOrders.Where(candidate => candidate.Id == id).Select(candidate => candidate.Status).SingleAsync(Ct);
    }

    private async Task<long> PostingCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project, Ct);
    }

    private async Task<decimal> BalanceAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return (await reader.AccountBalances.SingleAsync(balance => balance.ProjectId == project && balance.Type == type, Ct)).SignedBalance.Amount;
    }

    private sealed record BoqSnapshot(DateOnly? FrozenOn, string Lines);

    private async Task<BoqSnapshot> BoqOfAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        Boq boq = await reader.Boqs.Include(candidate => candidate.Lines).SingleAsync(candidate => candidate.ProjectId == project, Ct);
        string lines = string.Join(
            "|",
            boq.Lines.OrderBy(line => line.Position).Select(line => string.Create(
                CultureInfo.InvariantCulture,
                $"{line.Position}:{line.DescriptionAr}:{line.SellRate.Amount}:{line.QuantityEstimated}:{line.QuantitySurveyed}")));
        return new BoqSnapshot(boq.FrozenOn, lines);
    }

    private async Task ExecuteAsync(Project project, decimal cumulativeQuantity)
    {
        await using KaffDbContext context = _database.CreateContext();
        BoqLine line = await context.Set<BoqLine>().SingleAsync(candidate => candidate.ProjectId == project.Id, Ct);
        line.ApplyExecutedChange(cumulativeQuantity - line.QuantityExecuted).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);
    }

    private async Task SetStatusAsync(Guid project, ProjectStatus status)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project loaded = await context.Projects.SingleAsync(candidate => candidate.Id == project, Ct);
        loaded.TransitionTo(status, new DateOnly(2026, 9, 25), "test").IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);
    }

    private async Task<Project> NewCostPlusProjectAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("CO-CP"), UniqueNames.Code("مشروع تكلفة"), _clientId, ContractType.CostPlus, DateTimeOffset.UtcNow).Value;
        project.SetCostPlusTerms(Percentage.FromPercent(10m)).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2026, 9, 24), null).IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active, "D-230: signing activates the project");
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        User office = await context.Users.SingleAsync(candidate => candidate.Id == _technicalOffice, Ct);
        context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, office, AssignmentLevel.Standard, _owner, Now).Value);
        await context.SaveChangesAsync(Ct);
        return project;
    }

    private async Task<Project> NewProjectAsync()
    {
        Project project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            project = Project.Create(UniqueNames.Code("CO-P"), UniqueNames.Code("مشروع أمر تغيير"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
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
                (_technicalOffice, AssignmentLevel.Standard),
                (_technicalOffice2, AssignmentLevel.Standard),
                (_technicalOffice3, AssignmentLevel.Standard),
                (_prepEngineer, AssignmentLevel.Junior),
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

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/sign", _owner, new { signedOn = "2026-09-24", holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext activate = _database.CreateContext())
        {
            Project loaded = await activate.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct);
            loaded.Status.Should().Be(ProjectStatus.Active, "D-230: signing activates the project");

            // spec.md section 15: the client's 25% advance comes in at signing.
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

        Client client = Client.Create(
            UniqueNames.Code("CO-C"), "عميل أمر التغيير", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Bab bab = Bab.Create(UniqueNames.Code("COB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(0m)).Value;
        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("COSAFE"), "خزنة", "Safe", Currency.Egp, new DateOnly(2026, 1, 1)).Value;

        User owner = Make("co-owner", Role.Owner);
        User finance = Make("co-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User office = Make("co-to", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User office2 = Make("co-to2", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User office3 = Make("co-to3", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User officeUnassigned = Make("co-to-un", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User prep = Make("co-prep", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("co-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = Make("co-hr", Role.Hr, WellKnownDepartments.HrId);

        context.Clients.Add(client);
        context.Babs.Add(bab);
        context.Accounts.Add(safe);
        context.Users.AddRange(owner, finance, office, office2, office3, officeUnassigned, prep, supervisor, hr);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _babId = bab.Id;
        _safeId = safe.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _technicalOffice = office.Id;
        _technicalOffice2 = office2.Id;
        _technicalOffice3 = office3.Id;
        _technicalOfficeUnassigned = officeUnassigned.Id;
        _prepEngineer = prep.Id;
        _supervisor = supervisor.Id;
        _hr = hr.Id;
    }

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;
}
