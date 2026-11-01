using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// KAFF-500 stage 2: the extract endpoints over HTTP. Permissions first (CLAUDE.md "Testing", priority 2: one
/// assertion per role about what it cannot reach, hitting the endpoint directly), then the chain driven through
/// the routes with spec.md section 15's first extract as the expectation.
/// </summary>
/// <remarks>
/// Every project here is its own: a signed Lump Sum project whose frozen BOQ prices 1,000 units at 1,000 each
/// (contract value 1,000,000), with 300 executed, so the first extract certifies 300,000. Each user is a real row
/// and the gate is the shipped one (<see cref="TestAuthHandler"/> replaces authentication only).
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ExtractEndpointTests : IAsyncLifetime
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
    private Guid _financeUnassigned;
    private Guid _technicalOffice;
    private Guid _technicalOffice2;
    private Guid _prepEngineer;
    private Guid _supervisor;
    private Guid _engineerUnassigned;
    private Guid _hr;

    public ExtractEndpointTests(PostgresDatabase database) => _database = database;

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

    // ---- permissions: role x assignment, hitting every route directly ---------------------------

    [Fact]
    public async Task Prepare_is_for_an_assigned_site_engineer_and_nobody_else()
    {
        Project project = await NewProjectAsync();
        string route = Extracts(project.Id);
        object body = PrepareBody();

        foreach (Guid refused in new[] { _owner, _finance, _technicalOffice, _hr, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, route, refused, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _prepEngineer, body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// The route-to-permission map, read from the endpoints the host actually built (the same source
    /// <c>EndpointPermissionCoverageTests</c> uses), so a route re-pointed at another permission is red. D-189 rules
    /// these gates; the Owner reuses <c>FinancialMovementApprove</c> and no new catalogue row exists.
    /// </summary>
    [Fact]
    public void Each_extract_route_requires_the_permission_D_189_rules_and_the_route_project()
    {
        Dictionary<string, Permission> expected = new()
        {
            ["POST /api/projects/{projectId:guid}/extracts"] = Permission.ExtractPrepare,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/qc-report"] = Permission.ExtractQcReportSubmit,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/evidence-revision"] = Permission.ExtractPrepare,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/technical-approval"] = Permission.QuantityGateApprove,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/technical-rejection"] = Permission.QuantityGateApprove,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/deductions"] = Permission.ExtractDeductionsApply,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/accounts-rejection"] = Permission.ExtractDeductionsApply,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/owner-approval"] = Permission.FinancialMovementApprove,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/owner-rejection"] = Permission.FinancialMovementApprove,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/issue"] = Permission.FinancialMovementApprove,
            ["GET /api/projects/{projectId:guid}/extracts"] = Permission.ProjectRead,
            ["GET /api/projects/{projectId:guid}/extracts/{id:guid}"] = Permission.ProjectRead,
        };

        Dictionary<string, PermissionRequirement> actual = [];
        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (RouteEndpoint route in source.Endpoints.OfType<RouteEndpoint>())
            {
                string? pattern = route.RoutePattern.RawText;
                if (pattern is null || !pattern.Contains("/extracts", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string method in route.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods)
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

        actual.Keys.Should().BeEquivalentTo(expected.Keys, "every extract route is gated, and no other extract route exists");
        foreach ((string key, Permission permission) in expected)
        {
            actual[key].Permission.Should().Be(permission, key);
            actual[key].Scope.Should().Be(ProjectScope.FromRoute(), $"{key} checks the assignment to the route's project");
        }
    }

    [Fact]
    public async Task The_qc_report_needs_supervisor_level_so_a_junior_engineer_is_refused()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);
        string route = Step(project.Id, extract, "qc-report");

        foreach (Guid refused in new[] { _prepEngineer, _owner, _finance, _technicalOffice, _hr, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _supervisor, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_technical_office_gate_is_the_technical_offices_alone()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        string route = Step(project.Id, extract, "technical-approval");

        foreach (Guid refused in new[] { _owner, _finance, _prepEngineer, _supervisor, _hr })
        {
            (await SendAsync(HttpMethod.Post, route, refused, new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _technicalOffice, new { })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// AC-500-I. The Technical Office cannot apply deductions, and neither can the Owner: the grants must not
    /// intersect, so nobody holds both ends of the chain. Refused callers leave no posting behind.
    /// </summary>
    [Fact]
    public async Task Only_finance_applies_deductions_and_the_technical_office_and_owner_are_refused()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.TechnicalApproved);
        string route = Step(project.Id, extract, "deductions");
        long before = await PostingCountAsync(project.Id);

        foreach (Guid refused in new[] { _technicalOffice, _owner, _financeUnassigned, _prepEngineer, _hr })
        {
            (await SendAsync(HttpMethod.Post, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await PostingCountAsync(project.Id)).Should().Be(before, "a refused call posts nothing");
        (await SendAsync(HttpMethod.Post, route, _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Owner_approval_is_the_owners_alone()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.DeductionsApplied);
        string route = Step(project.Id, extract, "owner-approval");

        foreach (Guid refused in new[] { _finance, _technicalOffice, _supervisor, _hr })
        {
            (await SendAsync(HttpMethod.Post, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Issue_is_the_owners_alone_for_now()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.OwnerApproved);
        string route = Step(project.Id, extract, "issue");

        foreach (Guid refused in new[] { _finance, _technicalOffice, _supervisor, _hr })
        {
            (await SendAsync(HttpMethod.Post, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Each_rejection_route_is_gated_like_the_gate_it_rejects_at()
    {
        Project project = await NewProjectAsync();
        object body = new { reason = "wrong quantities" };

        Guid atTechnical = await DriveAsync(project, ExtractStatus.QcSubmitted);
        string technical = Step(project.Id, atTechnical, "technical-rejection");
        foreach (Guid refused in new[] { _owner, _finance, _supervisor, _hr })
        {
            (await SendAsync(HttpMethod.Post, technical, refused, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Finance is refused at the Technical Office route even though it is assigned: wrong gate.
        (await SendAsync(HttpMethod.Post, Step(project.Id, atTechnical, "accounts-rejection"), _technicalOffice, body))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, Step(project.Id, atTechnical, "owner-rejection"), _technicalOffice, body))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, technical, _technicalOffice, body)).StatusCode.Should().Be(HttpStatusCode.OK);

        project = await NewProjectAsync();
        Guid atAccounts = await DriveAsync(project, ExtractStatus.TechnicalApproved);
        (await SendAsync(HttpMethod.Post, Step(project.Id, atAccounts, "accounts-rejection"), _owner, body))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, Step(project.Id, atAccounts, "accounts-rejection"), _finance, body))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        project = await NewProjectAsync();
        Guid atOwner = await DriveAsync(project, ExtractStatus.DeductionsApplied);
        (await SendAsync(HttpMethod.Post, Step(project.Id, atOwner, "owner-rejection"), _finance, body))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, Step(project.Id, atOwner, "owner-rejection"), _owner, body))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_rejection_route_refuses_an_extract_that_is_not_waiting_at_its_gate()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "owner-rejection"), _owner, new { reason = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("extract.not_waiting_at_this_gate");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.QcSubmitted, "a refused rejection moves nothing");
    }

    [Fact]
    public async Task Reading_needs_project_read_and_an_assignment()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);

        foreach (Guid refused in new[] { _engineerUnassigned, _financeUnassigned, _hr })
        {
            (await SendAsync(HttpMethod.Get, Extracts(project.Id), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Get, Single(project.Id, extract), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Get, Single(project.Id, extract), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_anonymous_call_is_refused()
    {
        Project project = await NewProjectAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Extracts(project.Id), UriKind.Relative));

        (await _client.SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The preparer is a site engineer, and "site engineers approve nothing financial" (spec.md section 9), so the
    /// extract they prepared comes back without the evidence money or the document.
    /// </summary>
    [Fact]
    public async Task A_site_engineer_reads_the_extract_without_any_money()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.DeductionsApplied);

        JsonElement asEngineer = await ReadAsync(project.Id, extract, _prepEngineer);
        asEngineer.GetProperty("evidence").ValueKind.Should().Be(JsonValueKind.Null);
        asEngineer.GetProperty("document").ValueKind.Should().Be(JsonValueKind.Null);

        JsonElement asFinance = await ReadAsync(project.Id, extract, _finance);
        asFinance.GetProperty("evidence").ValueKind.Should().Be(JsonValueKind.Object);
        asFinance.GetProperty("document").ValueKind.Should().Be(JsonValueKind.Object);
    }

    // ---- the chain, section 15 extract one -------------------------------------------------------

    /// <summary>
    /// AC-500-A and AC-500-J. The first extract of spec.md section 15 driven through every route: work 300,000,
    /// hold 60,000, advance recovered 75,000, material advance 75,000, net payable 240,000. The printed column
    /// adds to the printed total exactly, and every figure declares its kind.
    /// </summary>
    [Fact]
    public async Task Section_15_extract_one_runs_through_the_routes_and_the_printed_column_foots()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        JsonElement read = await ReadAsync(project.Id, extract, _owner);
        read.GetProperty("status").GetString().Should().Be("Issued");

        JsonElement document = read.GetProperty("document");
        JsonElement figures = document.GetProperty("figures");
        Decimal(figures, "workValue").Should().Be(300_000m);
        Decimal(figures, "holdThisPeriod").Should().Be(60_000m);
        Decimal(figures, "holdToDate").Should().Be(60_000m);
        Decimal(figures, "advanceRecovered").Should().Be(75_000m);
        Decimal(figures, "materialAdvance").Should().Be(75_000m);
        Decimal(figures, "netPayable").Should().Be(240_000m);

        document.GetProperty("foots").GetBoolean().Should().BeTrue();
        document.GetProperty("printedTotal").GetString().Should().Be(document.GetProperty("columnSum").GetString());
        decimal printed = document.GetProperty("column").EnumerateArray()
            .Sum(row => (row.GetProperty("isDeduction").GetBoolean() ? -1m : 1m) * ParseDecimal(row.GetProperty("amount")));
        printed.Should().Be(240_000m, "the rows as printed add to the total as printed");

        foreach (JsonElement figure in figures.EnumerateArray())
        {
            figure.GetProperty("kind").GetString().Should().BeOneOf("Calculated", "Entered", "Carried");
            figure.GetProperty("derivation").GetArrayLength().Should().BeGreaterThan(0, "no number is shown without its provenance");
        }

        JsonElement material = figures.EnumerateArray().Single(figure => figure.GetProperty("key").GetString() == "materialAdvance");
        material.GetProperty("derivation").EnumerateArray()
            .Where(input => input.GetProperty("kind").GetString() == "Entered")
            .Should().OnlyContain(input => input.GetProperty("enteredByUserId").ValueKind == JsonValueKind.String
                && input.GetProperty("enteredAt").ValueKind == JsonValueKind.String);

        // The ledgers, summed from the postings: hold accrued 60,000, advance 250,000 - 75,000.
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(60_000m);
        (await BalanceAsync(project.Id, AccountType.ClientAdvance)).Should().Be(175_000m);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m, "the debt stands until collection");
    }

    [Fact]
    public async Task Accounts_deductions_post_nothing_and_owner_approval_posts_the_accrual_in_one_save()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.TechnicalApproved);
        long start = await PostingCountAsync(project.Id);

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "deductions"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostingCountAsync(project.Id)).Should().Be(start, "the default ruling accrues at Owner approval");

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "owner-approval"), _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostingCountAsync(project.Id)).Should().Be(start + 4, "revenue, hold, advance recovery and the material issue; no material recovery yet");
    }

    [Fact]
    public async Task The_next_extract_certifies_from_where_the_last_one_stopped_and_only_after_it_is_issued()
    {
        Project project = await NewProjectAsync();
        Guid first = await DriveAsync(project, ExtractStatus.QcSubmitted);

        (await SendAsync(HttpMethod.Post, Extracts(project.Id), _prepEngineer, PrepareBody()))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "the first is still in flight");

        await CompleteAsync(project, first);
        await ExecuteAsync(project, 400m);

        HttpResponseMessage second = await SendAsync(HttpMethod.Post, Extracts(project.Id), _prepEngineer, PrepareBody());
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument created = JsonDocument.Parse(await second.Content.ReadAsStringAsync(Ct));
        created.RootElement.GetProperty("number").GetInt32().Should().Be(2);

        await using KaffDbContext reader = _database.CreateBareContext();
        Extract stored = await reader.Extracts.SingleAsync(candidate => candidate.Number == 2 && candidate.ProjectId == project.Id, Ct);
        stored.PreviouslyCertifiedWork.Should().Be(new Money(300_000m));
        stored.CumulativeCertifiedWork.Should().Be(new Money(400_000m), "derived from 400 executed at 1,000, never typed");
    }

    // ---- separation of duties, and the read model that leaves the control out --------------------

    [Fact]
    public async Task Someone_who_acted_earlier_in_the_cycle_is_refused_and_the_read_model_leaves_the_control_out()
    {
        Project project = await NewProjectAsync();
        Guid extractId;

        await using (KaffDbContext context = _database.CreateContext())
        {
            Project loaded = await context.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct);
            // The technical-office user prepared this one (a role change after the fact, or a data fix): they
            // hold QuantityGateApprove and are on the record as its preparer.
            Extract extract = Extract.Prepare(
                loaded, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), new Money(300_000m), Money.Zero,
                new Money(100_000m), Money.Zero, _technicalOffice, Now).Value;
            extract.SubmitQcReport(_supervisor, Now).IsSuccess.Should().BeTrue();
            context.Extracts.Add(extract);
            await context.SaveChangesAsync(Ct);
            extractId = extract.Id;
        }

        JsonElement asPreparer = await ReadAsync(project.Id, extractId, _technicalOffice);
        asPreparer.GetProperty("actions").GetProperty("approveTechnicalOffice").GetBoolean().Should().BeFalse();
        asPreparer.GetProperty("actions").GetProperty("rejectAtTechnicalOffice").GetBoolean().Should().BeFalse();
        asPreparer.GetProperty("actions").GetProperty("callerMayDecide").GetBoolean().Should().BeFalse();

        JsonElement asOther = await ReadAsync(project.Id, extractId, _technicalOffice2);
        asOther.GetProperty("actions").GetProperty("approveTechnicalOffice").GetBoolean().Should().BeTrue();
        asOther.GetProperty("actions").GetProperty("callerMayDecide").GetBoolean().Should().BeTrue();

        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Post, Step(project.Id, extractId, "technical-approval"), _technicalOffice, new { });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(refused)).Should().Be("auth.same_actor_created_and_approved");
        (await StatusOfAsync(extractId)).Should().Be(ExtractStatus.QcSubmitted);

        (await SendAsync(HttpMethod.Post, Step(project.Id, extractId, "technical-approval"), _technicalOffice2, new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_read_model_offers_each_role_only_its_own_controls()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.DeductionsApplied);

        Dictionary<string, bool> owner = await ActionsAsync(project.Id, extract, _owner);
        owner.Where(pair => pair.Value).Select(pair => pair.Key).Should().BeEquivalentTo(
            ["approveAsOwner", "rejectAtOwner", "callerMayDecide"], "the Owner sees the owner gate and nothing else");

        Dictionary<string, bool> finance = await ActionsAsync(project.Id, extract, _finance);
        finance.Where(pair => pair.Key != "callerMayDecide").Should().OnlyContain(pair => !pair.Value, "Finance has nothing to do at the Owner's gate");

        Dictionary<string, bool> supervisor = await ActionsAsync(project.Id, extract, _supervisor);
        supervisor.Where(pair => pair.Key != "callerMayDecide").Should().OnlyContain(pair => !pair.Value);
    }

    // ---- rejections, reversal, atomicity, stopped project -----------------------------------------

    [Fact]
    public async Task A_rejection_needs_a_reason_returns_to_draft_and_stores_the_reason()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        string route = Step(project.Id, extract, "technical-rejection");

        foreach (object body in new object[] { new { reason = "   " }, new { reason = (string?)null }, new { } })
        {
            HttpResponseMessage blank = await SendAsync(HttpMethod.Post, route, _technicalOffice, body);
            (await ErrorCodeAsync(blank)).Should().Be("extract.reason_required");
        }

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.QcSubmitted, "a refused rejection steps nothing back");

        (await SendAsync(HttpMethod.Post, route, _technicalOffice, new { reason = "  quantities do not match the site  " }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Extract stored = await reader.Extracts.Include(candidate => candidate.GateHistory).SingleAsync(candidate => candidate.Id == extract, Ct);
        stored.Status.Should().Be(ExtractStatus.Draft);
        ExtractGateRecord rejection = stored.GateHistory.Single(record => record.Outcome == ExtractGateOutcome.Rejected);
        rejection.Reason.Should().Be("quantities do not match the site");
        rejection.ActorUserId.Should().Be(_technicalOffice);
    }

    /// <summary>
    /// AC-500-G over HTTP. The default ruling accrues at Owner approval, after which an extract cannot be
    /// rejected, so the reversal path is reachable only if the ruling moves earlier; Karim's answer could do that.
    /// This runs with the ruling set to Accounts and proves the rejection reverses and never deletes.
    /// </summary>
    [Fact]
    public async Task A_rejection_after_the_ruled_accrual_writes_reversing_postings_and_deletes_nothing()
    {
        using RulingHost accountsRuling = RulingFactory(ExtractStage.AccountsDeductions);
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.TechnicalApproved, accountsRuling.Client);

        (await SendAsync(accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "deductions"), _finance, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        long accrued = await PostingCountAsync(project.Id);
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(60_000m);

        HttpResponseMessage rejected = await SendAsync(
            accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "owner-rejection"), _owner, new { reason = "hold rate wrong" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK);

        (await PostingCountAsync(project.Id)).Should().Be(accrued + 4, "four accrual legs, four mirrors, none removed");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(0m);
        (await BalanceAsync(project.Id, AccountType.ClientAdvance)).Should().Be(250_000m);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> reversals = await reader.Postings
            .Where(posting => posting.ProjectId == project.Id && posting.ReversesId != null)
            .ToListAsync(Ct);
        reversals.Should().HaveCount(4);
        reversals.Should().OnlyContain(reversal => reversal.SourceDocumentId == extract);
    }

    /// <summary>
    /// AC-500-K. The advance ledger is drawn down between Accounts and the Owner, so the Owner's approval would
    /// recover 75,000 from a ledger holding 50,000. The deferred guard refuses at commit, none of the postings is
    /// written, the extract has not moved, and the refusal names the balance as it then stands.
    /// </summary>
    [Fact]
    public async Task A_posting_that_would_breach_a_guard_writes_none_and_names_the_balance()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.DeductionsApplied);
        await DrawDownAdvanceAsync(project, 200_000m);
        long before = await PostingCountAsync(project.Id);

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "owner-approval"), _owner, null);

        refused.IsSuccessStatusCode.Should().BeFalse();
        (await ErrorCodeAsync(refused)).Should().Be("treasury.negative_balance");
        (await PostingCountAsync(project.Id)).Should().Be(before, "none of the five postings was written");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.DeductionsApplied, "the status did not move either");

        using JsonDocument problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Ct));
        JsonElement balances = problem.RootElement.GetProperty("balances");
        balances.EnumerateArray().Should().Contain(balance =>
            balance.GetProperty("accountCode").GetString()!.EndsWith("-CADV", StringComparison.Ordinal)
            && ParseDecimal(balance.GetProperty("balance")) == 50_000m);
    }

    /// <summary>AC-500-H. The endpoint half agrees with the domain half: no posting, no status change, no control offered.</summary>
    [Fact]
    public async Task A_stopped_project_issues_nothing()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.OwnerApproved);
        await SetStatusAsync(project.Id, ProjectStatus.Stopped);
        long before = await PostingCountAsync(project.Id);

        (await ActionsAsync(project.Id, extract, _owner))["issue"].Should().BeFalse("the control is left out for a stopped project");

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "issue"), _owner, null);

        refused.IsSuccessStatusCode.Should().BeFalse();
        (await PostingCountAsync(project.Id)).Should().Be(before);
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.OwnerApproved);
    }

    // ---- revising the evidence of a rejected extract (AC-500-F, the correctable half) --------------

    private static object ReviseBody() => new { materialValueOnSite = "90000", materialInstalled = "0" };

    private async Task<Guid> RejectedAsync(Project project)
    {
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "technical-rejection"), _technicalOffice, new { reason = "quantities are short" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return extract;
    }

    [Fact]
    public async Task Revising_evidence_is_for_an_assigned_preparer_and_nobody_else()
    {
        Project project = await NewProjectAsync();
        Guid extract = await RejectedAsync(project);
        string route = Step(project.Id, extract, "evidence-revision");

        foreach (Guid refused in new[] { _owner, _finance, _technicalOffice, _hr, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, route, refused, ReviseBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _prepEngineer, ReviseBody())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_rejected_extract_is_revised_from_current_quantities_and_the_chain_runs_on_the_new_figures()
    {
        Project project = await NewProjectAsync();
        Guid extract = await RejectedAsync(project);
        await ExecuteAsync(project, 350m);
        long postings = await PostingCountAsync(project.Id);

        HttpResponseMessage revised = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "evidence-revision"), _prepEngineer, ReviseBody());
        revised.StatusCode.Should().Be(HttpStatusCode.OK, await revised.Content.ReadAsStringAsync(Ct));

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Draft);
        JsonElement read = await ReadAsync(project.Id, extract, _owner);
        ParseDecimal(read.GetProperty("evidence").GetProperty("cumulativeCertifiedWork")).Should().Be(350_000m, "re-derived, not typed");
        ParseDecimal(read.GetProperty("evidence").GetProperty("materialValueOnSite")).Should().Be(90_000m);

        // The revision is on the history, after the rejection, with the revising engineer.
        JsonElement[] history = [.. read.GetProperty("gateHistory").EnumerateArray()];
        history.Count(record => record.GetProperty("outcome").GetString() == "Rejected").Should().Be(1);
        JsonElement last = history[^1];
        last.GetProperty("gate").GetString().Should().Be("EvidenceRevised");
        last.GetProperty("actorUserId").GetGuid().Should().Be(_prepEngineer);

        (await PostingCountAsync(project.Id)).Should().Be(postings, "a revision writes no posting");

        await StepAsync(_client, project, extract, ExtractStatus.Draft, ExtractStatus.DeductionsApplied);
        JsonElement document = (await ReadAsync(project.Id, extract, _finance)).GetProperty("document");
        Decimal(document.GetProperty("figures"), "workValue").Should().Be(350_000m, "the chain ran on the revised quantities");
    }

    [Fact]
    public async Task Revision_is_refused_outside_a_rejected_draft_and_changes_nothing()
    {
        Project project = await NewProjectAsync();
        Guid fresh = await PrepareAsync(project);
        string freshRoute = Step(project.Id, fresh, "evidence-revision");
        HttpResponseMessage neverRejected = await SendAsync(HttpMethod.Post, freshRoute, _prepEngineer, ReviseBody());
        neverRejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(neverRejected)).Should().Be("extract.revision_not_allowed");

        await StepAsync(_client, project, fresh, ExtractStatus.Draft, ExtractStatus.QcSubmitted);
        (await ErrorCodeAsync(await SendAsync(HttpMethod.Post, freshRoute, _prepEngineer, ReviseBody())))
            .Should().Be("extract.revision_not_allowed", "an extract past Draft is never edited");

        await StepAsync(_client, project, fresh, ExtractStatus.QcSubmitted, ExtractStatus.Issued);
        (await ErrorCodeAsync(await SendAsync(HttpMethod.Post, freshRoute, _prepEngineer, ReviseBody())))
            .Should().Be("extract.revision_not_allowed");
        (await StatusOfAsync(fresh)).Should().Be(ExtractStatus.Issued);

        await using KaffDbContext reader = _database.CreateBareContext();
        Extract stored = await reader.Extracts.Include(candidate => candidate.GateHistory).SingleAsync(candidate => candidate.Id == fresh, Ct);
        stored.GateHistory.Should().NotContain(record => record.Gate == ExtractGate.EvidenceRevised);
        stored.MaterialValueOnSite.Should().Be(new Money(100_000m));
    }

    [Fact]
    public async Task The_read_model_offers_revision_to_the_preparer_of_a_rejected_draft_and_to_nobody_else()
    {
        Project project = await NewProjectAsync();
        Guid fresh = await PrepareAsync(project);
        (await ActionsAsync(project.Id, fresh, _prepEngineer))["reviseEvidence"].Should().BeFalse("nothing has been rejected yet");

        await StepAsync(_client, project, fresh, ExtractStatus.Draft, ExtractStatus.QcSubmitted);
        (await SendAsync(HttpMethod.Post, Step(project.Id, fresh, "technical-rejection"), _technicalOffice, new { reason = "short" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ActionsAsync(project.Id, fresh, _prepEngineer))["reviseEvidence"].Should().BeTrue();
        foreach (Guid other in new[] { _owner, _finance, _technicalOffice })
        {
            (await ActionsAsync(project.Id, fresh, other))["reviseEvidence"].Should().BeFalse("the control is left out, not disabled");
        }

        (await SendAsync(HttpMethod.Post, Step(project.Id, fresh, "evidence-revision"), _prepEngineer, ReviseBody()))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await ActionsAsync(project.Id, fresh, _prepEngineer))["reviseEvidence"].Should().BeTrue("a draft may be revised again before resubmission");

        await StepAsync(_client, project, fresh, ExtractStatus.Draft, ExtractStatus.QcSubmitted);
        (await ActionsAsync(project.Id, fresh, _prepEngineer))["reviseEvidence"].Should().BeFalse("once resubmitted it is no longer a draft");
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private static string Extracts(Guid project) => $"/api/projects/{project}/extracts";

    private static string Single(Guid project, Guid extract) => $"/api/projects/{project}/extracts/{extract}";

    private static string Step(Guid project, Guid extract, string step) => $"/api/projects/{project}/extracts/{extract}/{step}";

    private static object PrepareBody() => new
    {
        periodStart = "2026-09-01",
        periodEnd = "2026-09-30",
        materialValueOnSite = "100000",
        materialInstalled = "0",
    };

    private static decimal ParseDecimal(JsonElement element) =>
        decimal.Parse(element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText(), CultureInfo.InvariantCulture);

    private static decimal Decimal(JsonElement figures, string key) =>
        ParseDecimal(figures.EnumerateArray().Single(figure => figure.GetProperty("key").GetString() == key).GetProperty("amount"));

    private async Task<Guid> PrepareAsync(Project project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Extracts(project.Id), _prepEngineer, PrepareBody());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> DriveAsync(Project project, ExtractStatus target, HttpClient? client = null)
    {
        client ??= _client;
        Guid extract = await PrepareAsync(project);
        await StepAsync(client, project, extract, ExtractStatus.Draft, target);
        return extract;
    }

    private Task CompleteAsync(Project project, Guid extract) => StepAsync(_client, project, extract, ExtractStatus.QcSubmitted, ExtractStatus.Issued);

    private async Task StepAsync(HttpClient client, Project project, Guid extract, ExtractStatus from, ExtractStatus target)
    {
        (ExtractStatus Reaches, string Route, Guid Actor, object? Body)[] chain =
        [
            (ExtractStatus.QcSubmitted, "qc-report", _supervisor, null),
            (ExtractStatus.TechnicalApproved, "technical-approval", _technicalOffice, new { }),
            (ExtractStatus.DeductionsApplied, "deductions", _finance, null),
            (ExtractStatus.OwnerApproved, "owner-approval", _owner, null),
            (ExtractStatus.Issued, "issue", _owner, null),
        ];

        foreach ((ExtractStatus reaches, string route, Guid actor, object? body) in chain)
        {
            if (reaches <= from || reaches > target)
            {
                continue;
            }

            HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, Step(project.Id, extract, route), actor, body);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");
        }
    }

    // The status ordering of the happy path matches the enum except PartiallyApproved (3) and TechnicalApproved (4),
    // which these tests do not use together.

    private async Task<JsonElement> ReadAsync(Guid project, Guid extract, Guid actor)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, Single(project, extract), actor, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<Dictionary<string, bool>> ActionsAsync(Guid project, Guid extract, Guid actor)
    {
        JsonElement read = await ReadAsync(project, extract, actor);
        return read.GetProperty("actions").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetBoolean());
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body) =>
        SendAsync(_client, method, route, actorId, body);

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string route, Guid actorId, object? body)
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

        return await client.SendAsync(request, Ct);
    }

    private async Task<ExtractStatus> StatusOfAsync(Guid extract)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Extracts.Where(candidate => candidate.Id == extract).Select(candidate => candidate.Status).SingleAsync(Ct);
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

    private async Task DrawDownAdvanceAsync(Project project, decimal amount)
    {
        await using KaffDbContext context = _database.CreateContext();
        Account advance = await context.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.ClientAdvance, Ct);
        Account receivable = await context.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.ClientReceivable, Ct);
        context.Postings.Add(Posting.Create(
            receivable, advance, new Money(amount), PostingType.ClientAdvanceRecovery,
            new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "DRAWDOWN"),
            new DateOnly(2026, 9, 26), _owner, Now, project.Id).Value);
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>A host whose ruled accrual moment is <paramref name="stage"/>, over the same database.</summary>
    private RulingHost RulingFactory(ExtractStage stage)
    {
        var factory = new KaffApiFactory(_database.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(RulingSet.Default with { ExtractPostingMoment = new ExtractPostingMoment(stage) })));
        return new RulingHost(factory, factory.CreateClient());
    }

    private sealed class RulingHost(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, HttpClient client) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
        }
    }

    private async Task<Project> NewProjectAsync()
    {
        Project project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            project = Project.Create(UniqueNames.Code("EX-P"), "مشروع مستخلص", _clientId, ContractType.LumpSum, Now).Value;
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

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/sign", _owner, new { signedOn = "2026-09-24" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext activate = _database.CreateContext())
        {
            Project loaded = await activate.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct);
            loaded.TransitionTo(ProjectStatus.Active, new DateOnly(2026, 9, 24)).IsSuccess.Should().BeTrue();

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
            UniqueNames.Code("EX-C"), "عميل المستخلص", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Bab bab = Bab.Create(UniqueNames.Code("EXB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(0m)).Value;
        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("EXSAFE"), "خزنة", "Safe", Currency.Egp, new DateOnly(2026, 1, 1)).Value;

        User owner = Make("ex-owner", Role.Owner);
        User finance = Make("ex-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User financeUnassigned = Make("ex-fin-un", Role.Finance, WellKnownDepartments.FinanceId);
        User technicalOffice = Make("ex-to", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User technicalOffice2 = Make("ex-to2", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User prep = Make("ex-prep", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("ex-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User engineerUnassigned = Make("ex-eng-un", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = Make("ex-hr", Role.Hr, WellKnownDepartments.HrId);

        context.Clients.Add(client);
        context.Babs.Add(bab);
        context.Accounts.Add(safe);
        context.Users.AddRange(owner, finance, financeUnassigned, technicalOffice, technicalOffice2, prep, supervisor, engineerUnassigned, hr);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _babId = bab.Id;
        _safeId = safe.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _financeUnassigned = financeUnassigned.Id;
        _technicalOffice = technicalOffice.Id;
        _technicalOffice2 = technicalOffice2.Id;
        _prepEngineer = prep.Id;
        _supervisor = supervisor.Id;
        _engineerUnassigned = engineerUnassigned.Id;
        _hr = hr.Id;
    }

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;
}
