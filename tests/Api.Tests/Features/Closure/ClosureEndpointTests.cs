using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Authorization;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests.Features.Closure;

/// <summary>
/// KAFF-800 to KAFF-807 over HTTP. The rule this suite exists for is spec.md section 5.1: the hold only grows, and releases
/// once, in full, at handover. So the hold balance and the count of HoldRelease postings are asserted at every stage, not only
/// the amount at the end: a release one state early is the defect the rule is there to prevent.
/// </summary>
/// <remarks>
/// Every project here is its own: a Lump Sum project with a 1,000,000 contract value whose hold already holds 200,000,
/// accrued by three postings (60,000, 80,000 and 60,000, the sums of spec.md section 15). The accruals are written
/// straight to the ledger so the suite does not depend on the extract chain; the amount released is read back from them.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ClosureEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _subcontractorId;
    private Guid _owner;
    private Guid _finance;
    private Guid _financeUnassigned;
    private Guid _technicalOffice;
    private Guid _technicalOfficeUnassigned;
    private Guid _supervisor;
    private Guid _prepEngineer;
    private Guid _hr;

    public ClosureEndpointTests(PostgresDatabase database) => _database = database;

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

    // ---- KAFF-800 · practical completion and the snag list -------------------------------------

    [Fact]
    public async Task Practical_completion_moves_only_an_active_project_and_touches_no_money()
    {
        Guid active = await NewProjectAsync(ProjectStatus.Active);
        long postings = await PostingCountAsync(active);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{active}/practical-completion", _technicalOffice, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusAsync(active)).Should().Be(ProjectStatus.HandoverPending);
        (await PostingCountAsync(active)).Should().Be(postings, "practical completion posts nothing");
        (await HoldAsync(active)).Should().Be(200_000m, "nothing leaves the hold at practical completion");

        foreach (ProjectStatus other in new[] { ProjectStatus.Setup, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.Stopped })
        {
            Guid project = await NewProjectAsync(other);
            HttpResponseMessage response = await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/practical-completion", _technicalOffice, null);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict, $"from {other}");
            (await StatusAsync(project)).Should().Be(other);
        }
    }

    [Fact]
    public async Task A_snag_needs_all_five_fields_and_walks_open_fixed_verified_without_skipping()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);
        string snags = $"/api/projects/{project}/snags";

        (await SendAsync(HttpMethod.Post, snags, _technicalOffice, new { description = "crack", location = "kitchen", ownerKind = "Kaff" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "no priority");
        (await SendAsync(HttpMethod.Post, snags, _technicalOffice, new { description = "crack", ownerKind = "Kaff", priority = "Major" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "no location");
        (await SnagCountAsync(project)).Should().Be(0);

        Guid snag = await RaiseSnagAsync(project, SnagPriority.Major);
        (await SendAsync(HttpMethod.Post, $"{snags}/{snag}/verification", _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "open to verified skips fixed");
        (await SendAsync(HttpMethod.Post, $"{snags}/{snag}/fix", _supervisor, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"{snags}/{snag}/fix", _supervisor, null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"{snags}/{snag}/verification", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument list = JsonDocument.Parse(await (await SendAsync(HttpMethod.Get, snags, _technicalOffice, null)).Content.ReadAsStringAsync(Ct));
        list.RootElement.EnumerateArray().Single().GetProperty("status").GetString().Should().Be("Verified");
    }

    [Fact]
    public async Task A_design_project_has_no_snag_list_no_handover_and_no_release()
    {
        Guid design = await NewDesignProjectAsync();

        (await SendAsync(HttpMethod.Post, $"/api/projects/{design}/snags", _technicalOffice,
            new { description = "x", location = "y", ownerKind = "Kaff", priority = "Major" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{design}/practical-completion", _technicalOffice, null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{design}/handover", _technicalOffice, null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{design}/hold-release", _finance, null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{design}/close", _owner, null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StatusAsync(design)).Should().Be(ProjectStatus.Active);
    }

    // ---- KAFF-800/802 · major snags block handover; the hold is not touched until the release ------

    [Fact]
    public async Task A_major_snag_blocks_handover_by_name_until_verified_and_a_minor_one_does_not()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);
        Guid minor = await RaiseSnagAsync(project, SnagPriority.Minor);
        Guid major = await RaiseSnagAsync(project, SnagPriority.Major);
        string handover = $"/api/projects/{project}/handover";

        HttpResponseMessage blocked = await SendAsync(HttpMethod.Post, handover, _technicalOffice, null);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (JsonDocument problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync(Ct)))
        {
            problem.RootElement.GetProperty("code").GetString().Should().Be("closure.major_snags_open");
            problem.RootElement.GetProperty("snags").EnumerateArray().Select(number => number.GetInt32()).Should().Equal(2);
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{major}/fix", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, handover, _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "a fixed major snag is not yet verified (Q115)");
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{major}/verification", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await StatusAsync(project)).Should().Be(ProjectStatus.HandoverPending);
        (await SendAsync(HttpMethod.Post, handover, _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.OK, "the minor snag is still open and does not block");
        (await StatusAsync(project)).Should().Be(ProjectStatus.Handover);
        (await SnagStatusAsync(minor)).Should().Be(SnagStatus.Open);
    }

    /// <summary>
    /// The central test. The hold is 200,000 and is asserted at every stage: it does not move at practical completion, it
    /// does not move when a snag is raised, fixed, verified or resolved, it does not move at handover, and it leaves only
    /// at the release, in one HoldRelease posting of exactly the accrued sum.
    /// </summary>
    [Fact]
    public async Task The_hold_does_not_move_until_the_release_which_is_once_and_in_full()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Active);
        string root = $"/api/projects/{project}";

        // The stage, asserted: before handover there is no HoldRelease posting and the release is refused.
        await AssertHoldIntactAsync(project);
        (await SendAsync(HttpMethod.Post, $"{root}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "Active");
        await AssertHoldIntactAsync(project);

        (await SendAsync(HttpMethod.Post, $"{root}/practical-completion", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertHoldIntactAsync(project);
        HttpResponseMessage atPending = await SendAsync(HttpMethod.Post, $"{root}/hold-release", _finance, null);
        atPending.StatusCode.Should().Be(HttpStatusCode.Conflict, "HandoverPending");
        (await ErrorCodeAsync(atPending)).Should().Be("treasury.posting_not_allowed_in_project_state");
        await AssertHoldIntactAsync(project);

        // A snag, in every state of its life, is not a way into the hold.
        Guid snag = await RaiseSnagAsync(project, SnagPriority.Major, SnagOwnerKind.Subcontractor);
        long postings = await PostingCountAsync(project);
        await AssertHoldIntactAsync(project);
        (await SendAsync(HttpMethod.Post, $"{root}/snags/{snag}/fix", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"{root}/snags/{snag}/verification", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"{root}/snags/{snag}/resolution", _technicalOffice,
            new { kind = "DebitNote", reason = "subcontractor defect" })).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertHoldIntactAsync(project);
        (await PostingCountAsync(project)).Should().Be(postings, "resolving a snag posts nothing");

        (await SendAsync(HttpMethod.Post, $"{root}/handover", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertHoldIntactAsync(project);
        (await PostingCountAsync(project)).Should().Be(postings, "handing over posts nothing: the release is its own act");

        // The release: one posting, the whole hold, no amount in the request.
        HttpResponseMessage released = await SendAsync(HttpMethod.Post, $"{root}/hold-release", _finance, null);
        released.StatusCode.Should().Be(HttpStatusCode.OK, await released.Content.ReadAsStringAsync(Ct));
        using (JsonDocument body = JsonDocument.Parse(await released.Content.ReadAsStringAsync(Ct)))
        {
            decimal.Parse(body.RootElement.GetProperty("released").GetString()!, CultureInfo.InvariantCulture).Should().Be(200_000m);
        }

        (await HoldAsync(project)).Should().Be(0m, "the release leaves the hold at exactly zero");
        (await HoldReleaseCountAsync(project)).Should().Be(1);
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Posting release = await reader.Postings.SingleAsync(
                posting => posting.ProjectId == project && posting.Type == PostingType.HoldRelease, Ct);
            List<Money> accruals = await reader.Postings
                .Where(posting => posting.ProjectId == project && posting.Type == PostingType.HoldAccrual)
                .Select(posting => posting.Amount)
                .ToListAsync(Ct);
            decimal accrued = accruals.Sum(amount => amount.Amount);
            release.Amount.Amount.Should().Be(accrued, "the release equals the accrued hold, summed from postings");
            release.ReversesId.Should().BeNull();
            release.SourceDocumentType.Should().Be(SourceDocumentType.ProjectHandover);
        }

        // Once: a second release is refused and the hold is still zero.
        HttpResponseMessage again = await SendAsync(HttpMethod.Post, $"{root}/hold-release", _finance, null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(again)).Should().Be("closure.hold_already_released");
        (await HoldAsync(project)).Should().Be(0m);
        (await HoldReleaseCountAsync(project)).Should().Be(1);
    }

    [Fact]
    public async Task The_hold_is_never_debited_before_handover_from_any_pre_handover_state()
    {
        foreach (ProjectStatus state in new[] { ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Stopped, ProjectStatus.Terminated })
        {
            Guid project = await NewProjectAsync(state);
            long postings = await PostingCountAsync(project);

            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict, $"{state}");
            (await HoldAsync(project)).Should().Be(200_000m, $"{state}: the hold is unchanged");
            (await HoldReleaseCountAsync(project)).Should().Be(0, $"{state}");
            (await PostingCountAsync(project)).Should().Be(postings, $"{state}: no posting was written");
        }
    }

    [Fact]
    public async Task A_warranty_callback_and_a_closure_attempt_leave_the_hold_alone()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Handover);
        long postings = await PostingCountAsync(project);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/warranty-callbacks", _technicalOffice, new { description = "leak" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", _owner, null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "Handover is not UnderWarranty");

        (await HoldAsync(project)).Should().Be(200_000m);
        (await PostingCountAsync(project)).Should().Be(postings);
    }

    [Fact]
    public async Task Open_minor_snags_do_not_stop_the_whole_hold_being_released()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);
        await RaiseSnagAsync(project, SnagPriority.Minor);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/handover", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await HoldAsync(project)).Should().Be(0m);
    }

    /// <summary>
    /// spec.md section 6.4: the five ledgers never net against each other. All five are read before and after the release:
    /// client advance and firm advance on the project, عهدة and the owner current account at company level, and the hold.
    /// Only the hold moves. Each of the other four is non-zero beforehand, so "unchanged" is not satisfied by emptiness.
    /// </summary>
    [Fact]
    public async Task The_release_moves_the_hold_alone_and_none_of_the_other_four_ledgers()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Handover, advance: 250_000m);
        (Guid firm, Guid petty, Guid ownerCurrent, Guid safe) = await SeedOtherLedgersAsync(project);
        Guid hold = await AccountIdAsync(project, AccountType.Hold);
        Guid clientAdvance = await AccountIdAsync(project, AccountType.ClientAdvance);
        Guid receivable = await AccountIdAsync(project, AccountType.ClientReceivable);
        (string, Guid)[] accounts =
        [
            ("ClientAdvance", clientAdvance), ("Hold", hold), ("FirmAdvance", firm), ("PettyCash", petty),
            ("OwnerCurrent", ownerCurrent), ("Safe", safe), ("Receivable", receivable),
        ];

        Dictionary<string, decimal> before = await ReadLedgersAsync(accounts);
        before["ClientAdvance"].Should().Be(250_000m);
        before["Hold"].Should().Be(200_000m);
        new[] { before["FirmAdvance"], before["PettyCash"], before["OwnerCurrent"] }.Should().OnlyContain(balance => balance != 0m);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        Dictionary<string, decimal> after = await ReadLedgersAsync(accounts);
        after["Hold"].Should().Be(0m);
        foreach (string untouched in new[] { "ClientAdvance", "FirmAdvance", "PettyCash", "OwnerCurrent", "Safe" })
        {
            after[untouched].Should().Be(before[untouched], $"{untouched} is not netted against the hold");
        }

        (after["Receivable"] - before["Receivable"]).Should().Be(200_000m, "the released hold becomes what the client owes (spec.md section 15)");
    }

    [Fact]
    public async Task The_release_is_allowed_while_under_warranty_too()
    {
        Guid project = await NewProjectAsync(ProjectStatus.UnderWarranty);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await HoldAsync(project)).Should().Be(0m);
    }

    [Fact]
    public async Task A_release_with_no_hold_has_nothing_to_release()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Handover, hold: 0m);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("closure.nothing_to_release");
    }

    // ---- handover stamps the warranty clock; the audit trail -------------------------------------

    [Fact]
    public async Task Handover_stamps_the_handover_date_and_the_warranty_end_four_months_later_and_is_audited()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/handover", _technicalOffice, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        DateOnly handoverOn = DateOnly.Parse(body.RootElement.GetProperty("handoverOn").GetString()!, CultureInfo.InvariantCulture);
        DateOnly warrantyEnds = DateOnly.Parse(body.RootElement.GetProperty("warrantyEndsOn").GetString()!, CultureInfo.InvariantCulture);
        warrantyEnds.Should().Be(handoverOn.AddMonths(4));

        await using KaffDbContext reader = _database.CreateBareContext();
        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == project && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);
        record.ActorUserId.Should().Be(_technicalOffice);
        record.ChangedProperties.Should().Contain(nameof(Project.Status));
    }

    [Fact]
    public async Task The_release_writes_its_postings_with_who_and_when()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Handover);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting release = await reader.Postings.SingleAsync(posting => posting.ProjectId == project && posting.Type == PostingType.HoldRelease, Ct);
        release.CreatedByUserId.Should().Be(_finance);
        release.CreatedAt.Should().BeAfter(Now);
        (await reader.AuditRecords.AnyAsync(record => record.EntityId == release.Id, Ct)).Should().BeTrue();
    }

    // ---- KAFF-803 · snag resolution ---------------------------------------------------------------

    [Fact]
    public async Task A_snag_resolution_stores_the_choice_and_the_reason_and_a_debit_note_needs_a_subcontractor_owner()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);
        Guid kaffs = await RaiseSnagAsync(project, SnagPriority.Minor);
        Guid subs = await RaiseSnagAsync(project, SnagPriority.Minor, SnagOwnerKind.Subcontractor);
        string snags = $"/api/projects/{project}/snags";

        (await SendAsync(HttpMethod.Post, $"{snags}/{kaffs}/resolution", _technicalOffice, new { kind = "Absorbed", reason = "ours" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "not fixed yet");
        await SendAsync(HttpMethod.Post, $"{snags}/{kaffs}/fix", _technicalOffice, null);
        await SendAsync(HttpMethod.Post, $"{snags}/{subs}/fix", _technicalOffice, null);

        (await SendAsync(HttpMethod.Post, $"{snags}/{kaffs}/resolution", _technicalOffice, new { kind = "DebitNote", reason = "theirs" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "Kaff owns the snag");
        (await SendAsync(HttpMethod.Post, $"{snags}/{kaffs}/resolution", _technicalOffice, new { kind = "Absorbed", reason = "  " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a reason is required");
        (await SendAsync(HttpMethod.Post, $"{snags}/{kaffs}/resolution", _technicalOffice, new { kind = "Absorbed", reason = "ours" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"{snags}/{subs}/resolution", _technicalOffice, new { kind = "DebitNote", reason = "their defect" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Snag stored = await reader.Snags.SingleAsync(snag => snag.Id == subs, Ct);
        stored.ResolutionKind.Should().Be(SnagResolutionKind.DebitNote);
        stored.ResolutionReason.Should().Be("their defect");
        stored.ResolvedByUserId.Should().Be(_technicalOffice);
        (await HoldAsync(project)).Should().Be(200_000m);
    }

    // ---- KAFF-804 · warranty and callbacks ---------------------------------------------------------

    [Fact]
    public async Task A_callback_attaches_to_a_running_warranty_and_is_refused_without_one()
    {
        Guid active = await NewProjectAsync(ProjectStatus.Active);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{active}/warranty-callbacks", _technicalOffice, new { description = "leak" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        Guid warranty = await NewProjectAsync(ProjectStatus.UnderWarranty);
        HttpResponseMessage raised = await SendAsync(
            HttpMethod.Post, $"/api/projects/{warranty}/warranty-callbacks", _supervisor, new { description = "leak in the bathroom" });
        raised.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument body = JsonDocument.Parse(await raised.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();
        (await SendAsync(HttpMethod.Post, $"/api/projects/{warranty}/warranty-callbacks/{id}/resolution", _technicalOffice, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{warranty}/warranty-callbacks/{id}/resolution", _technicalOffice, null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- KAFF-807 · closing a project ---------------------------------------------------------------

    [Fact]
    public async Task A_project_closes_only_when_the_ledgers_read_settled_and_the_refusal_names_every_unmet_condition()
    {
        Guid project = await NewProjectAsync(ProjectStatus.UnderWarranty, advance: 250_000m);
        string close = $"/api/projects/{project}/close";

        HttpResponseMessage both = await SendAsync(HttpMethod.Post, close, _owner, null);
        both.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (JsonDocument problem = JsonDocument.Parse(await both.Content.ReadAsStringAsync(Ct)))
        {
            problem.RootElement.GetProperty("unmet").EnumerateArray().Select(item => item.GetString())
                .Should().Equal("AdvanceRecovered", "HoldReleased");
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage advanceOnly = await SendAsync(HttpMethod.Post, close, _owner, null);
        advanceOnly.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (JsonDocument problem = JsonDocument.Parse(await advanceOnly.Content.ReadAsStringAsync(Ct)))
        {
            problem.RootElement.GetProperty("unmet").EnumerateArray().Select(item => item.GetString())
                .Should().Equal("AdvanceRecovered");
        }

        (await StatusAsync(project)).Should().Be(ProjectStatus.UnderWarranty);
    }

    [Fact]
    public async Task A_settled_project_closes_stamps_the_date_and_afterwards_accepts_collections_only()
    {
        Guid project = await NewProjectAsync(ProjectStatus.UnderWarranty);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", _owner, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the hold is still 200,000");
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage closed = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", _owner, null);

        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync(Ct));
        (await StatusAsync(project)).Should().Be(ProjectStatus.Closed);
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.SingleAsync(candidate => candidate.Id == project, Ct)).ClosedOn.Should().NotBeNull();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityId == project && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);
        record.ActorUserId.Should().Be(_owner);
        record.Reason.Should().Contain("hold 0.0000", "the readings of each condition are on the record");

        // A second release is still refused after closing: Closed accepts collections only.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_has_one_legal_source_state_and_it_is_under_warranty()
    {
        foreach (ProjectStatus other in new[] { ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.Stopped, ProjectStatus.Terminated })
        {
            Guid project = await NewProjectAsync(other, hold: 0m);

            (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", _owner, null)).StatusCode
                .Should().Be(HttpStatusCode.Conflict, $"from {other}");
            (await StatusAsync(project)).Should().Be(other);
        }
    }

    // ---- Terminated is unruled ---------------------------------------------------------------------

    [Fact]
    public async Task Terminating_refuses_with_the_translated_error_changes_nothing_and_writes_no_posting()
    {
        foreach (ProjectStatus state in new[] { ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Stopped })
        {
            Guid project = await NewProjectAsync(state);
            long postings = await PostingCountAsync(project);

            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/termination", _owner, new { reason = "client left" });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict, $"{state}");
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            problem.RootElement.GetProperty("code").GetString().Should().Be("closure.termination_settlement_unruled");
            problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.closure.termination_settlement_unruled");
            (await StatusAsync(project)).Should().Be(state);
            (await PostingCountAsync(project)).Should().Be(postings);
            (await HoldAsync(project)).Should().Be(200_000m);
        }
    }

    // ---- KAFF-807-G · after closing, only collections post ------------------------------------------

    /// <summary>
    /// The hold here was never released, so "already released" cannot be the reason for the refusal: a release attempted
    /// on a Closed project with its hold intact is refused by the project-state policy and by nothing else. The control
    /// is the same hold on an UnderWarranty project, released by the same call (<c>The_release_is_allowed_while_under_warranty_too</c>).
    /// </summary>
    [Fact]
    public async Task A_closed_project_refuses_a_hold_release_because_of_its_state_and_not_because_one_happened_before()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Closed);
        (await StatusAsync(project)).Should().Be(ProjectStatus.Closed);
        (await HoldAsync(project)).Should().Be(200_000m);
        (await HoldReleaseCountAsync(project)).Should().Be(0, "no release was ever written, so the already-released check has nothing to find");
        long postings = await PostingCountAsync(project);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("treasury.posting_not_allowed_in_project_state");
        (await HoldAsync(project)).Should().Be(200_000m);
        (await PostingCountAsync(project)).Should().Be(postings);
    }

    // ---- the audit trail: who, when, what changed before and after, and why where the flow gives a reason ----

    [Fact]
    public async Task A_snag_raised_fixed_and_verified_writes_an_audit_row_per_step_with_who_when_and_the_status_before_and_after()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);
        Guid snag = await RaiseSnagAsync(project, SnagPriority.Major);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{snag}/fix", _supervisor, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{snag}/verification", _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        List<AuditRecord> rows = await AuditOfAsync(snag);

        rows.Select(row => row.Action).Should().Equal(AuditAction.Created, AuditAction.Modified, AuditAction.Modified);
        rows.Select(row => row.ActorUserId).Should().Equal(_technicalOffice, _supervisor, _technicalOffice);
        rows.Should().OnlyContain(row => row.EntityType == nameof(Snag) && row.ProjectId == project && row.OccurredAt > Now);
        rows[0].AfterJson.Should().Contain("crack in the wall").And.Contain("Open");
        rows[1].ChangedProperties.Should().Contain(nameof(Snag.Status));
        rows[1].BeforeJson.Should().Contain("Open");
        rows[1].AfterJson.Should().Contain("Fixed");
        rows[2].ChangedProperties.Should().Contain(nameof(Snag.Status));
        rows[2].BeforeJson.Should().Contain("Fixed");
        rows[2].AfterJson.Should().Contain("Verified");
    }

    [Fact]
    public async Task A_snag_resolution_writes_an_audit_row_with_who_the_choice_and_the_reason()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending);
        Guid snag = await RaiseSnagAsync(project, SnagPriority.Minor, SnagOwnerKind.Subcontractor);
        await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{snag}/fix", _technicalOffice, null);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{snag}/resolution", _technicalOffice,
            new { kind = "DebitNote", reason = "their defect" })).StatusCode.Should().Be(HttpStatusCode.OK);

        AuditRecord last = (await AuditOfAsync(snag)).Last();

        last.Action.Should().Be(AuditAction.Modified);
        last.ActorUserId.Should().Be(_technicalOffice);
        last.OccurredAt.Should().BeAfter(Now);
        last.ChangedProperties.Should().Contain([nameof(Snag.ResolutionKind), nameof(Snag.ResolutionReason)]);
        last.AfterJson.Should().Contain("DebitNote").And.Contain("their defect");
        last.Reason.Should().Be("their defect", "a reason the flow asks for is on the audit record, not only on the snag");
    }

    [Fact]
    public async Task A_callback_raised_and_resolved_writes_an_audit_row_per_step_with_who_when_and_what_changed()
    {
        Guid project = await NewProjectAsync(ProjectStatus.UnderWarranty);
        HttpResponseMessage raised = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/warranty-callbacks", _supervisor, new { description = "leak in the bathroom" });
        raised.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument body = JsonDocument.Parse(await raised.Content.ReadAsStringAsync(Ct));
        Guid callback = body.RootElement.GetProperty("id").GetGuid();
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/warranty-callbacks/{callback}/resolution", _technicalOffice, null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        List<AuditRecord> rows = await AuditOfAsync(callback);

        rows.Select(row => row.Action).Should().Equal(AuditAction.Created, AuditAction.Modified);
        rows.Select(row => row.ActorUserId).Should().Equal(_supervisor, _technicalOffice);
        rows.Should().OnlyContain(row => row.EntityType == nameof(WarrantyCallback) && row.ProjectId == project && row.OccurredAt > Now);
        rows[0].AfterJson.Should().Contain("leak in the bathroom");
        rows[1].ChangedProperties.Should().Contain([nameof(WarrantyCallback.ResolvedByUserId), nameof(WarrantyCallback.ResolvedAt)]);
    }

    [Fact]
    public async Task The_release_posting_is_audited_with_who_when_and_the_amount()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Handover);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting release = await reader.Postings.SingleAsync(posting => posting.ProjectId == project && posting.Type == PostingType.HoldRelease, Ct);
        AuditRecord row = await reader.AuditRecords.SingleAsync(record => record.EntityId == release.Id && record.EntityType == nameof(Posting), Ct);

        row.Action.Should().Be(AuditAction.Created);
        row.ActorUserId.Should().Be(_finance);
        row.OccurredAt.Should().BeAfter(Now);
        row.ProjectId.Should().Be(project);
        row.AfterJson.Should().Contain("200000").And.Contain("HoldRelease");
    }

    /// <summary>
    /// AC-802-M: the trail of a released hold carries "the project state". Nothing records it today: the release changes
    /// no project row, so the only audit record is the posting's own, and a posting does not carry the status of the
    /// project it was written against. Product gap, routed to the Scrum Master: the release handler would have to set
    /// the audit reason (as CloseProject does) or the posting would have to snapshot the state. Skipped, not deleted, so
    /// the criterion stays visible; remove the Skip when the product records it.
    /// </summary>
    [Fact(Skip = "PRODUCT GAP AC-802-M: the release's audit trail does not record the project state. See the remarks.")]
    public async Task The_release_audit_trail_records_the_project_state()
    {
        Guid project = await NewProjectAsync(ProjectStatus.Handover);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<AuditRecord> trail = await reader.AuditRecords.Where(record => record.ProjectId == project).ToListAsync(Ct);

        trail.Where(record => record.OccurredAt > Now && record.EntityType == nameof(Posting))
            .Should().Contain(record => (record.Reason ?? string.Empty).Contains(nameof(ProjectStatus.Handover), StringComparison.Ordinal)
                || (record.AfterJson ?? string.Empty).Contains(nameof(ProjectStatus.Handover), StringComparison.Ordinal));
    }

    // ---- snag edits after the project closes -------------------------------------------------------

    /// <summary>
    /// Snag.Fix, Verify and Resolve consult only the snag's own status, never the project's. A Closed project's snag can
    /// still be moved. Whether a snag edit should consult <c>RulingSet.PostingProjectStates</c> or a snag-specific rule is
    /// a design question for the Scrum Master and the Architect; this test states the expectation that a closed project's
    /// snag is frozen and is skipped until that is ruled. It was run un-skipped and failed with 200 where 409 was expected.
    /// </summary>
    [Fact(Skip = "PRODUCT DEFECT (minor, design question): snag Fix/Verify/Resolve ignore the project state, so a Closed project's snags can still be edited.")]
    public async Task A_closed_projects_snags_can_no_longer_be_fixed_verified_or_resolved()
    {
        Guid project = await NewProjectAsync(ProjectStatus.HandoverPending, hold: 0m);
        Guid open = await RaiseSnagAsync(project, SnagPriority.Minor);
        Guid fixedOne = await RaiseSnagAsync(project, SnagPriority.Minor);
        await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{fixedOne}/fix", _technicalOffice, null);
        foreach (string step in new[] { "handover", "warranty" })
        {
            (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/{step}", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/close", _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusAsync(project)).Should().Be(ProjectStatus.Closed);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{open}/fix", _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{fixedOne}/verification", _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags/{fixedOne}/resolution", _technicalOffice,
            new { kind = "Absorbed", reason = "late" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SnagStatusAsync(open)).Should().Be(SnagStatus.Open);
        (await SnagStatusAsync(fixedOne)).Should().Be(SnagStatus.Fixed);
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private async Task<List<AuditRecord>> AuditOfAsync(Guid entityId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.AuditRecords.Where(record => record.EntityId == entityId).OrderBy(record => record.OccurredAt).ToListAsync(Ct);
    }

    private async Task<Guid> AccountIdAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Accounts.Where(account => account.ProjectId == project && account.Type == type).Select(account => account.Id).SingleAsync(Ct);
    }

    private async Task<Dictionary<string, decimal>> ReadLedgersAsync((string Name, Guid Account)[] accounts)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        Dictionary<string, decimal> read = [];
        foreach ((string name, Guid account) in accounts)
        {
            read[name] = (await reader.AccountBalances.SingleAsync(balance => balance.AccountId == account, Ct)).SignedBalance.Amount;
        }

        return read;
    }

    /// <summary>
    /// Gives the project a firm advance, and creates its own safe, عهدة and owner current accounts (company level) with a
    /// balance each, so the five ledgers of spec.md section 6.4 are all non-zero around a release. The safe is funded by an
    /// owner injection first, so no posting here can breach the safe's floor.
    /// </summary>
    private async Task<(Guid Firm, Guid Petty, Guid OwnerCurrent, Guid Safe)> SeedOtherLedgersAsync(Guid project)
    {
        await using KaffDbContext context = _database.CreateContext();
        DateOnly date = new(2026, 10, 1);
        Account safe = Account.Create(AccountType.Safe, UniqueNames.Code("CLS2"), "خزنة", "Safe", Currency.Egp, date).Value;
        Account ownerCurrent = Account.Create(
            AccountType.OwnerCurrentAccount, UniqueNames.Code("CLOCA"), "جاري المالك", "Owner current", Currency.Egp, date).Value;
        Account petty = Account.Create(
            AccountType.PettyCashAdvance, UniqueNames.Code("CLPC"), "عهدة", "Petty cash", Currency.Egp, date,
            projectId: project, partyType: PartyType.Employee, partyId: Guid.CreateVersion7()).Value;
        Account firm = await context.Accounts.SingleAsync(account => account.ProjectId == project && account.Type == AccountType.FirmAdvance, Ct);
        context.Accounts.AddRange(safe, ownerCurrent, petty);
        await context.SaveChangesAsync(Ct);

        // from = credited side, to = debited side, as everywhere in this suite.
        context.Postings.Add(Posting.Create(
            ownerCurrent, safe, new Money(500_000m), PostingType.OwnerInjection,
            new SourceDocument(SourceDocumentType.Payment, Guid.CreateVersion7(), "INJ"), date, _owner, Now).Value);
        await context.SaveChangesAsync(Ct);
        context.Postings.Add(Posting.Create(
            safe, firm, new Money(30_000m), PostingType.FirmAdvanceIssue,
            new SourceDocument(SourceDocumentType.FirmAdvanceRequest, Guid.CreateVersion7(), "FADV"), date, _owner, Now, project).Value);
        context.Postings.Add(Posting.Create(
            safe, petty, new Money(10_000m), PostingType.PettyCashIssue,
            new SourceDocument(SourceDocumentType.PettyCashRequest, Guid.CreateVersion7(), "PC"), date, _owner, Now, project).Value);
        await context.SaveChangesAsync(Ct);

        return (firm.Id, petty.Id, ownerCurrent.Id, safe.Id);
    }

    private async Task AssertHoldIntactAsync(Guid project)
    {
        (await HoldAsync(project)).Should().Be(200_000m, "the hold only grows until the release");
        (await HoldReleaseCountAsync(project)).Should().Be(0, "no HoldRelease posting exists before the release");
    }

    private async Task<Guid> RaiseSnagAsync(Guid project, SnagPriority priority, SnagOwnerKind owner = SnagOwnerKind.Kaff)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/snags", _technicalOffice, new
        {
            description = "crack in the wall",
            location = "kitchen",
            ownerKind = owner.ToString(),
            subcontractorId = owner == SnagOwnerKind.Subcontractor ? _subcontractorId : (Guid?)null,
            priority = priority.ToString(),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
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

    private async Task<ProjectStatus> StatusAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.Where(candidate => candidate.Id == project).Select(candidate => candidate.Status).SingleAsync(Ct);
    }

    private async Task<SnagStatus> SnagStatusAsync(Guid snag)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Snags.Where(candidate => candidate.Id == snag).Select(candidate => candidate.Status).SingleAsync(Ct);
    }

    private async Task<long> SnagCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Snags.LongCountAsync(snag => snag.ProjectId == project, Ct);
    }

    private async Task<long> PostingCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project, Ct);
    }

    private async Task<long> HoldReleaseCountAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project && posting.Type == PostingType.HoldRelease, Ct);
    }

    private Task<decimal> HoldAsync(Guid project) => BalanceAsync(project, AccountType.Hold);

    private async Task<decimal> BalanceAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return (await reader.AccountBalances.SingleAsync(balance => balance.ProjectId == project && balance.Type == type, Ct)).SignedBalance.Amount;
    }

    private static readonly Dictionary<ProjectStatus, ProjectStatus[]> Paths = new()
    {
        [ProjectStatus.Setup] = [],
        [ProjectStatus.Active] = [ProjectStatus.Active],
        [ProjectStatus.HandoverPending] = [ProjectStatus.Active, ProjectStatus.HandoverPending],
        [ProjectStatus.Handover] = [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover],
        [ProjectStatus.UnderWarranty] =
            [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty],
        [ProjectStatus.Stopped] = [ProjectStatus.Active, ProjectStatus.Stopped],
        [ProjectStatus.Terminated] = [ProjectStatus.Active, ProjectStatus.Terminated],
        [ProjectStatus.Closed] =
            [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty, ProjectStatus.Closed],
    };

    /// <summary>
    /// A Lump Sum project in <paramref name="status"/> whose hold already holds <paramref name="hold"/>, accrued by three
    /// postings while the project was Active, and whose client advance holds <paramref name="advance"/>.
    /// </summary>
    private async Task<Guid> NewProjectAsync(ProjectStatus status, decimal hold = 200_000m, decimal advance = 0m)
    {
        if (status == ProjectStatus.Setup)
        {
            return await NewSetupProjectAsync(hold);
        }

        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("CL-P"), "مشروع إقفال", _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2026, 9, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        foreach (Guid userId in new[] { _finance, _technicalOffice, _supervisor, _prepEngineer })
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Ct);
            AssignmentLevel level = userId == _supervisor ? AssignmentLevel.Supervisor
                : userId == _prepEngineer ? AssignmentLevel.Junior : AssignmentLevel.Standard;
            context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, level, _owner, Now).Value);
        }

        IReadOnlyList<Account> accounts = ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), new DateOnly(2026, 9, 1)).Value;
        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync(Ct);

        // The accruals happen while the project is Active, as the extract chain would write them.
        project.TransitionTo(ProjectStatus.Active, new DateOnly(2026, 9, 1)).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);

        Account receivable = accounts.Single(account => account.Type == AccountType.ClientReceivable);
        Account holdAccount = accounts.Single(account => account.Type == AccountType.Hold);
        if (hold > 0m)
        {
            foreach (decimal part in new[] { hold * 0.3m, hold * 0.4m, hold * 0.3m })
            {
                context.Postings.Add(Posting.Create(
                    receivable, holdAccount, new Money(part), PostingType.HoldAccrual,
                    new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT"),
                    new DateOnly(2026, 9, 30), _owner, Now, project.Id).Value);
            }
        }

        if (advance > 0m)
        {
            Account advanceAccount = accounts.Single(account => account.Type == AccountType.ClientAdvance);
            Account safe = await context.Accounts.Where(account => account.Type == AccountType.Safe).OrderBy(account => account.Code).FirstAsync(Ct);
            context.Postings.Add(Posting.Create(
                advanceAccount, safe, new Money(advance), PostingType.ClientAdvanceReceipt,
                new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "SIGNING"),
                new DateOnly(2026, 9, 1), _owner, Now, project.Id).Value);
        }

        await context.SaveChangesAsync(Ct);

        foreach (ProjectStatus step in Paths[status].Skip(1))
        {
            project.TransitionTo(step, new DateOnly(2026, 10, 1), "test").IsSuccess.Should().BeTrue($"{step}");
        }

        await context.SaveChangesAsync(Ct);

        return project.Id;
    }

    /// <summary>A Setup project cannot hold an accrual written while Active, so its hold is written directly and the state never advances.</summary>
    private async Task<Guid> NewSetupProjectAsync(decimal hold)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("CL-S"), "مشروع إعداد", _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2026, 9, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        IReadOnlyList<Account> accounts = ProjectAccountSetFactory.Create(project, new HashSet<AccountType>(), new DateOnly(2026, 9, 1)).Value;
        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync(Ct);

        Account receivable = accounts.Single(account => account.Type == AccountType.ClientReceivable);
        Account holdAccount = accounts.Single(account => account.Type == AccountType.Hold);
        if (hold > 0m)
        {
            // PostingProjectStatePolicy refuses an extract-derived posting while the project is in Setup,
            // which is correct: no extract exists before the project is Active. A Setup project may
            // therefore only be asked for a zero hold.
            context.Postings.Add(Posting.Create(
                receivable, holdAccount, new Money(hold), PostingType.HoldAccrual,
                new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT"),
                new DateOnly(2026, 9, 30), _owner, Now, project.Id).Value);
        }

        await context.SaveChangesAsync(Ct);

        foreach (Guid userId in new[] { _finance, _technicalOffice })
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, AssignmentLevel.Standard, _owner, Now).Value);
        }

        await context.SaveChangesAsync(Ct);
        return project.Id;
    }

    private async Task<Guid> NewDesignProjectAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        Project project = Project.Create(UniqueNames.Code("CL-D"), "مشروع تصميم", _clientId, ContractType.Design, DateTimeOffset.UtcNow).Value;
        project.SetDesignTerms(100m, new Money(500m)).IsSuccess.Should().BeTrue();
        project.Sign(new DateOnly(2026, 9, 1), null).IsSuccess.Should().BeTrue();
        project.TransitionTo(ProjectStatus.Active, new DateOnly(2026, 9, 1)).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        foreach (Guid userId in new[] { _finance, _technicalOffice })
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == userId, Ct);
            context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, AssignmentLevel.Standard, _owner, Now).Value);
        }

        await context.SaveChangesAsync(Ct);
        return project.Id;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("CL-C"), "عميل الإقفال", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("CLSAFE"), "خزنة", "Safe", Currency.Egp, new DateOnly(2026, 1, 1)).Value;
        Subcontractor subcontractor = Subcontractor.Create(
            UniqueNames.Code("CL-SUB"), "مقاول إقفال", UniqueNames.Phone(), Now).Value;

        User owner = Make("cl-owner", Role.Owner);
        User finance = Make("cl-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User financeUnassigned = Make("cl-fin-un", Role.Finance, WellKnownDepartments.FinanceId);
        User office = Make("cl-to", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User officeUnassigned = Make("cl-to-un", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("cl-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User prep = Make("cl-prep", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = Make("cl-hr", Role.Hr, WellKnownDepartments.HrId);

        context.Clients.Add(client);
        context.Accounts.Add(safe);
        context.Subcontractors.Add(subcontractor);
        context.Users.AddRange(owner, finance, financeUnassigned, office, officeUnassigned, supervisor, prep, hr);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _subcontractorId = subcontractor.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _financeUnassigned = financeUnassigned.Id;
        _technicalOffice = office.Id;
        _technicalOfficeUnassigned = officeUnassigned.Id;
        _supervisor = supervisor.Id;
        _prepEngineer = prep.Id;
        _hr = hr.Id;
    }

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;
}
