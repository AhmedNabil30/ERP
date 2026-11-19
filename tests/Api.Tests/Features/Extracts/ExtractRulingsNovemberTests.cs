using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// Nabil's rulings of 2026-11-19 through the routes: D-239 (credit notes and the cheque list), the copy review's split of
/// the collection refusal, D-241 item 1 (the junior engineer's extracts list) and D-241 item 2 (the revised extract).
/// </summary>
public sealed partial class ExtractEndpointTests
{
    // ---- D-239 item 3: a credit note on a Collected extract cannot be reversed -------------------------

    [Fact]
    public async Task An_approved_credit_note_on_a_collected_extract_cannot_be_reversed_and_the_extract_stays_collected()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 40_000m);
        await OkAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 200_000m, 0m, "TRX-C"));
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        long before = await PostingCountAsync(project.Id);

        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "wrong amount" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync(Ct));
        (await ErrorCodeAsync(refused)).Should().Be("adjustment.extract_collected");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected, "the extract is never reopened by it");
        (await PostingCountAsync(project.Id)).Should().Be(before, "a refused reversal posts nothing");
    }

    // ---- D-239 item 4: the Owner reads the cheques in hand, and moves none ----------------------------

    [Fact]
    public async Task The_owner_reads_the_cheque_list_and_a_single_cheque_but_cannot_move_a_cheque()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 240_000m, 0m, "CHQ-O"));
        Guid cheque = await ChequeOfAsync(project.Id, "CHQ-O");

        (await SendAsync(HttpMethod.Get, Cheques(project.Id), _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"{Cheques(project.Id)}/{cheque}", _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (string step in new[] { "deposit", "clearance", "bounce" })
        {
            (await SendAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, step), _owner, new { reason = "x" })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, step);
        }

        foreach (Guid refused in new[] { _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Get, Cheques(project.Id), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Get, $"{Cheques(project.Id)}/{cheque}", refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    // ---- Copy review 2026-11-18: nothing remaining is its own refusal ---------------------------------

    [Fact]
    public async Task With_a_cheque_in_hand_for_the_whole_amount_a_further_payment_is_refused_as_nothing_remaining()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 240_000m, 0m, "CHQ-N"));

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cash", 1m, 0m, "N"));

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(refused)).Should().Be("collection.nothing_remaining");
    }

    // ---- D-241 item 1: the junior engineer reads the list with no money and prepares ------------------

    [Fact]
    public async Task The_junior_engineer_reads_the_extracts_list_with_every_money_field_null_and_prepares_the_next_extract()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ExtractStatus.Issued);

        HttpResponseMessage list = await SendAsync(HttpMethod.Get, Extracts(project.Id), _prepEngineer, null);
        list.StatusCode.Should().Be(HttpStatusCode.OK, await list.Content.ReadAsStringAsync(Ct));
        using (JsonDocument rows = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct)))
        {
            JsonElement row = rows.RootElement.EnumerateArray().Single();
            row.GetProperty("number").GetInt32().Should().Be(1);
            row.GetProperty("status").GetString().Should().Be(nameof(ExtractStatus.Issued));
            row.GetProperty("periodStart").GetString().Should().Be("2026-09-01");
            row.GetProperty("money").ValueKind.Should().Be(JsonValueKind.Null, "every money field is hidden from him");
        }

        (await SendAsync(HttpMethod.Get, Extracts(project.Id), _engineerUnassigned, null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "the role without the assignment is refused");

        HttpResponseMessage prepared = await SendAsync(
            HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-10-01", periodEnd = "2026-10-31" });
        prepared.StatusCode.Should().Be(HttpStatusCode.Created, await prepared.Content.ReadAsStringAsync(Ct));
    }

    // ---- D-241 item 2: a revised extract cancels the disputed original --------------------------------

    /// <summary>
    /// The chain end to end on a project with no hold: issue, dispute on quantity, revise (the original is Void and its
    /// accrual postings reversed by its own flow, with the reason stored and audited), then the revised extract for the
    /// same period through the whole chain again. The ledgers are read at every step.
    /// </summary>
    [Fact]
    public async Task A_quantity_dispute_revises_the_extract_the_original_is_cancelled_with_its_ledgers_back_and_the_revision_runs_the_chain()
    {
        Project project = await NewProjectAsync(holdPercent: 0m);
        AccountType[] ledgers =
            [AccountType.ClientReceivable, AccountType.ContractRevenue, AccountType.ClientAdvance, AccountType.MaterialAdvance, AccountType.Hold];
        Dictionary<AccountType, decimal> start = await LedgersAsync(project.Id, ledgers);

        Guid original = await DriveAsync(project, ExtractStatus.Issued);
        Math.Abs(await LedgerAsync(project.Id, AccountType.ContractRevenue)).Should().Be(300_000m, "the issued extract recognised 300,000");
        (await LedgerAsync(project.Id, AccountType.ClientReceivable)).Should().BeGreaterThan(0m);
        (await LedgerAsync(project.Id, AccountType.Hold)).Should().Be(0m, "this project holds nothing back");

        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute"), _finance, new { reason = "quantities overstated" });
        (await StatusOfAsync(original)).Should().Be(ExtractStatus.Disputed);

        HttpResponseMessage revised = await SendAsync(
            HttpMethod.Post, Step(project.Id, original, "dispute-resolution"), _finance,
            new { kind = "RevisedExtract", reason = "client measured 250 units" });
        revised.StatusCode.Should().Be(HttpStatusCode.OK, await revised.Content.ReadAsStringAsync(Ct));

        (await StatusOfAsync(original)).Should().Be(ExtractStatus.Void);
        (await LedgersAsync(project.Id, ledgers)).Should().Equal(start, "the original's postings are reversed by its own flow");
        (await AdjustmentCountAsync(project.Id)).Should().Be(0, "a revision raises no credit note");

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            List<Posting> postings = await reader.Postings
                .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract && posting.SourceDocumentId == original)
                .ToListAsync(Ct);
            List<Posting> originals = [.. postings.Where(posting => posting.ReversesId is null)];
            originals.Should().NotBeEmpty();
            postings.Where(posting => posting.ReversesId is not null).Select(posting => posting.ReversesId!.Value)
                .Should().BeEquivalentTo(originals.Select(posting => posting.Id), "every accrual posting has exactly one mirror");

            ExtractGateRecord gate = await reader.Set<ExtractGateRecord>()
                .SingleAsync(record => record.ExtractId == original && record.Reason == "client measured 250 units", Ct);
            gate.Gate.ToString().Should().Be("Cancellation");
            gate.ActorUserId.Should().Be(_finance);
            (await reader.AuditRecords.AnyAsync(a => a.EntityType == nameof(ExtractGateRecord) && a.EntityId == gate.Id, Ct))
                .Should().BeTrue("the cancellation writes audit with its stored reason");
        }

        (await SendAsync(HttpMethod.Post, Step(project.Id, original, "dispute-resolution"), _finance,
                new { kind = "RevisedExtract", reason = "again" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "a cancelled extract is never revised twice");

        // The corrected quantity, then the revised extract for the same period through the whole chain again.
        await ExecuteAsync(project, 250m);
        (await SendAsync(HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-10-01", periodEnd = "2026-10-31" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "a cancelled extract waits for its revision before the next normal one");
        Guid revision = await PrepareRevisionAsync(project, original);
        revision.Should().NotBe(original);
        JsonElement draft = await ReadAsync(project.Id, revision, _finance);
        draft.GetProperty("number").GetInt32().Should().Be(2);
        draft.GetProperty("periodStart").GetString().Should().Be("2026-09-01");
        draft.GetProperty("periodEnd").GetString().Should().Be("2026-09-30");
        (await LedgersAsync(project.Id, ledgers)).Should().Equal(start, "a draft posts nothing");

        await StepAsync(_client, project, revision, ExtractStatus.Draft, ExtractStatus.OwnerApproved);
        Math.Abs(await LedgerAsync(project.Id, AccountType.ContractRevenue)).Should().Be(250_000m, "the revision accrues at the Owner's approval");
        await StepAsync(_client, project, revision, ExtractStatus.OwnerApproved, ExtractStatus.Issued);
        (await StatusOfAsync(revision)).Should().Be(ExtractStatus.Issued);
        (await StatusOfAsync(original)).Should().Be(ExtractStatus.Void);
        Math.Abs(await LedgerAsync(project.Id, AccountType.ContractRevenue)).Should().Be(250_000m, "only the revision stands");
        (await LedgerAsync(project.Id, AccountType.Hold)).Should().Be(0m);
    }

    /// <summary>
    /// CLAUDE.md: the hold only grows. A cancellation would reverse the hold accrual, so an original that accrued a hold
    /// is refused before anything is written, and the extract stays disputed.
    /// </summary>
    [Fact]
    public async Task A_revision_is_refused_when_the_original_accrued_a_hold_and_nothing_changes()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "dispute"), _finance, new { reason = "quantities overstated" });
        List<string> before = await PostingSnapshotsAsync(project.Id);

        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "remeasured" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync(Ct));
        (await ErrorCodeAsync(refused)).Should().Be("extract.revision_hold_accrued");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Disputed);
        (await PostingSnapshotsAsync(project.Id)).Should().Equal(before);
    }

    [Fact]
    public async Task A_revision_is_refused_while_a_cheque_is_in_hand_on_the_original()
    {
        Project project = await NewProjectAsync(holdPercent: 0m);
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 10_000m, 0m, "CHQ-P"));
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "dispute"), _finance, new { reason = "quantities overstated" });

        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "remeasured" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync(Ct));
        (await ErrorCodeAsync(refused)).Should().Be("extract.revision_money_standing");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Disputed);
    }

    /// <summary>
    /// D-242 item 3: a payment received on the original stays posted and counts as already paid against the revision.
    /// Nothing is refunded and nothing is lost: the safe keeps the money at every step, and the revision is Collected
    /// once the rest arrives.
    /// </summary>
    [Fact]
    public async Task A_payment_on_the_cancelled_original_counts_as_already_paid_against_the_revision()
    {
        Project project = await NewProjectAsync(holdPercent: 0m);
        Guid original = await DriveAsync(project, ExtractStatus.Issued);
        decimal safeBefore = await NetIntoAsync(project.Id, AccountType.Safe);
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "collect"), _finance, Collect("Transfer", 100_000m, 0m, "TRX-PART"));
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute"), _finance, new { reason = "quantities overstated" });

        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "remeasured" });
        (await StatusOfAsync(original)).Should().Be(ExtractStatus.Void);
        (await NetIntoAsync(project.Id, AccountType.Safe)).Should().Be(safeBefore + 100_000m, "nothing is refunded");
        (await CollectionPostingCountAsync(project.Id)).Should().Be(1, "the payment is never reversed or re-posted");
        (await LedgerAsync(project.Id, AccountType.ClientReceivable)).Should().Be(-100_000m, "the client has paid ahead of the revision");

        await ExecuteAsync(project, 250m);
        Guid revision = await PrepareRevisionAsync(project, original);
        await StepAsync(_client, project, revision, ExtractStatus.Draft, ExtractStatus.Issued);

        JsonElement figures = await CollectionFiguresAsync(project.Id, revision, _finance);
        ParseDecimal(figures.GetProperty("collected")).Should().Be(100_000m, "the original's payment counts against the revision");
        (decimal cash, decimal withheld) = await DueAsync(_client, project.Id, revision);
        decimal net = await LedgerAsync(project.Id, AccountType.ClientReceivable);
        (cash + withheld).Should().Be(net, "what remains is the revision's net less the payment already received");

        await OkAsync(HttpMethod.Post, Step(project.Id, revision, "collect"), _finance, Collect("Transfer", cash, withheld, "TRX-REST"));
        (await StatusOfAsync(revision)).Should().Be(ExtractStatus.Collected);
        (await LedgerAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m);
        (await NetIntoAsync(project.Id, AccountType.Safe)).Should().Be(safeBefore + 100_000m + cash);
    }

    /// <summary>D-242 item 4: a revision in flight does not block the next normal extract, and the rule stands for normal ones.</summary>
    [Fact]
    public async Task A_revision_in_flight_runs_alongside_the_next_normal_extract_and_only_one_normal_extract_is_open()
    {
        Project project = await NewProjectAsync(holdPercent: 0m);
        Guid original = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute"), _finance, new { reason = "quantities overstated" });
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "remeasured" });
        Guid revision = await PrepareRevisionAsync(project, original);
        (await StatusOfAsync(revision)).Should().Be(ExtractStatus.Draft);

        HttpResponseMessage next = await SendAsync(
            HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-10-01", periodEnd = "2026-10-31" });
        next.StatusCode.Should().Be(HttpStatusCode.Created, await next.Content.ReadAsStringAsync(Ct));

        HttpResponseMessage third = await SendAsync(
            HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-11-01", periodEnd = "2026-11-30" });
        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(third)).Should().Be("extract.another_extract_open");

        (await SendAsync(HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-09-01", periodEnd = "2026-09-30", revisesExtractId = original }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "a cancelled extract has one revision");
    }

    // ---- D-242 item 2: a rejection goes to whoever can fix its reason --------------------------------

    [Fact]
    public async Task A_rejection_for_wrong_quantities_waits_for_the_site_engineer_and_one_for_material_values_for_the_supervisor()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        string reject = Step(project.Id, extract, "technical-rejection");

        HttpResponseMessage noKind = await SendAsync(HttpMethod.Post, reject, _technicalOffice, new { reason = "short" });
        noKind.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(noKind)).Should().Be("extract.rejection_kind_required");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.QcSubmitted);

        await OkAsync(HttpMethod.Post, reject, _technicalOffice, new { reason = "short by 20 m3", kind = "Quantities" });
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Draft);
        JsonElement waiting = (await GetJsonAsync(WaitingOnRoute(project.Id), _owner)).EnumerateArray().Single().GetProperty("waitingOn");
        waiting.GetProperty("permission").GetString().Should().Be("ExtractPrepare", "the site engineer fixes the quantities");
        (await ActionsAsync(project.Id, extract, _supervisor))["submitQcReport"].Should().BeFalse();
        HttpResponseMessage early = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "qc-report"), _supervisor, QcBody());
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(early)).Should().Be("extract.awaiting_evidence_revision");

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "evidence-revision"), _prepEngineer, ReviseBody());
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "qc-report"), _supervisor, QcBody());

        await OkAsync(HttpMethod.Post, reject, _technicalOffice2, new { reason = "on-site value overstated", kind = "MaterialValues" });
        waiting = (await GetJsonAsync(WaitingOnRoute(project.Id), _owner)).EnumerateArray().Single().GetProperty("waitingOn");
        waiting.GetProperty("permission").GetString().Should().Be("ExtractQcReportSubmit", "the supervisor enters the values again");
        (await ActionsAsync(project.Id, extract, _prepEngineer))["reviseEvidence"].Should().BeFalse();
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "qc-report"), _supervisor, QcBody(materialValueOnSite: 50_000m));

        await using KaffDbContext reader = _database.CreateBareContext();
        List<ExtractGateRecord> rejections = await reader.Set<ExtractGateRecord>()
            .Where(record => record.ExtractId == extract && record.Outcome == ExtractGateOutcome.Rejected)
            .ToListAsync(Ct);
        rejections.Select(record => $"{record.RejectionKind}: {record.Reason}")
            .Should().BeEquivalentTo("Quantities: short by 20 m3", "MaterialValues: on-site value overstated");
        foreach (ExtractGateRecord record in rejections)
        {
            (await reader.AuditRecords.AnyAsync(a => a.EntityType == nameof(ExtractGateRecord) && a.EntityId == record.Id, Ct))
                .Should().BeTrue("each rejection is audited with its reason");
        }
    }

    /// <summary>The revision is prepared for the original's period whatever period the request names.</summary>
    private async Task<Guid> PrepareRevisionAsync(Project project, Guid original)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, Extracts(project.Id), _prepEngineer,
            new { periodStart = "2026-12-01", periodEnd = "2026-12-31", revisesExtractId = original });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Dictionary<AccountType, decimal>> LedgersAsync(Guid project, AccountType[] types)
    {
        Dictionary<AccountType, decimal> balances = [];
        foreach (AccountType type in types)
        {
            balances[type] = await LedgerAsync(project, type);
        }

        return balances;
    }

    /// <summary>A project ledger's balance, zero while nothing has been posted to it (the balance view has no row yet).</summary>
    private async Task<decimal> LedgerAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        AccountBalance? row = await reader.AccountBalances
            .SingleOrDefaultAsync(balance => balance.ProjectId == project && balance.Type == type, Ct);
        return row?.SignedBalance.Amount ?? 0m;
    }
}
