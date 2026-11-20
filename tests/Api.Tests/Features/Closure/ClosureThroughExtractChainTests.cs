using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Closure;

/// <summary>
/// The closure story run on the hold the real extract chain accrues, not on a hold written straight to the ledger
/// (the shortcut <see cref="ClosureEndpointTests"/> takes for everything that does not need the chain).
/// </summary>
/// <remarks>
/// <para>
/// The project recipe and the route walk are the ones <c>ExtractEndpointTests</c> uses (a signed Lump Sum project with
/// a frozen BOQ of 1,000 units at 1,000, the 250,000 advance at signing, 300 units executed), copied here because that
/// class keeps them private. The arithmetic is spec.md section 15 and is proved in
/// <c>Section15ThroughExtractChainTests</c> without HTTP; this class proves that the routes produce the same ledgers and
/// that the closure acts then behave on them.
/// </para>
/// <para>
/// A totals-only test cannot see a timing error, so the stage each posting is written at is asserted, not only the
/// amount: the hold accrues at Owner approval, cash at collection, the release at the release.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ClosureThroughExtractChainTests : IAsyncLifetime
{
    private const string StatePolicyRefusal = "treasury.posting_not_allowed_in_project_state";

    private static readonly DateTimeOffset Now = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private readonly Dictionary<Guid, object> _materialValues = [];
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _babId;
    private Guid _safeId;
    private Guid _owner;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _prepEngineer;
    private Guid _supervisor;
    private Guid _financeUnassigned;

    public ClosureThroughExtractChainTests(PostgresDatabase database) => _database = database;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- KAFF-802-A: spec.md section 15 through the routes, to the release and the close -------------------

    [Fact]
    public async Task Section_15_through_the_routes_accrues_the_hold_at_owner_approval_and_releases_it_once_in_full_at_handover()
    {
        Project project = await NewProjectAsync();
        Guid id = project.Id;

        // Extract 1, stage by stage: the hold is written at Owner approval and at no earlier stage; cash only at collection.
        Guid first = await PrepareAsync(project, "100000", "0");
        foreach ((ExtractStatus reached, int accruals) in new[]
        {
            (ExtractStatus.QcSubmitted, 0), (ExtractStatus.TechnicalApproved, 0), (ExtractStatus.DeductionsApplied, 0),
            (ExtractStatus.OwnerApproved, 1), (ExtractStatus.Issued, 1),
        })
        {
            await AdvanceAsync(project, first, reached);
            (await CountAsync(id, PostingType.HoldAccrual)).Should().Be(accruals, $"hold accruals once extract 1 is {reached}");
            (await CountAsync(id, PostingType.ClientCollection)).Should().Be(0, $"no cash at {reached}");
        }

        (await BalanceAsync(id, AccountType.Hold)).Should().Be(60_000m);
        await CollectAsync(project, first);
        (await CountAsync(id, PostingType.ClientCollection)).Should().Be(1, "cash is written at collection");

        await ExecuteAsync(project, 600m);
        Guid second = await PrepareAsync(project, "0", "60000");
        await AdvanceAsync(project, second, ExtractStatus.Issued);
        await CollectAsync(project, second);

        await ExecuteAsync(project, 1_000m);
        Guid third = await PrepareAsync(project, "0", "40000");
        await AdvanceAsync(project, third, ExtractStatus.Issued);
        await CollectAsync(project, third);

        // The chain's own ledgers, summed from postings.
        List<Posting> postings = await PostingsAsync(id);
        postings.Where(posting => posting.Type == PostingType.HoldAccrual).Select(posting => posting.Amount.Amount).Should()
            .Equal(60_000m, 60_000m, 80_000m);
        postings.Where(posting => posting.Type == PostingType.ClientCollection).OrderBy(posting => posting.CreatedAt)
            .Select(posting => posting.Amount.Amount).Should().Equal(240_000m, 120_000m, 190_000m);
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(200_000m, "hold accumulated is 20% of the contract value");
        (await BalanceAsync(id, AccountType.ClientAdvance)).Should().Be(0m, "the advance ledger ends exactly at zero");
        (await BalanceAsync(id, AccountType.MaterialAdvance)).Should().Be(0m, "material advance in equals material advance recovered");
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(0m);

        // Before handover the release is refused and the hold is intact.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountAsync(id, PostingType.HoldRelease)).Should().Be(0);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/practical-completion", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/handover", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountAsync(id, PostingType.HoldRelease)).Should().Be(0, "handing over writes no release: the release is its own act");
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(200_000m);

        HttpResponseMessage released = await SendAsync(HttpMethod.Post, $"/api/projects/{id}/hold-release", _finance, null);
        released.StatusCode.Should().Be(HttpStatusCode.OK, await released.Content.ReadAsStringAsync(Ct));
        using (JsonDocument body = JsonDocument.Parse(await released.Content.ReadAsStringAsync(Ct)))
        {
            decimal.Parse(body.RootElement.GetProperty("released").GetString()!, CultureInfo.InvariantCulture).Should().Be(200_000m);
        }

        List<Posting> afterRelease = await PostingsAsync(id);
        Posting release = afterRelease.Single(posting => posting.Type == PostingType.HoldRelease);
        release.Amount.Amount.Should().Be(afterRelease.Where(posting => posting.Type == PostingType.HoldAccrual).Sum(posting => posting.Amount.Amount));
        release.Amount.Amount.Should().Be(200_000m, "20% of the 1,000,000 contract");
        release.ReversesId.Should().BeNull();
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(0m);
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(200_000m, "the released hold is what the client now owes (section 15, handover row)");

        // Verifier F-5, 2026-11-18, the same walk: after the release the generic reversal route took extract 1's accrual
        // out of the hold (-60,000), then reversed the release (140,000), and the hold was released again. Every step is
        // refused now, and the ledgers do not move.
        Guid firstAccrual = afterRelease.Where(posting => posting.Type == PostingType.HoldAccrual).OrderBy(posting => posting.CreatedAt).First().Id;
        foreach (Guid target in new[] { firstAccrual, release.Id })
        {
            HttpResponseMessage reversed = await SendAsync(
                HttpMethod.Post, $"/api/projects/{id}/treasury/postings/{target}/reverse", _finance, new { postingDate = "2026-11-06" });
            reversed.StatusCode.Should().Be(HttpStatusCode.Conflict, await reversed.Content.ReadAsStringAsync(Ct));
            using JsonDocument problem = JsonDocument.Parse(await reversed.Content.ReadAsStringAsync(Ct));
            problem.RootElement.GetProperty("code").GetString().Should().Be("treasury.hold_posting_not_reversible");
            (await BalanceAsync(id, AccountType.Hold)).Should().Be(0m);
            (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(200_000m);
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/hold-release", _finance, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the hold releases once");
        (await CountAsync(id, PostingType.HoldRelease)).Should().Be(1);

        // The final collection: the client pays the released hold through its own route (D-244 item 2, KAFF-317-E), on
        // the same collection flow as an extract. Total client cash then equals the contract value.
        HttpResponseMessage paid = await SendAsync(HttpMethod.Post, HoldCollect(id), _finance, HoldPayment("Transfer", "200000", "0", "HOLD"));
        paid.StatusCode.Should().Be(HttpStatusCode.OK, await paid.Content.ReadAsStringAsync(Ct));

        List<Posting> all = await PostingsAsync(id);
        all.Where(posting => posting.Type is PostingType.ClientAdvanceReceipt or PostingType.ClientCollection)
            .Sum(posting => posting.Amount.Amount).Should().Be(1_000_000m, "total client cash equals the contract value");
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(0m);

        // Every extract is Collected, the advance is recovered and the hold released: the one query that decides closure finds nothing.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/warranty", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage closed = await SendAsync(HttpMethod.Post, $"/api/projects/{id}/close", _owner, null);
        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync(Ct));
        (await StatusAsync(id)).Should().Be(ProjectStatus.Closed);
    }

    // ---- D-244 item 2, KAFF-317-E: the released hold is collected on the extract's collection flow ----------

    /// <summary>
    /// Handover, the hold released, then the client pays it as it pays an extract: a part transfer with tax withheld at
    /// source, a cheque that bounces (reason stored, nothing posted), a cheque that is cash only when it clears, and a
    /// cap at what remains. Finance alone, with the assignment. The receivable ends at zero.
    /// </summary>
    [Fact]
    public async Task The_released_hold_is_collected_in_parts_with_withholding_and_a_cheque_that_is_cash_only_at_clearance()
    {
        Project project = await NewProjectAsync(WithholdingCategory.ContractingAndSupplies);
        Guid id = project.Id;
        Guid first = await PrepareAsync(project, "100000", "0");
        await AdvanceAsync(project, first, ExtractStatus.Issued);
        await CollectAsync(project, first);
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(0m);

        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/practical-completion", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage early = await SendAsync(HttpMethod.Post, HoldCollect(id), _finance, HoldPayment("Transfer", "1000", "0", "EARLY"));
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(early)).Should().Be("closure.hold_not_released");

        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/handover", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(60_000m, "the released hold is what the client owes");
        (await HoldPositionAsync(id)).Should().Equal(60_000m, 0m, 0m, 0m, 60_000m);
        decimal safe = await NetIntoAsync(id, AccountType.Safe);
        decimal tax = await NetIntoAsync(id, AccountType.TaxWithheldAtSource);

        // Finance alone, and only with the assignment.
        foreach (Guid refused in new[] { _owner, _technicalOffice, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Post, HoldCollect(id), refused, HoldPayment("Cash", "1", "0", "X"))).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Get, $"/api/projects/{id}/hold-release/collection", refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        // A part transfer with tax withheld at source.
        HttpResponseMessage part = await SendAsync(HttpMethod.Post, HoldCollect(id), _finance, HoldPayment("Transfer", "29400", "600", "TRX-H1"));
        part.StatusCode.Should().Be(HttpStatusCode.OK, await part.Content.ReadAsStringAsync(Ct));
        (await HoldPositionAsync(id)).Should().Equal(60_000m, 29_400m, 600m, 0m, 30_000m);
        (await NetIntoAsync(id, AccountType.Safe)).Should().Be(safe + 29_400m);
        (await NetIntoAsync(id, AccountType.TaxWithheldAtSource)).Should().Be(tax + 600m);
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(30_000m);

        // Never more than remains.
        HttpResponseMessage over = await SendAsync(HttpMethod.Post, HoldCollect(id), _finance, HoldPayment("Cash", "30000.01", "0", "OVER"));
        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(over)).Should().Be("closure.released_hold_exceeded");

        // A cheque that bounces: nothing posted, the reason stored, the amount expected again.
        long before = await CountAllAsync(id);
        Guid bounced = await HoldChequeAsync(id, "30000", "CHQ-H1");
        (await HoldPositionAsync(id)).Should().Equal(60_000m, 29_400m, 600m, 30_000m, 0m);
        HttpResponseMessage nothing = await SendAsync(HttpMethod.Post, HoldCollect(id), _finance, HoldPayment("Cash", "1", "0", "N"));
        (await ErrorCodeAsync(nothing)).Should().Be("closure.released_hold_nothing_remaining");
        (await SendAsync(HttpMethod.Post, ChequeStep(id, bounced, "deposit"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage noReason = await SendAsync(HttpMethod.Post, ChequeStep(id, bounced, "bounce"), _finance, new { reason = " " });
        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(noReason)).Should().Be("collection.bounce_reason_required");
        (await SendAsync(HttpMethod.Post, ChequeStep(id, bounced, "bounce"), _finance, new { reason = "insufficient funds" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await CountAllAsync(id)).Should().Be(before, "a cheque posts nothing until it clears");
        (await HoldPositionAsync(id)).Should().Equal(60_000m, 29_400m, 600m, 0m, 30_000m);
        using (JsonDocument cheque = JsonDocument.Parse(await (await SendAsync(HttpMethod.Get, $"/api/projects/{id}/cheques/{bounced}", _finance, null)).Content.ReadAsStringAsync(Ct)))
        {
            cheque.RootElement.GetProperty("bounceReason").GetString().Should().Be("insufficient funds");
            cheque.RootElement.GetProperty("extractNumber").ValueKind.Should().Be(JsonValueKind.Null, "it paid the released hold");
        }

        // A cheque that clears: cash only at clearance, and the released hold is fully collected.
        Guid good = await HoldChequeAsync(id, "30000", "CHQ-H2");
        (await ChequeFlagsAsync(id, good)).Should().Equal(true, false, false);
        (await SendAsync(HttpMethod.Post, ChequeStep(id, good, "deposit"), _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        // A cheque on the released hold is moved from the same flags as one on an extract.
        (await ChequeFlagsAsync(id, good)).Should().Equal(false, true, true);
        (await NetIntoAsync(id, AccountType.Safe)).Should().Be(safe + 29_400m, "a deposited cheque is not yet cash");
        HttpResponseMessage cleared = await SendAsync(HttpMethod.Post, ChequeStep(id, good, "clearance"), _finance, null);
        cleared.StatusCode.Should().Be(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync(Ct));
        (await NetIntoAsync(id, AccountType.Safe)).Should().Be(safe + 59_400m);
        (await HoldPositionAsync(id)).Should().Equal(60_000m, 59_400m, 600m, 0m, 0m);
        (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(0m);
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(0m);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<Posting> collections = await reader.Postings
            .Where(posting => posting.ProjectId == id && posting.SourceDocumentType == SourceDocumentType.ProjectHandover
                && (posting.Type == PostingType.ClientCollection || posting.Type == PostingType.TaxWithheldAtSource))
            .ToListAsync(Ct);
        collections.Select(posting => posting.Amount.Amount).Should().BeEquivalentTo([29_400m, 600m, 30_000m]);
        collections.Should().AllSatisfy(posting => posting.SourceDocumentId.Should().Be(id));
        Guid[] records = await reader.CollectionRecords.Where(r => r.ProjectId == id && r.ExtractId == null).Select(r => r.Id).ToArrayAsync(Ct);
        records.Should().HaveCount(3);
        (await reader.AuditRecords.Where(a => a.EntityId != null && records.Contains(a.EntityId.Value)).Select(a => a.EntityId).Distinct().CountAsync(Ct))
            .Should().Be(3, "every released-hold collection is audited");
        (await reader.AuditRecords.Where(a => a.EntityType == "Cheque" && a.EntityId == bounced).Select(a => a.AfterJson).ToListAsync(Ct))
            .Should().Contain(after => after != null && after.Contains("insufficient funds", StringComparison.Ordinal), "the bounce and its reason are audited");
    }

    // ---- KAFF-807-B: the extracts condition, through the handler's own query ------------------------------

    /// <summary>
    /// The advance is drawn to zero and the hold released, so the extracts condition is the only one left. An extract in
    /// any state short of Issued then blocks the close and the refusal names exactly that condition and the count; an
    /// extract that is Issued or Collected does not.
    /// </summary>
    [Theory]
    [InlineData(ExtractStatus.Draft, true)]
    [InlineData(ExtractStatus.QcSubmitted, true)]
    [InlineData(ExtractStatus.TechnicalApproved, true)]
    [InlineData(ExtractStatus.DeductionsApplied, true)]
    [InlineData(ExtractStatus.OwnerApproved, true)]
    [InlineData(ExtractStatus.Issued, false)]
    [InlineData(ExtractStatus.Collected, false)]
    public async Task Closing_names_the_extracts_condition_when_an_extract_is_short_of_issued_and_closes_when_none_is(
        ExtractStatus secondExtract, bool blocks)
    {
        Project project = await NewProjectAsync();
        Guid id = project.Id;

        Guid first = await PrepareAsync(project, "100000", "0");
        await AdvanceAsync(project, first, ExtractStatus.Issued);
        await CollectAsync(project, first);

        await ExecuteAsync(project, 600m);
        Guid second = await PrepareAsync(project, "0", "60000");
        await AdvanceAsync(project, second, secondExtract == ExtractStatus.Collected ? ExtractStatus.Issued : secondExtract);
        if (secondExtract == ExtractStatus.Collected)
        {
            await CollectAsync(project, second);
        }

        await DrawDownAdvanceToZeroAsync(project);
        await SetStatusPathAsync(id, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{id}/hold-release", _finance, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BalanceAsync(id, AccountType.ClientAdvance)).Should().Be(0m);
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(0m);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{id}/close", _owner, null);

        if (!blocks)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            (await StatusAsync(id)).Should().Be(ProjectStatus.Closed);
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("unmet").EnumerateArray().Select(item => item.GetString()).Should()
            .Equal(["ExtractsIssued"], $"an extract at {secondExtract} is the only unmet condition");
        problem.RootElement.GetProperty("unissuedExtracts").GetInt32().Should().Be(1);
        (await StatusAsync(id)).Should().Be(ProjectStatus.UnderWarranty);
    }

    // ---- KAFF-807-G: after closing, only collections post -----------------------------------------------

    /// <summary>
    /// One Closed project, three attempts, three different answers. The extract act and the release are refused by the
    /// project-state policy (the hold here is still 60,000 and was never released, so "already released" is not in play),
    /// and the collection is accepted.
    /// </summary>
    [Fact]
    public async Task A_closed_project_refuses_an_extract_act_and_a_hold_release_and_accepts_a_collection()
    {
        Project project = await NewProjectAsync();
        Guid id = project.Id;

        Guid issued = await PrepareAsync(project, "100000", "0");
        await AdvanceAsync(project, issued, ExtractStatus.Issued);
        await ExecuteAsync(project, 600m);
        Guid inFlight = await PrepareAsync(project, "0", "60000");
        await AdvanceAsync(project, inFlight, ExtractStatus.DeductionsApplied);

        await SetStatusPathAsync(
            id, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty, ProjectStatus.Closed);
        (await StatusAsync(id)).Should().Be(ProjectStatus.Closed);
        long postings = await CountAllAsync(id);
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(60_000m);
        (await CountAsync(id, PostingType.HoldRelease)).Should().Be(0);

        // 1. an extract act that posts: refused by the state policy, nothing written, the extract has not moved.
        HttpResponseMessage act = await SendAsync(HttpMethod.Post, Step(id, inFlight, "owner-approval"), _owner, null);
        act.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(act)).Should().Be(StatePolicyRefusal);
        (await ExtractStatusAsync(inFlight)).Should().Be(ExtractStatus.DeductionsApplied);
        (await CountAllAsync(id)).Should().Be(postings);

        // 2. a hold release: refused by the state policy and by nothing else.
        HttpResponseMessage release = await SendAsync(HttpMethod.Post, $"/api/projects/{id}/hold-release", _finance, null);
        release.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(release)).Should().Be(StatePolicyRefusal);
        (await BalanceAsync(id, AccountType.Hold)).Should().Be(60_000m);
        (await CountAllAsync(id)).Should().Be(postings);

        // 3. a collection: accepted, and it writes the cash.
        HttpResponseMessage collection = await SendAsync(HttpMethod.Post, Step(id, issued, "collect"), _finance, CollectBody("240000"));
        collection.StatusCode.Should().Be(HttpStatusCode.OK, await collection.Content.ReadAsStringAsync(Ct));
        (await ExtractStatusAsync(issued)).Should().Be(ExtractStatus.Collected);
        (await CountAsync(id, PostingType.ClientCollection)).Should().Be(1);
        (await CountAllAsync(id)).Should().BeGreaterThan(postings);
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    private static string Step(Guid project, Guid extract, string step) => $"/api/projects/{project}/extracts/{extract}/{step}";

    private static readonly string[] HoldPositionFields = ["expected", "received", "withheld", "inCheques", "remaining"];

    private static string HoldCollect(Guid project) => $"/api/projects/{project}/hold-release/collect";

    private static string ChequeStep(Guid project, Guid cheque, string step) => $"/api/projects/{project}/cheques/{cheque}/{step}";

    /// <summary>The cheque read's deposit, clear and bounce flags, as Finance reads them.</summary>
    private async Task<bool[]> ChequeFlagsAsync(Guid project, Guid cheque)
    {
        using JsonDocument read = JsonDocument.Parse(
            await (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/cheques/{cheque}", _finance, null)).Content.ReadAsStringAsync(Ct));
        JsonElement actions = read.RootElement.GetProperty("actions");
        return [actions.GetProperty("deposit").GetBoolean(), actions.GetProperty("clear").GetBoolean(), actions.GetProperty("bounce").GetBoolean()];
    }

    private static object HoldPayment(string method, string cashReceived, string withheldAtSource, string reference) =>
        new { method, date = "2026-11-06", reference, cashReceived, withheldAtSource };

    /// <summary>The released hold's position as read: expected, received, withheld, in cheques, remaining.</summary>
    private async Task<decimal[]> HoldPositionAsync(Guid project)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/api/projects/{project}/hold-release/collection", _finance, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. HoldPositionFields
            .Select(name => decimal.Parse(body.RootElement.GetProperty(name).GetString()!, CultureInfo.InvariantCulture))];
    }

    private async Task<Guid> HoldChequeAsync(Guid project, string amount, string reference)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, HoldCollect(project), _finance, HoldPayment("Cheque", amount, "0", reference));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        await using KaffDbContext reader = _database.CreateBareContext();
        return await (
            from cheque in reader.Cheques
            join record in reader.CollectionRecords on cheque.CollectionId equals record.Id
            where cheque.ProjectId == project && record.Reference == reference
            select cheque.Id).SingleAsync(Ct);
    }

    /// <summary>Net value this project's postings moved into accounts of <paramref name="type"/> (the safe and the tax account are company-wide).</summary>
    private async Task<decimal> NetIntoAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        Guid[] ids = await reader.Accounts.Where(account => account.Type == type).Select(account => account.Id).ToArrayAsync(Ct);
        List<Posting> postings = await reader.Postings
            .Where(posting => posting.ProjectId == project && (ids.Contains(posting.ToAccountId) || ids.Contains(posting.FromAccountId)))
            .ToListAsync(Ct);
        return postings.Sum(posting => ids.Contains(posting.ToAccountId) ? posting.Amount.Amount : -posting.Amount.Amount);
    }

    private static object CollectBody(string cashReceived) =>
        new { method = "Transfer", date = "2026-10-01", reference = "TRX-1", cashReceived, withheldAtSource = "0" };

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private async Task<Guid> PrepareAsync(Project project, string materialValueOnSite, string materialInstalled)
    {
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            $"/api/projects/{project.Id}/extracts",
            _prepEngineer,
            new { periodStart = "2026-09-01", periodEnd = "2026-09-30" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        // D-235 item 4: the supervising engineer enters the two material values with the QC report, not the preparer.
        _materialValues[id] = new { materialValueOnSite, materialInstalled };
        return id;
    }

    /// <summary>Walks an extract from Draft to <paramref name="target"/> with a different actor at each gate.</summary>
    private async Task AdvanceAsync(Project project, Guid extract, ExtractStatus target)
    {
        ExtractStatus current = await ExtractStatusAsync(extract);

        (ExtractStatus Reaches, string Route, Guid Actor, object? Body)[] chain =
        [
            (ExtractStatus.QcSubmitted, "qc-report", _supervisor, _materialValues[extract]),
            (ExtractStatus.TechnicalApproved, "technical-approval", _technicalOffice, new { }),
            (ExtractStatus.DeductionsApplied, "deductions", _finance, null),
            (ExtractStatus.OwnerApproved, "owner-approval", _owner, null),
            (ExtractStatus.Issued, "issue", _owner, null),
        ];

        foreach ((ExtractStatus reaches, string route, Guid actor, object? body) in chain)
        {
            if (reaches <= current || reaches > target)
            {
                continue;
            }

            HttpResponseMessage response = await SendAsync(HttpMethod.Post, Step(project.Id, extract, route), actor, body);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");
        }
    }

    /// <summary>spec.md section 15: the client pays the full net each time. D-235 item 2: the collection says what arrived.</summary>
    private async Task CollectAsync(Project project, Guid extract)
    {
        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"/api/projects/{project.Id}/extracts/{extract}", _finance, null);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await read.Content.ReadAsStringAsync(Ct));
        string remaining = document.RootElement.GetProperty("collection").GetProperty("remaining").GetString()!;

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody(remaining));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
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

    private async Task<ExtractStatus> ExtractStatusAsync(Guid extract)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Extracts.Where(candidate => candidate.Id == extract).Select(candidate => candidate.Status).SingleAsync(Ct);
    }

    private async Task<ProjectStatus> StatusAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.Where(candidate => candidate.Id == project).Select(candidate => candidate.Status).SingleAsync(Ct);
    }

    private async Task<List<Posting>> PostingsAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.Where(posting => posting.ProjectId == project).OrderBy(posting => posting.CreatedAt).ToListAsync(Ct);
    }

    private async Task<long> CountAsync(Guid project, PostingType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project && posting.Type == type, Ct);
    }

    private async Task<long> CountAllAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Postings.LongCountAsync(posting => posting.ProjectId == project, Ct);
    }

    private async Task<Guid> AccountIdAsync(Guid project, AccountType type)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Accounts.Where(account => account.ProjectId == project && account.Type == type).Select(account => account.Id).SingleAsync(Ct);
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

    /// <summary>Moves the project through the given states directly, as the closure acts would, without their preconditions.</summary>
    private async Task SetStatusPathAsync(Guid project, params ProjectStatus[] path)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project loaded = await context.Projects.SingleAsync(candidate => candidate.Id == project, Ct);
        foreach (ProjectStatus step in path)
        {
            loaded.TransitionTo(step, new DateOnly(2026, 10, 1), "test").IsSuccess.Should().BeTrue($"{step}");
        }

        await context.SaveChangesAsync(Ct);
    }

    /// <summary>Draws whatever client advance is left to zero while the project is still Active, so closure reads it as recovered.</summary>
    private async Task DrawDownAdvanceToZeroAsync(Project project)
    {
        decimal remaining = await BalanceAsync(project.Id, AccountType.ClientAdvance);
        if (remaining <= 0m)
        {
            return;
        }

        await using KaffDbContext context = _database.CreateContext();
        Account advance = await context.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.ClientAdvance, Ct);
        Account receivable = await context.Accounts.SingleAsync(a => a.ProjectId == project.Id && a.Type == AccountType.ClientReceivable, Ct);
        context.Postings.Add(Posting.Create(
            receivable, advance, new Money(remaining), PostingType.ClientAdvanceRecovery,
            new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "DRAWDOWN"),
            new DateOnly(2026, 9, 26), _owner, Now, project.Id).Value);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<Project> NewProjectAsync(WithholdingCategory withholding = WithholdingCategory.None)
    {
        Project project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            project = Project.Create(UniqueNames.Code("CC-P"), UniqueNames.Code("مشروع إقفال بالمستخلصات"), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
            project.SetWithholding(withholding, ClientKind.Corporate).IsSuccess.Should().BeTrue();
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

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/sign", _owner, new { signedOn = "2026-09-24", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
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
            UniqueNames.Code("CC-C"), "عميل الإقفال", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Bab bab = Bab.Create(UniqueNames.Code("CCB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(0m)).Value;
        Account safe = Account.Create(
            AccountType.Safe, UniqueNames.Code("CCSAFE"), "خزنة", "Safe", Currency.Egp, new DateOnly(2026, 1, 1)).Value;

        User owner = Make("cc-owner", Role.Owner);
        User finance = Make("cc-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User technicalOffice = Make("cc-to", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User prep = Make("cc-prep", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = Make("cc-sup", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User financeUnassigned = Make("cc-fin-un", Role.Finance, WellKnownDepartments.FinanceId);
        Account taxWithheld = Account.Create(
            AccountType.TaxWithheldAtSource, UniqueNames.Code("CCTAX"), "ضريبة خصم من المنبع", "Tax withheld at source",
            Currency.Egp, new DateOnly(2026, 1, 1)).Value;

        context.Clients.Add(client);
        context.Babs.Add(bab);
        context.Accounts.AddRange(safe, taxWithheld);
        context.Users.AddRange(owner, finance, technicalOffice, prep, supervisor, financeUnassigned);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _babId = bab.Id;
        _safeId = safe.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _prepEngineer = prep.Id;
        _supervisor = supervisor.Id;
        _financeUnassigned = financeUnassigned.Id;
    }

    private static User Make(string name, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null) =>
        User.Create(UniqueNames.Code(name), name, UniqueNames.Phone(), role, Now, department, subDepartment).Value;
}
