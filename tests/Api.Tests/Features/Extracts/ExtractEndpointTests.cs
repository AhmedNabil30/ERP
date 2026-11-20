using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
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
public sealed partial class ExtractEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private readonly TestClock _clock = new();
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _individualClientId;
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
        _factory = new KaffApiFactory(_database, clock: _clock);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- money documents are dated by the site day, not the UTC day (D-224 open item) --------------

    [Theory]
    [InlineData("2027-07-15T22:30:00+00:00", "2027-07-16")] // Cairo +03:00 in summer: 01:30 on the 16th
    [InlineData("2027-12-15T22:30:00+00:00", "2027-12-16")] // Cairo +02:00 in winter: 00:30 on the 16th
    public async Task The_issue_postings_carry_the_site_day(string instant, string postingDate)
    {
        Project project = await NewProjectAsync();
        _clock.Advance(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture) - _clock.GetUtcNow());

        await DriveAsync(project, ExtractStatus.Issued);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> postings = await reader.Postings
            .Where(p => p.ProjectId == project.Id && p.SourceDocumentType == SourceDocumentType.Extract).ToListAsync(Ct);
        postings.Should().NotBeEmpty();
        postings.Select(p => p.PostingDate).Distinct().Should().Equal(DateOnly.Parse(postingDate, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("2027-07-15T22:30:00+00:00", "2027-07-16")]
    [InlineData("2027-12-15T22:30:00+00:00", "2027-12-16")]
    public async Task The_credit_note_posting_carries_the_site_day(string instant, string postingDate)
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 40_000m);
        _clock.Advance(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture) - _clock.GetUtcNow());

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting credit = await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.CreditNote, Ct);
        credit.PostingDate.Should().Be(DateOnly.Parse(postingDate, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_collection_posts_on_the_site_day_and_keeps_the_date_the_user_gave()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        _clock.Advance(DateTimeOffset.Parse("2027-07-15T22:30:00+00:00", CultureInfo.InvariantCulture) - _clock.GetUtcNow());

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody())).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting cash = await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.ClientCollection, Ct);
        cash.PostingDate.Should().Be(new DateOnly(2027, 7, 16), "the posting is dated by the site day");
        CollectionRecord record = await reader.CollectionRecords.SingleAsync(c => c.ExtractId == extract, Ct);
        record.Date.Should().Be(new DateOnly(2026, 10, 1), "the date the user typed is taken as given");
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
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/collect"] = Permission.ExtractCollect,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/collection-reversal"] = Permission.ExtractCollect,
            ["GET /api/projects/{projectId:guid}/extracts/{id:guid}/collection"] = Permission.ExtractCollect,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/dispute"] = Permission.ExtractDisputeRaise,
            ["POST /api/projects/{projectId:guid}/extracts/{id:guid}/dispute-resolution"] = Permission.ExtractDisputeResolve,
            ["GET /api/projects/{projectId:guid}/extracts"] = Permission.ExtractRead,
            ["GET /api/projects/{projectId:guid}/extracts/{id:guid}"] = Permission.ProjectRead,
            ["GET /api/projects/{projectId:guid}/extracts/{id:guid}/quantities"] = Permission.ExtractRead,
            ["GET /api/projects/{projectId:guid}/extracts/quantities-preview"] = Permission.ExtractPrepare,
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
            (await SendAsync(HttpMethod.Post, route, refused, QcBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, route, _supervisor, QcBody())).StatusCode.Should().Be(HttpStatusCode.OK);
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
        object body = new { reason = "wrong quantities", kind = "MaterialValues" };

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
            HttpMethod.Post, Step(project.Id, extract, "owner-rejection"), _owner, new { reason = "x", kind = "MaterialValues" });

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

    // ---- the proof read: expected from the calculator, actual from the postings -------------------

    /// <summary>
    /// spec.md section 15, extract 1: the proof screen's expectation for an issued extract comes from the
    /// calculator over the extract's stored inputs, and the posted figures match it to the cent. Nothing is
    /// collected yet, so the collection column is expected to be zero.
    /// </summary>
    [Fact]
    public async Task The_proof_expects_extract_one_from_the_calculator_and_the_postings_match()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        JsonElement proofEvent = ExtractEventOf(await ProofAsync(project.Id), extract, out string state);

        state.Should().Be("Available");
        ColumnsOf(proofEvent, "expected").Should().Equal(300_000m, -60_000m, -75_000m, 75_000m, 0m);
        ColumnsOf(proofEvent, "actual").Should().Equal(300_000m, -60_000m, -75_000m, 75_000m, 0m);
        ColumnsOf(proofEvent, "difference").Should().Equal(0m, 0m, 0m, 0m, 0m);
        proofEvent.GetProperty("expectedUnavailableReason").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// A second extract is expected over the ledger balances left by the first (the advance is 175,000 by then), read
    /// from the postings as they stood before the second's own accrual, and still matches.
    /// </summary>
    [Fact]
    public async Task The_proof_expects_a_second_extract_over_the_balances_the_first_left()
    {
        Project project = await NewProjectAsync();
        Guid first = await DriveAsync(project, ExtractStatus.Issued);
        await ExecuteAsync(project, 600m);
        Guid second = await DriveAsync(project, ExtractStatus.Issued);

        JsonElement proof = await ProofAsync(project.Id);

        foreach (Guid id in new[] { first, second })
        {
            ColumnsOf(ExtractEventOf(proof, id, out _), "difference").Should().Equal(0m, 0m, 0m, 0m, 0m);
        }

        ColumnsOf(ExtractEventOf(proof, second, out _), "expected")[2].Should().Be(-75_000m);
    }

    /// <summary>
    /// The expectation is not fitted to the postings: a stray hold accrual under the extract's source document makes
    /// the hold column differ by exactly that amount and nothing else.
    /// </summary>
    [Fact]
    public async Task The_proof_shows_a_stray_posting_as_a_difference_and_does_not_absorb_it()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        await using (KaffDbContext context = _database.CreateContext())
        {
            Account hold = await context.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.Hold, Ct);
            Account receivable = await context.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.ClientReceivable, Ct);
            context.Postings.Add(Posting.Create(
                receivable, hold, new Money(1_000m), PostingType.HoldAccrual,
                new SourceDocument(SourceDocumentType.Extract, extract, "EXT-1"),
                new DateOnly(2026, 9, 27), _owner, Now.AddMinutes(5), project.Id).Value);
            await context.SaveChangesAsync(Ct);
        }

        JsonElement proofEvent = ExtractEventOf(await ProofAsync(project.Id), extract, out _);

        ColumnsOf(proofEvent, "expected")[1].Should().Be(-60_000m);
        ColumnsOf(proofEvent, "difference").Should().Equal(0m, -1_000m, 0m, 0m, 0m);
    }

    /// <summary>
    /// An extract rejected back to Draft after its accrual is reversed by mirror postings; its expectation is zero in
    /// every column and the netted postings match it.
    /// </summary>
    [Fact]
    public async Task The_proof_expects_nothing_from_a_rejected_extract_whose_accrual_was_reversed()
    {
        using RulingHost accountsRuling = RulingFactory(ExtractStage.AccountsDeductions);
        // No hold: a hold leg is never reversed (spec.md §5.1, Verifier F-5), so only a hold-free accrual can be rejected.
        Project project = await NewProjectAsync(holdPercent: 0m);
        Guid extract = await DriveAsync(project, ExtractStatus.DeductionsApplied, accountsRuling.Client);

        HttpResponseMessage rejected = await SendAsync(
            accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "owner-rejection"), _owner, new { reason = "test", kind = "MaterialValues" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync(Ct));

        JsonElement proofEvent = ExtractEventOf(await ProofAsync(project.Id, accountsRuling.Client), extract, out _);

        ColumnsOf(proofEvent, "expected").Should().Equal(0m, 0m, 0m, 0m, 0m);
        ColumnsOf(proofEvent, "difference").Should().Equal(0m, 0m, 0m, 0m, 0m);
    }

    private static readonly string[] ColumnKeys = ["work", "hold", "advance", "materialAdvance", "collection"];

    private async Task<JsonElement> ProofAsync(Guid project, HttpClient? client = null)
    {
        HttpResponseMessage response = await SendAsync(client ?? _client, HttpMethod.Get, $"/api/projects/{project}/treasury/proof", _owner, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static JsonElement ExtractEventOf(JsonElement proof, Guid extract, out string expectedState)
    {
        expectedState = proof.GetProperty("expectedState").GetString()!;
        return proof.GetProperty("events").EnumerateArray()
            .Single(item => item.GetProperty("sourceDocumentId").GetGuid() == extract);
    }

    private static decimal[] ColumnsOf(JsonElement proofEvent, string side)
    {
        JsonElement columns = proofEvent.GetProperty(side);
        return [.. ColumnKeys.Select(key => ParseDecimal(columns.GetProperty(key)))];
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

        (await SendAsync(HttpMethod.Post, route, _technicalOffice, new { reason = "  quantities do not match the site  ", kind = "MaterialValues" }))
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
        // No hold on this contract: a hold leg is never reversed (spec.md §5.1, Verifier F-5); the next test covers that.
        Project project = await NewProjectAsync(holdPercent: 0m);
        Guid extract = await DriveAsync(project, ExtractStatus.TechnicalApproved, accountsRuling.Client);

        (await SendAsync(accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "deductions"), _finance, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        long accrued = await PostingCountAsync(project.Id);
        int legs;
        await using (KaffDbContext before = _database.CreateBareContext())
        {
            legs = await before.Postings.CountAsync(posting => posting.SourceDocumentId == extract, Ct);
        }

        legs.Should().BePositive();

        HttpResponseMessage rejected = await SendAsync(
            accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "owner-rejection"), _owner, new { reason = "rate wrong", kind = "MaterialValues" });
        rejected.StatusCode.Should().Be(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync(Ct));

        (await PostingCountAsync(project.Id)).Should().Be(accrued + legs, "one mirror per accrual leg, none removed");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(0m);
        (await BalanceAsync(project.Id, AccountType.ClientAdvance)).Should().Be(250_000m);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> reversals = await reader.Postings
            .Where(posting => posting.ProjectId == project.Id && posting.ReversesId != null)
            .ToListAsync(Ct);
        reversals.Should().HaveCount(legs);
        reversals.Should().OnlyContain(reversal => reversal.SourceDocumentId == extract);
    }

    /// <summary>
    /// spec.md §5.1: nothing comes out of the hold before handover. With the ruling at Accounts, a rejection after the
    /// accrual would reverse its hold leg, so the rejection is refused whole and nothing is written (Verifier F-5).
    /// </summary>
    [Fact]
    public async Task A_rejection_that_would_reverse_a_hold_accrual_is_refused_and_writes_nothing()
    {
        using RulingHost accountsRuling = RulingFactory(ExtractStage.AccountsDeductions);
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.TechnicalApproved, accountsRuling.Client);
        (await SendAsync(accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "deductions"), _finance, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        long accrued = await PostingCountAsync(project.Id);

        HttpResponseMessage rejected = await SendAsync(
            accountsRuling.Client, HttpMethod.Post, Step(project.Id, extract, "owner-rejection"), _owner, new { reason = "hold rate wrong", kind = "MaterialValues" });

        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(rejected)).Should().Be("treasury.hold_posting_not_reversible");
        (await PostingCountAsync(project.Id)).Should().Be(accrued);
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(60_000m);
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
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "technical-rejection"), _technicalOffice, new { reason = "quantities are short", kind = "Quantities" }))
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
        // D-235 item 4: the revision carries no money. The material values stay as the supervisor entered them and are
        // entered again with the next QC report.
        ParseDecimal(read.GetProperty("evidence").GetProperty("materialValueOnSite")).Should().Be(100_000m);

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
        (await SendAsync(HttpMethod.Post, Step(project.Id, fresh, "technical-rejection"), _technicalOffice, new { reason = "short", kind = "Quantities" }))
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

    // ---- KAFF-316 / KAFF-317: collections and cheques ----

    /// <summary>
    /// D-235 item 2: a collection records what arrived. The default is spec.md section 15's extract one paid in full, with
    /// nothing withheld.
    /// </summary>
    private static object CollectBody(string method = "Transfer", decimal cash = 240_000m, decimal withheld = 0m) => new
    {
        method,
        date = "2026-10-01",
        reference = "TRX-1",
        cashReceived = cash.ToString(CultureInfo.InvariantCulture),
        withheldAtSource = withheld.ToString(CultureInfo.InvariantCulture),
    };

    private async Task<long> CollectionPostingCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project && posting.Type == PostingType.ClientCollection, Ct);
    }

    [Fact]
    public async Task Collect_is_for_finance_alone_and_needs_an_assignment_to_the_project()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        string route = Step(project.Id, extract, "collect");

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, route, refused, CollectBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);
        (await SendAsync(HttpMethod.Post, route, _finance, CollectBody())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("{\"date\":\"2026-10-01\",\"reference\":\"R-1\"}", "collection.method_required")]
    [InlineData("{\"method\":\"Cash\",\"reference\":\"R-1\"}", "collection.date_required")]
    [InlineData("{\"method\":\"Cash\",\"date\":\"2026-10-01\"}", "collection.reference_required")]
    [InlineData("{\"method\":\"Cash\",\"date\":\"2026-10-01\",\"reference\":\"  \"}", "collection.reference_required")]
    public async Task A_collection_missing_method_date_or_reference_is_refused_and_nothing_changes(string json, string code)
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Step(project.Id, extract, "collect"), UriKind.Relative)) { Content = content };
        await using KaffDbContext reader = _database.CreateBareContext();
        var finance = await reader.Users.Where(user => user.Id == _finance).Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);
        request.Headers.Add(TestAuthHandler.UserIdHeader, _finance.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, finance.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, finance.Role.ToString());

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be(code);
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Cash_reaches_the_safe_at_Collected_and_not_at_Issued_and_no_other_ledger_moves()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        (await CollectionPostingCountAsync(project.Id)).Should().Be(0, "issuing writes no cash");
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cash"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> cash = await reader.Postings
            .Where(posting => posting.ProjectId == project.Id && posting.Type == PostingType.ClientCollection).ToListAsync(Ct);
        cash.Should().ContainSingle().Which.Amount.Should().Be(new Money(240_000m), "cash received equals the net payable");
        cash[0].SourceDocumentId.Should().Be(extract);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m, "the collection settles the debt");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(60_000m, "the hold is not touched by a collection");
        (await BalanceAsync(project.Id, AccountType.ClientAdvance)).Should().Be(175_000m, "the ledgers do not net");

        CollectionRecord record = await reader.CollectionRecords.SingleAsync(candidate => candidate.ExtractId == extract, Ct);
        record.Method.Should().Be(CollectionMethod.Cash);
        record.Reference.Should().Be("TRX-1");
        record.Date.Should().Be(new DateOnly(2026, 10, 1));
        record.CashReceived.Should().Be(new Money(240_000m));
        record.WithheldAtSource.Should().Be(Money.Zero);
        (await reader.Cheques.AnyAsync(cheque => cheque.ProjectId == project.Id, Ct)).Should().BeFalse("cash is not a cheque");
        (await reader.AuditRecords.AnyAsync(a => a.EntityType == nameof(CollectionRecord) && a.EntityId == record.Id, Ct))
            .Should().BeTrue("recording a collection writes audit");
    }

    [Fact]
    public async Task An_extract_that_is_not_issued_cannot_be_collected_and_a_collected_one_cannot_be_collected_twice()
    {
        Project project = await NewProjectAsync();
        Guid early = await DriveAsync(project, ExtractStatus.OwnerApproved);

        (await SendAsync(HttpMethod.Post, Step(project.Id, early, "collect"), _finance, CollectBody())).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);

        await SendAsync(HttpMethod.Post, Step(project.Id, early, "issue"), _owner, null);
        (await SendAsync(HttpMethod.Post, Step(project.Id, early, "collect"), _finance, CollectBody())).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Step(project.Id, early, "collect"), _finance, CollectBody())).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A_cheque_collection_starts_a_cheque_that_only_finance_moves_and_only_by_the_legal_transitions()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cheque"))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        Guid cheque;
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Cheque row = await reader.Cheques.SingleAsync(candidate => candidate.ProjectId == project.Id, Ct);
            row.State.Should().Be(ChequeState.Received);
            cheque = row.Id;
        }

        long postings = await PostingCountAsync(project.Id);
        string Route(string step) => $"/api/projects/{project.Id}/cheques/{cheque}/{step}";

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _hr, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Post, Route("deposit"), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, Route("clearance"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "received cannot clear");
        (await SendAsync(HttpMethod.Post, Route("bounce"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "received cannot bounce");
        (await SendAsync(HttpMethod.Post, Route("deposit"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Route("deposit"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "deposited cannot deposit again");
        (await SendAsync(HttpMethod.Post, Route("clearance"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Route("bounce"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "cleared cannot bounce");

        await using KaffDbContext after = _database.CreateBareContext();
        (await after.Cheques.SingleAsync(candidate => candidate.Id == cheque, Ct)).State.Should().Be(ChequeState.Cleared);
        // D-235 item 1: the cheque becomes cash when it clears, and only then; deposit and the refused moves post nothing.
        (await PostingCountAsync(project.Id)).Should().Be(postings + 1, "the clearance writes the cash leg and nothing else does");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected, "the cleared cheque completes the collection");
        (await after.AuditRecords.CountAsync(a => a.EntityType == nameof(Cheque) && a.EntityId == cheque, Ct))
            .Should().BeGreaterThanOrEqualTo(3, "created, deposited and cleared each write audit");
    }

    [Fact]
    public async Task A_deposited_cheque_can_bounce_and_the_extract_stays_issued()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cheque"));
        await using KaffDbContext reader = _database.CreateBareContext();
        Guid cheque = await reader.Cheques.Where(c => c.ProjectId == project.Id).Select(c => c.Id).SingleAsync(Ct);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/cheques/{cheque}/deposit", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/cheques/{cheque}/bounce", _finance, new { reason = "returned unpaid" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/cheques/{cheque}/clearance", _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // D-235 item 1: a cheque in hand never made the extract Collected, so a bounce leaves it Issued.
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
    }

    // ---- KAFF-316 read side and reversal (AC-316-F) ----

    private async Task<JsonElement> CollectionsAsync(Guid project, Guid extract, Guid actor)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, Step(project, extract, "collection"), actor, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task A_cheque_collection_is_readable_and_its_cheque_id_reaches_the_state_machine()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cheque"))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Get, Step(project.Id, extract, "collection"), refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        JsonElement collections = await CollectionsAsync(project.Id, extract, _finance);
        JsonElement only = collections.EnumerateArray().Single();
        only.GetProperty("method").GetString().Should().Be("Cheque");
        only.GetProperty("reference").GetString().Should().Be("TRX-1");
        only.GetProperty("date").GetString().Should().Be("2026-10-01");
        ParseDecimal(only.GetProperty("cashReceived")).Should().Be(240_000m);
        ParseDecimal(only.GetProperty("withheldAtSource")).Should().Be(0m);
        only.GetProperty("reversed").GetBoolean().Should().BeFalse();
        Guid cheque = only.GetProperty("chequeId").GetGuid();

        // D-239 item 4: the Owner reads a cheque too; he moves none (ExtractRulingsNovemberTests).
        string route = $"/api/projects/{project.Id}/cheques/{cheque}";
        foreach (Guid refused in new[] { _technicalOffice, _supervisor, _hr, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Get, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        async Task<JsonElement> ReadChequeAsync()
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Get, route, _finance, null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            return document.RootElement.Clone();
        }

        JsonElement received = await ReadChequeAsync();
        received.GetProperty("state").GetString().Should().Be("Received");
        received.GetProperty("collection").GetProperty("reference").GetString().Should().Be("TRX-1");

        (await SendAsync(HttpMethod.Post, $"{route}/deposit", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadChequeAsync()).GetProperty("state").GetString().Should().Be("Deposited");

        (await SendAsync(HttpMethod.Get, $"/api/projects/{project.Id}/cheques/{Guid.NewGuid()}", _finance, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_cash_collection_reads_back_with_no_cheque()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        (await CollectionsAsync(project.Id, extract, _finance)).GetArrayLength().Should().Be(0);

        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cash"));

        JsonElement only = (await CollectionsAsync(project.Id, extract, _finance)).EnumerateArray().Single();
        only.GetProperty("method").GetString().Should().Be("Cash");
        only.GetProperty("chequeId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Reversing_a_collection_is_finance_alone_needs_a_reason_and_mirrors_the_postings()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        string route = Step(project.Id, extract, "collection-reversal");
        object body = new { reason = "wrong client receipt" };

        (await SendAsync(HttpMethod.Post, route, _finance, body)).StatusCode.Should().Be(HttpStatusCode.Conflict, "nothing is collected yet");

        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cash"));
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m);

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, route, refused, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        foreach (object blank in new object[] { new { reason = "  " }, new { } })
        {
            HttpResponseMessage refused = await SendAsync(HttpMethod.Post, route, _finance, blank);
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ErrorCodeAsync(refused)).Should().Be("extract.reason_required");
        }

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(1, "a refused reversal writes nothing");

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, route, _finance, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m, "the debt stands again");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(60_000m, "the hold is untouched by a reversal");
        (await BalanceAsync(project.Id, AccountType.ClientAdvance)).Should().Be(175_000m);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> collection = await reader.Postings
            .Where(posting => posting.ProjectId == project.Id && posting.Type == PostingType.ClientCollection).ToListAsync(Ct);
        collection.Should().HaveCount(2, "the original stays and a new reversing posting is added; none is updated or deleted");
        Posting original = collection.Single(posting => posting.ReversesId is null);
        Posting reversal = collection.Single(posting => posting.ReversesId is not null);
        reversal.ReversesId.Should().Be(original.Id);
        reversal.Amount.Should().Be(original.Amount);
        reversal.FromAccountId.Should().Be(original.ToAccountId);
        reversal.ToAccountId.Should().Be(original.FromAccountId);

        (await reader.CollectionRecords.CountAsync(record => record.ExtractId == extract, Ct)).Should().Be(1, "the record is never deleted");
        ExtractGateRecord gate = await reader.Set<ExtractGateRecord>()
            .SingleAsync(record => record.ExtractId == extract && record.Outcome == ExtractGateOutcome.Reversed, Ct);
        gate.Reason.Should().Be("wrong client receipt");
        gate.ActorUserId.Should().Be(_finance);
        (await reader.AuditRecords.AnyAsync(a => a.EntityType == nameof(ExtractGateRecord) && a.EntityId == gate.Id, Ct))
            .Should().BeTrue("the reversal writes audit: who, when, and the stored reason");

        JsonElement read = (await CollectionsAsync(project.Id, extract, _finance)).EnumerateArray().Single();
        read.GetProperty("reversed").GetBoolean().Should().BeTrue();

        (await SendAsync(HttpMethod.Post, route, _finance, body)).StatusCode.Should().Be(HttpStatusCode.Conflict, "it cannot be reversed twice");
        (await CollectionPostingCountAsync(project.Id)).Should().Be(2);
    }

    [Fact]
    public async Task A_reversed_collection_can_be_recorded_again_and_the_read_shows_both()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cash"));
        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collection-reversal"), _finance, new { reason = "wrong date" });

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Transfer"))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        JsonElement[] rows = [.. (await CollectionsAsync(project.Id, extract, _finance)).EnumerateArray()];
        rows.Should().HaveCount(2);
        rows[0].GetProperty("method").GetString().Should().Be("Cash");
        rows[0].GetProperty("reversed").GetBoolean().Should().BeTrue();
        rows[1].GetProperty("method").GetString().Should().Be("Transfer");
        rows[1].GetProperty("reversed").GetBoolean().Should().BeFalse();
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m);

        // The reversal that stands is the second collection's: the first set is already reversed and is not mirrored again.
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collection-reversal"), _finance, new { reason = "again" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(4);
    }

    // ---- D-236 "still open": a flow's posting is reversed by that flow and by nothing else ----------

    [Fact]
    public async Task No_posting_of_a_collected_extract_can_be_reversed_as_a_generic_movement()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cash"))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        List<string> before = await PostingSnapshotsAsync(project.Id);

        List<Posting> owned;
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            owned = await reader.Postings
                .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract && posting.SourceDocumentId == extract)
                .ToListAsync(Ct);
        }

        owned.Should().Contain(posting => posting.Type == PostingType.ClientCollection, "the collection is the case D-236 names");
        foreach (Posting posting in owned)
        {
            HttpResponseMessage refused = await SendAsync(
                HttpMethod.Post,
                $"/api/projects/{project.Id}/treasury/postings/{posting.Id}/reverse",
                _finance,
                new { postingDate = posting.PostingDate });

            refused.StatusCode.Should().Be(HttpStatusCode.Conflict, posting.Type.ToString());
            (await ErrorCodeAsync(refused)).Should().BeOneOf(
                ["treasury.flow_posting_not_reversible", "treasury.hold_posting_not_reversible"], posting.Type.ToString());
        }

        (await PostingSnapshotsAsync(project.Id)).Should().BeEquivalentTo(before, "a refused reversal writes nothing");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
    }

    [Fact]
    public async Task An_approved_credit_note_cannot_be_reversed_as_a_generic_movement_only_through_the_adjustment()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 40_000m);
        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        Posting credit;
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            credit = await reader.Postings.SingleAsync(
                posting => posting.ProjectId == project.Id && posting.Type == PostingType.CreditNote, Ct);
        }

        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Post,
            $"/api/projects/{project.Id}/treasury/postings/{credit.Id}/reverse",
            _finance,
            new { postingDate = credit.PostingDate });
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(refused)).Should().Be("treasury.flow_posting_not_reversible");

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "wrong amount" })).StatusCode
            .Should().Be(HttpStatusCode.OK, "the adjustment's own reversal still works");
    }

    /// <summary>
    /// The same rule with the domain and the handler out of the way: a mirror of the collection inserted straight into
    /// the table, as the application's role, with no gate record beside it.
    /// </summary>
    [Fact]
    public async Task The_database_refuses_a_reversal_of_a_flow_posting_that_its_flow_did_not_make()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Cash"));

        await using KaffDbContext raw = _database.CreateBareContext();
        Posting collection = await raw.Postings.SingleAsync(
            posting => posting.ProjectId == project.Id && posting.Type == PostingType.ClientCollection, Ct);

        await DatabaseGuard.RefusesAsync(
            () => raw.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO postings
                     (id, posting_date, from_account_id, to_account_id, amount, "type",
                      source_document_type, source_document_id, source_document_reference,
                      project_id, created_by_user_id, created_at, reverses_id)
                 VALUES
                     ({Guid.CreateVersion7()}, {collection.PostingDate}, {collection.ToAccountId}, {collection.FromAccountId},
                      {collection.Amount.Amount}, {nameof(PostingType.ClientCollection)},
                      {nameof(SourceDocumentType.Extract)}, {extract}, NULL,
                      {project.Id}, {_finance}, {DateTimeOffset.UtcNow}, {collection.Id})
                 """,
                Ct),
            DatabaseGuard.FlowReversal);
    }

    // ---- KAFF-317: withholding at source, end to end through the endpoint ----

    private async Task<(Guid Extract, List<Posting> Cash, List<Posting> Tax)> CollectAsync(
        Project project, HttpClient? client = null)
    {
        client ??= _client;
        Guid extract = await DriveAsync(project, ExtractStatus.Issued, client);

        // D-235 item 2: the client pays what the extract expects, split as the ruled base expects it.
        (decimal cashDue, decimal withheldDue) = await DueAsync(client, project.Id, extract);
        HttpResponseMessage response = await SendAsync(
            client, HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody("Transfer", cashDue, withheldDue));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> cash = await reader.Postings
            .Where(posting => posting.ProjectId == project.Id && posting.Type == PostingType.ClientCollection).ToListAsync(Ct);
        List<Posting> tax = await reader.Postings
            .Where(posting => posting.ProjectId == project.Id && posting.Type == PostingType.TaxWithheldAtSource).ToListAsync(Ct);
        return (extract, cash, tax);
    }

    [Fact]
    public async Task An_individual_client_withholds_nothing_so_no_tax_component_and_no_withholding_posting()
    {
        // AC-317-B / F: the rate on an individual's contract is refused, and a collection from one records no withholding.
        Project rejected = Project.Create(UniqueNames.Code("EX-IW"), "x", _individualClientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        rejected.SetWithholding(WithholdingCategory.ContractingAndSupplies, ClientKind.Individual).IsFailure.Should().BeTrue();

        Project project = await NewProjectAsync(_individualClientId, WithholdingCategory.None, ClientKind.Individual);
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody());

        await using KaffDbContext reader = _database.CreateBareContext();
        Extract stored = await reader.Extracts.Include(e => e.Figures).SingleAsync(e => e.Id == extract, Ct);
        stored.Figures.Should().NotBeEmpty();
        stored.Figures.Should().NotContain(figure => figure.Kind == BillingComponentKind.TaxAtSource, "an individual's contract has no category");
        (await reader.Postings.AnyAsync(p => p.ProjectId == project.Id && p.Type == PostingType.TaxWithheldAtSource, Ct)).Should().BeFalse();
        (await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.ClientCollection, Ct))
            .Amount.Should().Be(new Money(240_000m));
        (await reader.CollectionRecords.SingleAsync(r => r.ExtractId == extract, Ct)).WithheldAtSource.Should().Be(Money.Zero);
    }

    [Fact]
    public async Task A_corporate_contract_with_no_category_withholds_nothing()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.None);

        (Guid _, List<Posting> cash, List<Posting> tax) = await CollectAsync(project);

        tax.Should().BeEmpty();
        cash.Should().ContainSingle().Which.Amount.Should().Be(new Money(240_000m));
    }

    [Fact]
    public async Task A_corporate_client_withholds_and_cash_is_the_net_less_the_withheld_amount_posted_only_at_Collected()
    {
        // spec.md 6.7: 1% contracting and supplies. Net payable 240,000 (spec.md 15, extract 1) -> withheld 2,400, cash 237,600.
        // The rate is spec.md's; it is not changed here whatever public guidance says.
        Project project = await NewProjectAsync(withholding: WithholdingCategory.ContractingAndSupplies);

        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        // The stage: nothing reaches the safe or the withholding account at Issued.
        await using (KaffDbContext atIssued = _database.CreateBareContext())
        {
            (await atIssued.Postings.AnyAsync(
                p => p.ProjectId == project.Id && (p.Type == PostingType.ClientCollection || p.Type == PostingType.TaxWithheldAtSource), Ct))
                .Should().BeFalse("neither leg is written at Issued");
        }

        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m, "the full net stands as the receivable at Issued");

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody(cash: 237_600m, withheld: 2_400m)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting cash = await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.ClientCollection, Ct);
        Posting tax = await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.TaxWithheldAtSource, Ct);

        tax.Amount.Should().Be(new Money(2_400m));
        cash.Amount.Should().Be(new Money(237_600m), "cash into the safe is the net less the withheld amount");
        (cash.Amount + tax.Amount).Should().Be(new Money(240_000m), "the two legs sum to the net payable");

        Account safe = await reader.Accounts.SingleAsync(a => a.Id == cash.ToAccountId, Ct);
        safe.Type.Should().Be(AccountType.Safe);
        Account taxAccount = await reader.Accounts.SingleAsync(a => a.Id == tax.ToAccountId, Ct);
        taxAccount.Type.Should().Be(AccountType.TaxWithheldAtSource);
        cash.SourceDocumentId.Should().Be(extract);
        tax.SourceDocumentId.Should().Be(extract);

        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m, "both legs together settle the receivable");

        CollectionRecord record = await reader.CollectionRecords.SingleAsync(r => r.ExtractId == extract, Ct);
        record.CashReceived.Should().Be(new Money(237_600m));
        record.WithheldAtSource.Should().Be(new Money(2_400m));

        JsonElement view = (await CollectionsAsync(project.Id, extract, _finance)).EnumerateArray().Single();
        ParseDecimal(view.GetProperty("cashReceived")).Should().Be(237_600m);
        ParseDecimal(view.GetProperty("withheldAtSource")).Should().Be(2_400m);

        // Reversing mirrors both legs: the debt stands again in full.
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collection-reversal"), _finance, new { reason = "bounced transfer" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m);
        (await reader.Postings.CountAsync(p => p.ProjectId == project.Id && p.ReversesId != null, Ct)).Should().Be(2);
    }

    [Theory]
    [InlineData(WithholdingBaseKind.NetPayable, 2_400)]
    [InlineData(WithholdingBaseKind.PeriodWorkValue, 3_000)]
    [InlineData(WithholdingBaseKind.CertifiedLessHold, 2_400)]
    public async Task The_withheld_amount_follows_the_ruled_base_with_no_handler_change(WithholdingBaseKind kind, int expectedWithheld)
    {
        // AC-317-D. Net 240,000, period work 300,000, certified less hold 240,000; 1% of each.
        var factory = new KaffApiFactory(_database).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(RulingSet.Default with { WithholdingBase = new WithholdingBaseRule(kind, HoldReleaseCarriesWithholding: true) })));
        using HttpClient client = factory.CreateClient();
        using (factory)
        {
            Project project = await NewProjectAsync(withholding: WithholdingCategory.ContractingAndSupplies);

            (Guid _, List<Posting> cash, List<Posting> tax) = await CollectAsync(project, client);

            tax.Should().ContainSingle().Which.Amount.Should().Be(new Money(expectedWithheld));
            cash.Should().ContainSingle().Which.Amount.Should().Be(new Money(240_000m - expectedWithheld));
        }
    }

    // ---- KAFF-511 / KAFF-512: a disputed extract, and the one Adjustment object ----

    private static string Adjustments(Guid project) => $"/api/projects/{project}/adjustments";

    private static string AdjustmentStep(Guid project, Guid id, string step) => $"/api/projects/{project}/adjustments/{id}/{step}";

    private async Task<Guid> AccountIdAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Accounts.Where(a => a.ProjectId == project && a.Type == type).Select(a => a.Id).SingleAsync(Ct);
    }

    private async Task<List<string>> PostingSnapshotsAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> postings = await reader.Postings.Where(p => p.ProjectId == project).OrderBy(p => p.Id).ToListAsync(Ct);
        return [.. postings.Select(p => $"{p.Id}|{p.FromAccountId}|{p.ToAccountId}|{p.Amount.Amount}|{p.Type}|{p.ReversesId}|{p.PostingDate}|{p.CreatedByUserId}")];
    }

    private async Task<long> PostingCountOfAsync(Guid project, PostingType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(p => p.ProjectId == project && p.Type == type, Ct);
    }

    private async Task<long> AdjustmentCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Adjustments.LongCountAsync(a => a.ProjectId == project, Ct);
    }

    /// <summary>Raises a credit note through the adjustment route. The revenue account exists once an extract has issued.</summary>
    private async Task<Guid> RaiseCreditNoteAsync(Project project, Guid extract, decimal amount, string reason = "client disputes the quantities")
    {
        Guid revenue = await AccountIdAsync(project.Id, AccountType.ContractRevenue);
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Adjustments(project.Id), _finance, new
        {
            type = "ClientCreditNote",
            reason,
            sourceExtractId = extract,
            targetAccountId = revenue,
            amount = amount.ToString(CultureInfo.InvariantCulture),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public void Each_adjustment_route_requires_the_permission_the_catalogue_names_and_the_route_project()
    {
        Dictionary<string, Permission> expected = new()
        {
            ["POST /api/projects/{projectId:guid}/adjustments"] = Permission.AdjustmentRaise,
            ["POST /api/projects/{projectId:guid}/adjustments/{id:guid}/approval"] = Permission.FinancialMovementApprove,
            ["POST /api/projects/{projectId:guid}/adjustments/{id:guid}/rejection"] = Permission.FinancialMovementApprove,
            ["POST /api/projects/{projectId:guid}/adjustments/{id:guid}/reversal"] = Permission.FinancialMovementApprove,
            ["GET /api/projects/{projectId:guid}/adjustments"] = Permission.TreasuryReadProject,
            ["GET /api/projects/{projectId:guid}/adjustments/{id:guid}"] = Permission.TreasuryReadProject,
        };

        Dictionary<string, PermissionRequirement> actual = [];
        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (RouteEndpoint route in source.Endpoints.OfType<RouteEndpoint>())
            {
                string? pattern = route.RoutePattern.RawText;
                if (pattern is null || !pattern.Contains("/adjustments", StringComparison.Ordinal))
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

        actual.Keys.Should().BeEquivalentTo(expected.Keys, "every adjustment route is gated, and no other exists");
        foreach ((string key, Permission permission) in expected)
        {
            actual[key].Permission.Should().Be(permission, key);
            actual[key].Scope.Should().Be(ProjectScope.FromRoute(), $"{key} checks the assignment to the route project");
        }
    }

    [Fact]
    public async Task Only_assigned_finance_raises_or_resolves_a_dispute_and_nobody_else_reaches_either_route()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        string dispute = Step(project.Id, extract, "dispute");
        string resolution = Step(project.Id, extract, "dispute-resolution");
        object resolve = new { kind = "CreditNote", reason = "agreed", amount = "1000" };
        List<string> before = await PostingSnapshotsAsync(project.Id);

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, dispute, refused, new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden, "raise");
            (await SendAsync(HttpMethod.Post, resolution, refused, resolve)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "resolve");
        }

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued, "a refused call moves nothing");
        (await PostingSnapshotsAsync(project.Id)).Should().Equal(before);
        (await SendAsync(HttpMethod.Post, dispute, _finance, new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_the_owner_decides_an_adjustment_and_only_assigned_finance_raises_one()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid revenue = await AccountIdAsync(project.Id, AccountType.ContractRevenue);
        object raise = new
        {
            type = "ClientCreditNote",
            reason = "x",
            sourceExtractId = extract,
            targetAccountId = revenue,
            amount = "1000",
        };

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Post, Adjustments(project.Id), refused, raise)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await AdjustmentCountAsync(project.Id)).Should().Be(0);
        Guid note = await RaiseCreditNoteAsync(project, extract, 1_000m);

        foreach (string step in new[] { "approval", "rejection", "reversal" })
        {
            foreach (Guid refused in new[] { _finance, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned })
            {
                (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, step), refused, new { reason = "x" }))
                    .StatusCode.Should().Be(HttpStatusCode.Forbidden, step);
            }
        }

        (await PostingCountOfAsync(project.Id, PostingType.CreditNote)).Should().Be(0);
    }

    [Fact]
    public async Task A_dispute_changes_no_posting_and_the_hold_balance_not_at_the_raise_and_not_at_either_resolution()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        decimal hold = await BalanceAsync(project.Id, AccountType.Hold);
        decimal receivable = await BalanceAsync(project.Id, AccountType.ClientReceivable);
        List<string> before = await PostingSnapshotsAsync(project.Id);
        hold.Should().BeGreaterThan(0m, "the issued extract accrued a hold");

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "dispute"), _finance, new { reason = "quantities are wrong" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Disputed);
        (await PostingSnapshotsAsync(project.Id)).Should().Equal(before, "the extract postings stand while disputed");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(hold);

        // Credit note: the extract is Issued again, a credit note is Raised, and still nothing is posted.
        HttpResponseMessage credited = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "dispute-resolution"), _finance,
            new { kind = "CreditNote", reason = "credit agreed", amount = "40000" });
        credited.StatusCode.Should().Be(HttpStatusCode.OK, await credited.Content.ReadAsStringAsync(Ct));
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        (await AdjustmentCountAsync(project.Id)).Should().Be(1);
        (await PostingSnapshotsAsync(project.Id)).Should().Equal(before, "raising the credit note posts nothing");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(hold);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(receivable);
    }

    [Fact]
    public async Task A_dispute_needs_an_issued_extract_a_reason_and_a_resolution_needs_a_disputed_one()
    {
        Project draftProject = await NewProjectAsync();
        Guid draft = await PrepareAsync(draftProject);
        (await SendAsync(HttpMethod.Post, Step(draftProject.Id, draft, "dispute"), _finance, new { reason = "x" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a draft cannot be disputed");

        Project project = await NewProjectAsync();
        Guid issued = await DriveAsync(project, ExtractStatus.Issued);
        HttpResponseMessage blank = await SendAsync(HttpMethod.Post, Step(project.Id, issued, "dispute"), _finance, new { reason = "  " });
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(blank)).Should().Be("extract.reason_required");
        (await StatusOfAsync(issued)).Should().Be(ExtractStatus.Issued);

        (await SendAsync(HttpMethod.Post, Step(project.Id, issued, "dispute-resolution"), _finance,
            new { kind = "CreditNote", reason = "x", amount = "1000" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "an issued extract is not disputed");

        (await SendAsync(HttpMethod.Post, Step(project.Id, issued, "dispute"), _finance, new { reason = "x" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Step(project.Id, issued, "dispute"), _finance, new { reason = "again" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "already disputed");

        (await SendAsync(HttpMethod.Post, Step(project.Id, issued, "collect"), _finance, CollectBody())).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "collect from Disputed");
        (await SendAsync(HttpMethod.Post, Step(project.Id, issued, "issue"), _owner, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "issue from Disputed");
        (await SendAsync(HttpMethod.Post, Step(project.Id, issued, "owner-rejection"), _owner, new { reason = "x", kind = "MaterialValues" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "reject from Disputed");

        (await StatusOfAsync(issued)).Should().Be(ExtractStatus.Disputed);
    }

    [Fact]
    public async Task A_resolution_with_no_kind_a_bad_amount_or_no_reason_is_refused_and_leaves_the_extract_disputed()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "dispute"), _finance, new { reason = "x" });
        string route = Step(project.Id, extract, "dispute-resolution");

        (await SendAsync(HttpMethod.Post, route, _finance, new { reason = "x", amount = "1000" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "no kind");
        (await SendAsync(HttpMethod.Post, route, _finance, new { kind = "CreditNote", reason = " ", amount = "1000" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "no reason");
        (await SendAsync(HttpMethod.Post, route, _finance, new { kind = "CreditNote", reason = "x", amount = "0" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "zero");
        (await SendAsync(HttpMethod.Post, route, _finance, new { kind = "CreditNote", reason = "x", amount = "-5" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "negative");

        // D-239 item 2: more than the net is no longer refused, so it is not among the bad amounts.
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Disputed);
        (await AdjustmentCountAsync(project.Id)).Should().Be(0);
    }

    /// <summary>
    /// The stage each posting is written at. Raising a credit note writes none; the Owner approval writes exactly one
    /// CreditNote posting, new, from the receivable to revenue, and every earlier posting is byte-for-byte unchanged.
    /// The hold is read before and after at each stage.
    /// </summary>
    [Fact]
    public async Task The_credit_note_posts_only_at_the_owners_approval_as_one_new_posting_and_the_hold_never_moves()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        decimal hold = await BalanceAsync(project.Id, AccountType.Hold);
        decimal receivable = await BalanceAsync(project.Id, AccountType.ClientReceivable);
        List<string> before = await PostingSnapshotsAsync(project.Id);

        Guid note = await RaiseCreditNoteAsync(project, extract, 40_000m);
        (await PostingCountOfAsync(project.Id, PostingType.CreditNote)).Should().Be(0, "raising posts nothing");
        (await PostingSnapshotsAsync(project.Id)).Should().Equal(before);

        HttpResponseMessage approved = await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null);
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(Ct));

        (await PostingCountOfAsync(project.Id, PostingType.CreditNote)).Should().Be(1);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(receivable - 40_000m);
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(hold, "a credit note never reaches into the hold");
        (await PostingCountOfAsync(project.Id, PostingType.HoldRelease)).Should().Be(0);

        List<string> after = await PostingSnapshotsAsync(project.Id);
        after.Should().HaveCount(before.Count + 1);
        after.Where(row => before.Contains(row)).Should().BeEquivalentTo(before, "every earlier posting is unchanged");
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Posting credit = await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.CreditNote, Ct);
            credit.ReversesId.Should().BeNull();
            credit.SourceDocumentType.Should().Be(SourceDocumentType.Adjustment);
            credit.SourceDocumentId.Should().Be(note);
            credit.FromAccountId.Should().Be(await AccountIdAsync(project.Id, AccountType.ClientReceivable));
            credit.ToAccountId.Should().Be(await AccountIdAsync(project.Id, AccountType.ContractRevenue));
            credit.Amount.Should().Be(new Money(40_000m));
        }

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a second approval posts nothing");
        (await PostingCountOfAsync(project.Id, PostingType.CreditNote)).Should().Be(1);
    }

    /// <summary>AC-512-D at the endpoint: the hold is named as the target and nothing is created, posted or moved.</summary>
    [Fact]
    public async Task An_adjustment_that_names_the_hold_is_refused_in_the_domain_and_the_hold_balance_is_unchanged()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid hold = await AccountIdAsync(project.Id, AccountType.Hold);
        decimal balance = await BalanceAsync(project.Id, AccountType.Hold);
        List<string> before = await PostingSnapshotsAsync(project.Id);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, Adjustments(project.Id), _finance, new
        {
            type = "ClientCreditNote",
            reason = "settle from the hold",
            sourceExtractId = extract,
            targetAccountId = hold,
            amount = "5000",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("adjustment.hold_untouchable");
        (await AdjustmentCountAsync(project.Id)).Should().Be(0);
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(balance);
        (await PostingSnapshotsAsync(project.Id)).Should().Equal(before);

        foreach (AccountType other in new[] { AccountType.ClientAdvance, AccountType.ClientReceivable, AccountType.MaterialAdvance })
        {
            HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Adjustments(project.Id), _finance, new
            {
                type = "ClientCreditNote",
                reason = "x",
                sourceExtractId = extract,
                targetAccountId = await AccountIdAsync(project.Id, other),
                amount = "5000",
            });
            (await ErrorCodeAsync(refused)).Should().Be("adjustment.target_account_not_allowed", other.ToString());
        }

        (await AdjustmentCountAsync(project.Id)).Should().Be(0);
    }

    [Fact]
    public async Task An_adjustment_missing_a_field_or_of_a_type_not_built_is_refused_and_nothing_is_stored()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid revenue = await AccountIdAsync(project.Id, AccountType.ContractRevenue);

        object Body(string type = "ClientCreditNote", string reason = "x", string amount = "1000") => new
        {
            type,
            reason,
            sourceExtractId = extract,
            targetAccountId = revenue,
            amount,
        };

        (string Name, object Body, HttpStatusCode Status, string Code)[] refusals =
        [
            ("no type", new { reason = "x", sourceExtractId = extract, targetAccountId = revenue, amount = "1000" }, HttpStatusCode.BadRequest, "adjustment.type_required"),
            ("no reason", Body(reason: " "), HttpStatusCode.BadRequest, "adjustment.reason_required"),
            ("no source", new { type = "ClientCreditNote", reason = "x", targetAccountId = revenue, amount = "1000" }, HttpStatusCode.BadRequest, "adjustment.source_document_required"),
            ("no target", new { type = "ClientCreditNote", reason = "x", sourceExtractId = extract, amount = "1000" }, HttpStatusCode.BadRequest, "adjustment.target_account_required"),
            ("zero", Body(amount: "0"), HttpStatusCode.BadRequest, "adjustment.amount_must_be_positive"),
            ("negative", Body(amount: "-1000"), HttpStatusCode.BadRequest, "adjustment.amount_must_be_positive"),
            ("debit note", Body("SubcontractorDebitNote"), HttpStatusCode.Conflict, "adjustment.type_not_built"),
            ("design credit", Body("DesignCredit"), HttpStatusCode.Conflict, "adjustment.type_not_built"),
            ("termination", Body("TerminationSettlement"), HttpStatusCode.Conflict, "adjustment.termination_settlement_unruled"),
        ];

        foreach ((string name, object body, HttpStatusCode status, string code) in refusals)
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Post, Adjustments(project.Id), _finance, body);
            response.StatusCode.Should().Be(status, name);
            (await ErrorCodeAsync(response)).Should().Be(code, name);
        }

        (await AdjustmentCountAsync(project.Id)).Should().Be(0);
        (await PostingCountOfAsync(project.Id, PostingType.CreditNote)).Should().Be(0);
    }

    /// <summary>D-239 item 2: credit notes against one extract are not capped together either.</summary>
    [Fact]
    public async Task Credit_notes_against_one_extract_may_together_exceed_its_net_and_a_rejection_needs_a_reason()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        Guid first = await RaiseCreditNoteAsync(project, extract, 200_000m);
        await RaiseCreditNoteAsync(project, extract, 40_000.0001m);

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, first, "rejection"), _owner, new { reason = "  " })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a rejection needs a reason");
        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, first, "rejection"), _owner, new { reason = "not agreed" })).StatusCode
            .Should().Be(HttpStatusCode.OK);

        await RaiseCreditNoteAsync(project, extract, 240_000m);
        (await PostingCountOfAsync(project.Id, PostingType.CreditNote)).Should().Be(0, "a rejection posts nothing");
    }

    [Fact]
    public async Task An_extract_with_a_credit_note_open_cannot_be_collected_until_the_credit_note_is_rejected()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 10_000m);

        HttpResponseMessage blocked = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody());
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(blocked)).Should().Be("extract.credit_note_open");
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "rejection"), _owner, new { reason = "withdrawn" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody())).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reversing_an_approved_credit_note_writes_a_mirror_through_ReversesId_and_never_edits_the_original()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        decimal receivable = await BalanceAsync(project.Id, AccountType.ClientReceivable);
        decimal hold = await BalanceAsync(project.Id, AccountType.Hold);
        Guid note = await RaiseCreditNoteAsync(project, extract, 40_000m);

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "x" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a raised adjustment has nothing to reverse");
        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        List<string> approved = await PostingSnapshotsAsync(project.Id);

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "  " })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "wrong amount" })).StatusCode
            .Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            List<Posting> credits = await reader.Postings
                .Where(p => p.ProjectId == project.Id && p.Type == PostingType.CreditNote).ToListAsync(Ct);
            credits.Should().HaveCount(2);
            Posting original = credits.Single(p => p.ReversesId is null);
            Posting mirror = credits.Single(p => p.ReversesId is not null);
            mirror.ReversesId.Should().Be(original.Id);
            mirror.FromAccountId.Should().Be(original.ToAccountId);
            mirror.ToAccountId.Should().Be(original.FromAccountId);
        }

        (await PostingSnapshotsAsync(project.Id)).Where(row => approved.Contains(row)).Should().BeEquivalentTo(approved, "nothing earlier was edited");
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(receivable);
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(hold);
        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "again" })).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Disputing_raising_and_deciding_write_audit_records_with_who_and_why()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await SendAsync(HttpMethod.Post, Step(project.Id, extract, "dispute"), _finance, new { reason = "audit me" });
        Guid note = await RaiseCreditNoteAsync(project, extract, 1_000m, "credit reason on record");
        await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "rejection"), _owner, new { reason = "rejection reason on record" });

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Kaff.Domain.Auditing.AuditRecord> forNote = await reader.AuditRecords.Where(a => a.EntityId == note).ToListAsync(Ct);

        forNote.Should().Contain(a => a.ActorUserId == _finance && (a.AfterJson ?? string.Empty).Contains("credit reason on record", StringComparison.Ordinal));
        forNote.Should().Contain(a => a.ActorUserId == _owner
            && a.ChangedProperties.Contains(nameof(Kaff.Domain.Adjustments.Adjustment.Status))
            && (a.AfterJson ?? string.Empty).Contains("rejection reason on record", StringComparison.Ordinal));

        List<Kaff.Domain.Auditing.AuditRecord> gateRecords = await reader.AuditRecords
            .Where(a => a.EntityType == nameof(ExtractGateRecord) && a.ActorUserId == _finance).ToListAsync(Ct);
        gateRecords.Should().Contain(a => (a.AfterJson ?? string.Empty).Contains("audit me", StringComparison.Ordinal));
    }

    // ---- the overview's reads: progress, team, waiting-on, money on the list (S-052) -----------------

    private static string Progress(Guid project) => $"/api/projects/{project}/progress";

    private static string TeamRoute(Guid project) => $"/api/projects/{project}/team";

    private static string WaitingOnRoute(Guid project) => $"/api/projects/{project}/waiting-on";

    private async Task<JsonElement> GetJsonAsync(string route, Guid actor)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, route, actor, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    [Fact]
    public void The_overview_routes_require_project_read_with_the_route_project()
    {
        Dictionary<string, Permission> expected = new()
        {
            ["GET /api/projects/{projectId:guid}/progress"] = Permission.ProjectRead,
            ["GET /api/projects/{projectId:guid}/team"] = Permission.ProjectRead,
            ["GET /api/projects/{projectId:guid}/waiting-on"] = Permission.ExtractRead,
        };

        Dictionary<string, PermissionRequirement> actual = [];
        foreach (EndpointDataSource source in _factory.Services.GetServices<EndpointDataSource>())
        {
            foreach (RouteEndpoint route in source.Endpoints.OfType<RouteEndpoint>())
            {
                string? pattern = route.RoutePattern.RawText;
                if (pattern is null || !expected.Keys.Any(key => key.EndsWith(pattern, StringComparison.Ordinal)))
                {
                    continue;
                }

                foreach (IAuthorizeData data in route.Metadata.OfType<IAuthorizeData>())
                {
                    if (PermissionRequirement.TryParse(data.Policy ?? string.Empty, out PermissionRequirement? requirement) && requirement is not null)
                    {
                        actual[$"GET {pattern}"] = requirement;
                    }
                }
            }
        }

        actual.Keys.Should().BeEquivalentTo(expected.Keys);
        foreach ((string key, Permission permission) in expected)
        {
            actual[key].Permission.Should().Be(permission, key);
            actual[key].Scope.Should().Be(ProjectScope.FromRoute(), key);
        }
    }

    [Fact]
    public async Task The_overview_reads_refuse_hr_and_an_unassigned_caller_and_admit_the_assigned_roles()
    {
        Project project = await NewProjectAsync();

        foreach (string route in new[] { Progress(project.Id), TeamRoute(project.Id), WaitingOnRoute(project.Id) })
        {
            foreach (Guid refused in new[] { _hr, _engineerUnassigned, _financeUnassigned })
            {
                (await SendAsync(HttpMethod.Get, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, route);
            }

            foreach (Guid admitted in new[] { _owner, _finance, _technicalOffice, _supervisor })
            {
                (await SendAsync(HttpMethod.Get, route, admitted, null)).StatusCode.Should().Be(HttpStatusCode.OK, route);
            }
        }
    }

    [Fact]
    public async Task Lump_sum_progress_is_derived_from_the_executed_quantity_and_moves_when_it_moves()
    {
        Project project = await NewProjectAsync();   // 1,000 units at 1,000, 300 executed

        JsonElement first = await GetJsonAsync(Progress(project.Id), _owner);
        first.GetProperty("kind").GetString().Should().Be("Percentage");
        ParseDecimal(first.GetProperty("completion")).Should().Be(0.3m);
        ParseDecimal(first.GetProperty("executedValue")).Should().Be(300_000m);
        ParseDecimal(first.GetProperty("contractValue")).Should().Be(1_000_000m);
        JsonElement bab = first.GetProperty("babs").EnumerateArray().Should().ContainSingle().Subject;
        bab.GetProperty("isActive").GetBoolean().Should().BeTrue();
        ParseDecimal(bab.GetProperty("completion")).Should().Be(0.3m);

        await ExecuteAsync(project, 500m);

        JsonElement second = await GetJsonAsync(Progress(project.Id), _owner);
        ParseDecimal(second.GetProperty("completion")).Should().Be(0.5m, "nothing stores progress: it follows QuantityExecuted");
        ParseDecimal(second.GetProperty("executedValue")).Should().Be(500_000m);

        await ExecuteAsync(project, 1_000m);
        JsonElement done = await GetJsonAsync(Progress(project.Id), _owner);
        ParseDecimal(done.GetProperty("completion")).Should().Be(1m);
        done.GetProperty("babs")[0].GetProperty("isActive").GetBoolean().Should().BeFalse("a finished Bab is not active");
    }

    [Fact]
    public async Task Progress_money_is_withheld_from_a_caller_who_may_not_see_extract_money_but_the_percentage_is_not()
    {
        Project project = await NewProjectAsync();

        JsonElement siteEngineer = await GetJsonAsync(Progress(project.Id), _prepEngineer);

        ParseDecimal(siteEngineer.GetProperty("completion")).Should().Be(0.3m);
        foreach (string money in new[] { "executedValue", "contractValue", "certifiedToDate", "costToDate", "supervisionToDate" })
        {
            siteEngineer.TryGetProperty(money, out _).Should().BeFalse($"{money} is money and a site engineer does not see it");
        }

        siteEngineer.GetProperty("babs")[0].TryGetProperty("executedValue", out _).Should().BeFalse();

        // D-218 item 6 (Q-218-1, Karim to confirm): the supervising site engineer sees what the owner sees.
        JsonElement supervisor = await GetJsonAsync(Progress(project.Id), _supervisor);
        ParseDecimal(supervisor.GetProperty("executedValue")).Should().Be(300_000m);
        ParseDecimal(supervisor.GetProperty("contractValue")).Should().Be(1_000_000m);
    }

    [Fact]
    public async Task Progress_reports_the_certified_value_of_issued_extracts_beside_the_bar_without_driving_it()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ExtractStatus.Issued);

        JsonElement progress = await GetJsonAsync(Progress(project.Id), _owner);

        ParseDecimal(progress.GetProperty("certifiedToDate")).Should().Be(300_000m);
        ParseDecimal(progress.GetProperty("completion")).Should().Be(0.3m);
    }

    [Fact]
    public async Task Cost_plus_progress_has_no_completion_property_at_all()
    {
        Guid projectId;
        await using (KaffDbContext context = _database.CreateContext())
        {
            Project costPlus = Project.Create(UniqueNames.Code("EX-CP"), UniqueNames.Code("تكلفة وربح"), _clientId, ContractType.CostPlus, DateTimeOffset.UtcNow).Value;
            costPlus.SetCostPlusTerms(Percentage.FromPercent(10m)).IsSuccess.Should().BeTrue();
            context.Projects.Add(costPlus);
            await context.SaveChangesAsync(Ct);
            projectId = costPlus.Id;
        }

        JsonElement progress = await GetJsonAsync(Progress(projectId), _owner);

        progress.GetProperty("kind").GetString().Should().Be("MonetaryOnly");
        progress.TryGetProperty("completion", out _).Should().BeFalse("spec.md section 5.2: no percentage progress bar, not even a zero");
        progress.TryGetProperty("babs", out _).Should().BeFalse();
        ParseDecimal(progress.GetProperty("costToDate")).Should().Be(0m);
        ParseDecimal(progress.GetProperty("supervisionToDate")).Should().Be(0m);
    }

    [Fact]
    public async Task Design_progress_is_refused_rather_than_invented()
    {
        Guid projectId;
        await using (KaffDbContext context = _database.CreateContext())
        {
            Project design = Project.Create(UniqueNames.Code("EX-DS"), UniqueNames.Code("تصميم"), _clientId, ContractType.Design, DateTimeOffset.UtcNow).Value;
            context.Projects.Add(design);
            await context.SaveChangesAsync(Ct);
            projectId = design.Id;
        }

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, Progress(projectId), _owner, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("billing.calculator_not_implemented");
    }

    [Fact]
    public async Task The_team_names_the_people_assigned_and_not_the_ones_who_are_not()
    {
        Project project = await NewProjectAsync();

        JsonElement team = await GetJsonAsync(TeamRoute(project.Id), _owner);

        JsonElement members = team.GetProperty("members");
        members.EnumerateArray().Select(member => member.GetProperty("userId").GetGuid())
            .Should().BeEquivalentTo(new[] { _finance, _technicalOffice, _technicalOffice2, _prepEngineer, _supervisor });
        members.EnumerateArray().Should().OnlyContain(member => member.GetProperty("fullName").GetString()!.Length > 0);
    }

    [Fact]
    public async Task The_project_read_carries_dates_and_money_derived_from_extracts_and_postings()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        JsonElement issued = await GetJsonAsync($"/api/projects/{project.Id}", _owner);
        issued.GetProperty("signedOn").GetString().Should().Be("2026-09-24");
        issued.GetProperty("startedOn").ValueKind.Should().Be(JsonValueKind.String);
        issued.GetProperty("handoverOn").ValueKind.Should().Be(JsonValueKind.Null);
        issued.GetProperty("lastDailyLogOn").ValueKind.Should().Be(JsonValueKind.Null, "no daily log was written");
        JsonElement money = issued.GetProperty("money");
        ParseDecimal(money.GetProperty("certifiedToDate")).Should().Be(300_000m);
        ParseDecimal(money.GetProperty("billedToDate")).Should().Be(240_000m);
        ParseDecimal(money.GetProperty("collectedToDate")).Should().Be(0m);
        ParseDecimal(money.GetProperty("outstanding")).Should().Be(240_000m);

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody())).StatusCode
            .Should().Be(HttpStatusCode.OK);
        JsonElement collected = (await GetJsonAsync($"/api/projects/{project.Id}", _owner)).GetProperty("money");
        ParseDecimal(collected.GetProperty("collectedToDate")).Should().Be(240_000m);
        ParseDecimal(collected.GetProperty("outstanding")).Should().Be(0m);

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collection-reversal"), _finance, new { reason = "bounced" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement reversed = (await GetJsonAsync($"/api/projects/{project.Id}", _owner)).GetProperty("money");
        ParseDecimal(reversed.GetProperty("collectedToDate")).Should().Be(0m, "a reversal subtracts: the figure is summed from postings");
        ParseDecimal(reversed.GetProperty("outstanding")).Should().Be(240_000m);

        (await GetJsonAsync($"/api/projects/{project.Id}", _prepEngineer)).TryGetProperty("money", out _)
            .Should().BeFalse("the junior site engineer does not receive the money block");

        (await GetJsonAsync($"/api/projects/{project.Id}", _supervisor)).GetProperty("money").ValueKind
            .Should().Be(JsonValueKind.Object, "D-218 item 6: the supervising engineer sees project money");
    }

    [Fact]
    public async Task The_extract_list_carries_the_money_a_money_column_needs_for_those_who_may_see_it()
    {
        Project project = await NewProjectAsync();
        Guid issuedExtract = await DriveAsync(project, ExtractStatus.Issued);

        JsonElement ownerRows = await GetJsonAsync(Extracts(project.Id), _owner);
        JsonElement row = ownerRows.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == issuedExtract);
        ParseDecimal(row.GetProperty("money").GetProperty("netPayable")).Should().Be(240_000m);
        ParseDecimal(row.GetProperty("money").GetProperty("collected")).Should().Be(0m);
        ParseDecimal(row.GetProperty("money").GetProperty("outstanding")).Should().Be(240_000m);

        (await SendAsync(HttpMethod.Post, Step(project.Id, issuedExtract, "collect"), _finance, CollectBody())).StatusCode
            .Should().Be(HttpStatusCode.OK);
        JsonElement afterCollect = (await GetJsonAsync(Extracts(project.Id), _finance)).EnumerateArray().Single();
        ParseDecimal(afterCollect.GetProperty("money").GetProperty("collected")).Should().Be(240_000m);
        ParseDecimal(afterCollect.GetProperty("money").GetProperty("outstanding")).Should().Be(0m);

        JsonElement engineerRow = (await GetJsonAsync(Extracts(project.Id), _supervisor)).EnumerateArray().Single();
        engineerRow.GetProperty("money").ValueKind.Should().Be(JsonValueKind.Null);
        engineerRow.GetProperty("waitingOn").GetProperty("isTerminal").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_extract_list_money_is_blank_before_the_extract_is_issued()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ExtractStatus.QcSubmitted);

        JsonElement row = (await GetJsonAsync(Extracts(project.Id), _owner)).EnumerateArray().Single();

        row.GetProperty("money").GetProperty("netPayable").ValueKind.Should().Be(JsonValueKind.Null, "no figures exist before deductions");
        row.GetProperty("money").GetProperty("collected").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Waiting_on_names_the_role_that_holds_the_gate_and_the_people_in_it_as_the_chain_advances()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);

        JsonElement atTechnical = (await GetJsonAsync(WaitingOnRoute(project.Id), _owner)).EnumerateArray().Single().GetProperty("waitingOn");
        atTechnical.GetProperty("status").GetString().Should().Be("QcSubmitted");
        atTechnical.GetProperty("permission").GetString().Should().Be("QuantityGateApprove");
        atTechnical.GetProperty("roles").EnumerateArray().Select(role => role.GetProperty("role").GetString())
            .Should().Equal("TechnicalOffice");
        atTechnical.GetProperty("holders").EnumerateArray().Select(holder => holder.GetProperty("userId").GetGuid())
            .Should().BeEquivalentTo(new[] { _technicalOffice, _technicalOffice2 }, "the Technical Office people assigned here, and nobody else");
        atTechnical.GetProperty("isDecisionGate").GetBoolean().Should().BeTrue();

        await StepAsync(_client, project, extract, ExtractStatus.QcSubmitted, ExtractStatus.TechnicalApproved);

        JsonElement atAccounts = (await GetJsonAsync(WaitingOnRoute(project.Id), _supervisor)).EnumerateArray().Single().GetProperty("waitingOn");
        atAccounts.GetProperty("status").GetString().Should().Be("TechnicalApproved");
        atAccounts.GetProperty("holders").EnumerateArray().Select(holder => holder.GetProperty("userId").GetGuid())
            .Should().BeEquivalentTo(new[] { _finance });

        await StepAsync(_client, project, extract, ExtractStatus.TechnicalApproved, ExtractStatus.DeductionsApplied);

        JsonElement atOwner = (await GetJsonAsync(WaitingOnRoute(project.Id), _finance)).EnumerateArray().Single().GetProperty("waitingOn");
        atOwner.GetProperty("holders").EnumerateArray().Select(holder => holder.GetProperty("userId").GetGuid())
            .Should().Contain(_owner, "the Owner reaches every project without an assignment row");
    }

    [Fact]
    public async Task A_collected_extract_is_waiting_on_nobody_and_is_left_out()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody())).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await GetJsonAsync(WaitingOnRoute(project.Id), _owner)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_extract_read_says_what_it_is_waiting_for()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);

        JsonElement read = await ReadAsync(project.Id, extract, _owner);

        read.GetProperty("waitingOn").GetProperty("roles")[0].GetProperty("role").GetString().Should().Be("TechnicalOffice");
    }

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
            (ExtractStatus.QcSubmitted, "qc-report", _supervisor, QcBody()),
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
        var factory = new KaffApiFactory(_database).WithWebHostBuilder(builder =>
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

    private async Task<Project> NewProjectAsync(
        Guid? clientId = null, WithholdingCategory withholding = WithholdingCategory.None, ClientKind kind = ClientKind.Corporate,
        decimal holdPercent = 20m)
    {
        Project project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            project = Project.Create(UniqueNames.Code("EX-P"), UniqueNames.Code("مشروع مستخلص"), clientId ?? _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
            project.SetWithholding(withholding, kind).IsSuccess.Should().BeTrue();
            project.SetLumpSumTerms(
                Percentage.FromPercent(25m), Percentage.FromPercent(holdPercent), Percentage.FromPercent(25m),
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

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/sign", _owner, new { signedOn = "2026-09-24", holdPercentage = (holdPercent / 100m).ToString(System.Globalization.CultureInfo.InvariantCulture), clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
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

        Client individual = Client.Create(
            UniqueNames.Code("EX-I"), "عميل فرد", UniqueNames.Phone(), ClientKind.Individual, Now).Value;
        Account taxWithheld = Account.Create(
            AccountType.TaxWithheldAtSource, UniqueNames.Code("EXTAX"), "ضريبة خصم من المنبع", "Tax withheld at source",
            Currency.Egp, new DateOnly(2026, 1, 1)).Value;

        context.Clients.AddRange(client, individual);
        context.Babs.Add(bab);
        context.Accounts.AddRange(safe, taxWithheld);
        context.Users.AddRange(owner, finance, financeUnassigned, technicalOffice, technicalOffice2, prep, supervisor, engineerUnassigned, hr);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _individualClientId = individual.Id;
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
