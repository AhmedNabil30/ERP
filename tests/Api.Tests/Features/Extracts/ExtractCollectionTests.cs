using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// D-235 items 1, 2 and 4, D-237's open item and the journey's server gaps (ux/journey-extract-chain-2026-11-18.md
/// section 5), through the routes. A cheque is cash only when it clears; a collection records what arrived; the
/// extract is Collected when received plus withheld reach net payable less approved credit notes; the supervising
/// engineer enters the material values with the QC report; the read says what the caller may do now.
/// </summary>
public sealed partial class ExtractEndpointTests
{
    private static object QcBody(decimal materialValueOnSite = 100_000m, decimal materialInstalled = 0m) => new
    {
        materialValueOnSite = materialValueOnSite.ToString(CultureInfo.InvariantCulture),
        materialInstalled = materialInstalled.ToString(CultureInfo.InvariantCulture),
    };

    private static string Cheques(Guid project) => $"/api/projects/{project}/cheques";

    private static string ChequeStep(Guid project, Guid cheque, string step) => $"/api/projects/{project}/cheques/{cheque}/{step}";

    /// <summary>What the extract still expects, split as its own figures expect the withholding: (cash, withheld).</summary>
    private async Task<(decimal Cash, decimal Withheld)> DueAsync(HttpClient client, Guid project, Guid extract)
    {
        HttpResponseMessage response = await SendAsync(client, HttpMethod.Get, Single(project, extract), _finance, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement collection = document.RootElement.GetProperty("collection");
        decimal remaining = ParseDecimal(collection.GetProperty("remaining"));
        decimal withheld = ParseDecimal(collection.GetProperty("expectedWithheld"));
        return (remaining - withheld, withheld);
    }

    private async Task<JsonElement> CollectionFiguresAsync(Guid project, Guid extract, Guid actor) =>
        (await ReadAsync(project, extract, actor)).GetProperty("collection");

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

    private async Task<Guid> ChequeOfAsync(Guid project, string reference)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await (
            from cheque in reader.Cheques
            join record in reader.CollectionRecords on cheque.CollectionId equals record.Id
            where cheque.ProjectId == project && record.Reference == reference
            select cheque.Id).SingleAsync(Ct);
    }

    private static object Collect(string method, decimal cash, decimal withheld, string reference) => new
    {
        method,
        date = "2026-10-01",
        reference,
        cashReceived = cash.ToString(CultureInfo.InvariantCulture),
        withheldAtSource = withheld.ToString(CultureInfo.InvariantCulture),
    };

    private async Task OkAsync(HttpMethod method, string route, Guid actor, object? body)
    {
        HttpResponseMessage response = await SendAsync(method, route, actor, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{route}: {await response.Content.ReadAsStringAsync(Ct)}");
    }

    // ---- D-237 open item: a generic movement cannot count as an extract's collection ---------------------

    [Fact]
    public async Task A_generic_movement_cannot_name_a_real_extract_and_count_as_its_collection()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid receivable = await AccountIdAsync(project.Id, AccountType.ClientReceivable);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/treasury/postings", _finance, new
        {
            fromAccountId = receivable,
            toAccountId = _safeId,
            amount = "240000",
            type = nameof(PostingType.ClientCollection),
            postingDate = "2026-09-24",
            sourceDocumentType = nameof(SourceDocumentType.Extract),
            sourceDocumentId = extract,
            sourceDocumentReference = "EXT-1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
        (await ErrorCodeAsync(response)).Should().Be("treasury.flow_source_not_allowed");
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);
        ParseDecimal((await CollectionFiguresAsync(project.Id, extract, _finance)).GetProperty("collected")).Should().Be(0m);
    }

    // ---- D-235 item 1: a cheque becomes cash only when it clears ------------------------------------

    [Fact]
    public async Task A_cheque_collection_posts_nothing_and_the_extract_stays_issued_until_the_cheque_clears()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        decimal safeBefore = await NetIntoAsync(project.Id, AccountType.Safe);

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 240_000m, 0m, "CHQ-1"));
        Guid cheque = await ChequeOfAsync(project.Id, "CHQ-1");

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued, "a cheque in the drawer is not money received");
        (await NetIntoAsync(project.Id, AccountType.Safe)).Should().Be(safeBefore, "nothing reaches the safe while the cheque is in hand");
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m);

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "deposit"), _finance, null);
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        (await NetIntoAsync(project.Id, AccountType.Safe)).Should().Be(safeBefore, "a deposit is not a clearance");

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "clearance"), _finance, null);
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        (await NetIntoAsync(project.Id, AccountType.Safe)).Should().Be(safeBefore + 240_000m);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m);

        await using KaffDbContext reader = _database.CreateBareContext();
        Posting cash = await reader.Postings.SingleAsync(p => p.ProjectId == project.Id && p.Type == PostingType.ClientCollection, Ct);
        cash.SourceDocumentId.Should().Be(extract);
        (await reader.AuditRecords.CountAsync(a => a.EntityType == nameof(Cheque) && a.EntityId == cheque, Ct))
            .Should().BeGreaterThanOrEqualTo(3, "received, deposited and cleared each keep their own audit record");
    }

    [Fact]
    public async Task A_cheque_with_withholding_recognises_the_withheld_amount_only_when_it_clears()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.ContractingAndSupplies);
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 237_600m, 2_400m, "CHQ-W"));
        Guid cheque = await ChequeOfAsync(project.Id, "CHQ-W");
        (await PostingCountOfAsync(project.Id, PostingType.TaxWithheldAtSource)).Should().Be(0, "the collection is not complete");

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "deposit"), _finance, null);
        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "clearance"), _finance, null);

        (await NetIntoAsync(project.Id, AccountType.TaxWithheldAtSource)).Should().Be(2_400m);
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m);
    }

    [Fact]
    public async Task A_bounced_cheque_needs_a_reason_posts_nothing_and_leaves_the_extract_issued_with_the_reason_stored()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 240_000m, 0m, "CHQ-B"));
        Guid cheque = await ChequeOfAsync(project.Id, "CHQ-B");
        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "deposit"), _finance, null);
        List<string> before = await PostingSnapshotsAsync(project.Id);

        foreach (object? blank in new object?[] { null, new { reason = "  " } })
        {
            HttpResponseMessage refused = await SendAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "bounce"), _finance, blank);
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ErrorCodeAsync(refused)).Should().Be("extract.reason_required");
        }

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "bounce"), _finance, new { reason = "returned: insufficient funds" });

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        (await PostingSnapshotsAsync(project.Id)).Should().BeEquivalentTo(before, "nothing was posted, so nothing is reversed");
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Cheques.SingleAsync(c => c.Id == cheque, Ct)).State.Should().Be(ChequeState.Bounced);
        ExtractGateRecord gate = await reader.Set<ExtractGateRecord>()
            .SingleAsync(record => record.ExtractId == extract && record.Reason == "returned: insufficient funds", Ct);
        gate.Gate.ToString().Should().Be("ChequeBounce");
        gate.ActorUserId.Should().Be(_finance);
        (await reader.AuditRecords.AnyAsync(a => a.EntityType == nameof(ExtractGateRecord) && a.EntityId == gate.Id, Ct)).Should().BeTrue();

        ParseDecimal((await CollectionFiguresAsync(project.Id, extract, _finance)).GetProperty("remaining"))
            .Should().Be(240_000m, "the bounced cheque no longer counts against the extract");
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 240_000m, 0m, "TRX-2"));
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
    }

    // ---- D-235 item 2: a collection records what arrived ------------------------------------------------

    [Fact]
    public async Task A_part_payment_leaves_the_extract_issued_and_the_remaining_amount_is_readable()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 100_000m, 0m, "TRX-P1"));

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        JsonElement figures = await CollectionFiguresAsync(project.Id, extract, _finance);
        ParseDecimal(figures.GetProperty("expected")).Should().Be(240_000m);
        ParseDecimal(figures.GetProperty("collected")).Should().Be(100_000m);
        ParseDecimal(figures.GetProperty("remaining")).Should().Be(140_000m);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(140_000m);

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cash", 140_000m, 0m, "RCPT-2"));
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        ParseDecimal((await CollectionFiguresAsync(project.Id, extract, _finance)).GetProperty("remaining")).Should().Be(0m);
    }

    [Fact]
    public async Task A_payment_that_would_exceed_the_expected_amount_is_refused_and_writes_nothing()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        HttpResponseMessage over = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 240_000.0001m, 0m, "X"));
        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(over)).Should().Be("collection.exceeds_expected");

        // A cheque in hand counts against what is still expected, so the same money cannot be recorded twice.
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 200_000m, 0m, "CHQ-X"));
        HttpResponseMessage twice = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 40_001m, 0m, "Y"));
        twice.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(twice)).Should().Be("collection.exceeds_expected");

        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.CollectionRecords.CountAsync(record => record.ExtractId == extract, Ct)).Should().Be(1, "only the cheque was recorded");
    }

    [Theory]
    [InlineData("{\"method\":\"Cash\",\"date\":\"2026-10-01\",\"reference\":\"R-1\"}", "collection.amount_required")]
    [InlineData("{\"method\":\"Cash\",\"date\":\"2026-10-01\",\"reference\":\"R-1\",\"cashReceived\":\"-1\"}", "collection.amount_invalid")]
    [InlineData("{\"method\":\"Cash\",\"date\":\"2026-10-01\",\"reference\":\"R-1\",\"cashReceived\":\"0\",\"withheldAtSource\":\"0\"}", "collection.amount_invalid")]
    [InlineData("{\"method\":\"Cash\",\"date\":\"2026-10-01\",\"reference\":\"R-1\",\"cashReceived\":\"10\",\"withheldAtSource\":\"-1\"}", "collection.amount_invalid")]
    public async Task A_collection_without_a_sane_amount_is_refused(string json, string code)
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Step(project.Id, extract, "collect"), UriKind.Relative)) { Content = content };
        await using KaffDbContext reader = _database.CreateBareContext();
        var finance = await reader.Users.Where(user => user.Id == _finance).Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);
        request.Headers.Add(Kaff.Api.Tests.Infrastructure.TestAuthHandler.UserIdHeader, _finance.ToString());
        request.Headers.Add(Kaff.Api.Tests.Infrastructure.TestAuthHandler.SecurityStampHeader, finance.SecurityStamp);
        request.Headers.Add(Kaff.Api.Tests.Infrastructure.TestAuthHandler.RoleHeader, finance.Role.ToString());

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be(code);
        (await CollectionPostingCountAsync(project.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_withheld_amount_on_a_contract_that_withholds_nothing_is_refused()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.None);
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 237_600m, 2_400m, "W"));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(refused)).Should().Be("collection.withholding_not_applicable");
        (await PostingCountOfAsync(project.Id, PostingType.TaxWithheldAtSource)).Should().Be(0);
    }

    [Fact]
    public async Task An_approved_credit_note_reduces_what_the_extract_expects_and_the_reduced_amount_collects_it()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 40_000m);
        await OkAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null);

        ParseDecimal((await CollectionFiguresAsync(project.Id, extract, _finance)).GetProperty("expected")).Should().Be(200_000m);
        HttpResponseMessage over = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, CollectBody());
        (await ErrorCodeAsync(over)).Should().Be("collection.exceeds_expected", "the full net is more than is now owed");

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 200_000m, 0m, "TRX-CN"));

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(0m);
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(60_000m, "neither the credit note nor the collection touches the hold");
    }

    [Fact]
    public async Task Reversing_a_part_payment_restores_the_remaining_amount()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 100_000m, 0m, "TRX-R"));

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collection-reversal"), _finance, new { reason = "wrong extract" });

        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Issued);
        ParseDecimal((await CollectionFiguresAsync(project.Id, extract, _finance)).GetProperty("remaining")).Should().Be(240_000m);
        (await BalanceAsync(project.Id, AccountType.ClientReceivable)).Should().Be(240_000m);
        JsonElement row = (await CollectionsAsync(project.Id, extract, _finance)).EnumerateArray().Single();
        row.GetProperty("reversed").GetBoolean().Should().BeTrue();
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "collection-reversal"), _finance, new { reason = "again" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "nothing stands to reverse");
    }

    [Fact]
    public async Task A_credit_note_cannot_exceed_what_is_still_unpaid()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 220_000m, 0m, "TRX-U"));
        Guid revenue = await AccountIdAsync(project.Id, AccountType.ContractRevenue);

        HttpResponseMessage over = await SendAsync(HttpMethod.Post, Adjustments(project.Id), _finance, new
        {
            type = "ClientCreditNote",
            reason = "price dispute",
            sourceExtractId = extract,
            targetAccountId = revenue,
            amount = "20000.0001",
        });

        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(over)).Should().Be("adjustment.exceeds_extract_net");
        await RaiseCreditNoteAsync(project, extract, 20_000m);
    }

    // ---- D-235 item 4: the supervising engineer enters the material values with the QC report ------------

    [Fact]
    public async Task The_supervisor_enters_the_material_values_with_the_qc_report_and_prepare_takes_none()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Extract prepared = await reader.Extracts.SingleAsync(candidate => candidate.Id == extract, Ct);
            prepared.MaterialValueOnSite.Should().Be(Money.Zero, "prepare no longer takes the material values the junior sent");
            prepared.MaterialInstalled.Should().Be(Money.Zero);
        }

        HttpResponseMessage bare = await SendAsync(HttpMethod.Post, Step(project.Id, extract, "qc-report"), _supervisor, null);
        bare.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(bare)).Should().Be("extract.material_values_required");
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Draft);

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "qc-report"), _supervisor, QcBody(100_000m, 0m));
        await StepAsync(_client, project, extract, ExtractStatus.QcSubmitted, ExtractStatus.DeductionsApplied);

        JsonElement read = await ReadAsync(project.Id, extract, _finance);
        ParseDecimal(read.GetProperty("evidence").GetProperty("materialValueOnSite")).Should().Be(100_000m);
        ParseDecimal(read.GetProperty("document").GetProperty("printedTotal")).Should().Be(240_000m, "spec.md section 15, extract 1");
        JsonElement entered = read.GetProperty("document").GetProperty("figures").EnumerateArray()
            .Single(figure => figure.GetProperty("key").GetString() == "materialAdvance")
            .GetProperty("derivation").EnumerateArray()
            .First(input => input.GetProperty("labelKey").GetString() == "extract.input.material_value_on_site");
        entered.GetProperty("enteredByUserId").GetGuid().Should().Be(_supervisor, "the figure names who typed it");
    }

    [Fact]
    public async Task The_junior_engineer_is_refused_every_route_that_carries_money()
    {
        Project project = await NewProjectAsync();
        Guid draft = await PrepareAsync(project);
        (await SendAsync(HttpMethod.Post, Step(project.Id, draft, "qc-report"), _prepEngineer, QcBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "the material values are money");
        await OkAsync(HttpMethod.Post, Step(project.Id, draft, "qc-report"), _supervisor, QcBody());
        await StepAsync(_client, project, draft, ExtractStatus.QcSubmitted, ExtractStatus.Issued);

        // The single read is ProjectRead and strips the money for him; every route that carries money refuses him.
        JsonElement read = await ReadAsync(project.Id, draft, _prepEngineer);
        foreach (string field in new[] { "evidence", "document", "collection", "changeOrders" })
        {
            read.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null, field);
        }

        (string Method, string Route, object? Body)[] money =
        [
            ("GET", Extracts(project.Id), null),
            ("POST", Step(project.Id, draft, "deductions"), null),
            ("POST", Step(project.Id, draft, "collect"), CollectBody()),
            ("GET", Step(project.Id, draft, "collection"), null),
            ("POST", Step(project.Id, draft, "collection-reversal"), new { reason = "x" }),
            ("GET", Cheques(project.Id), null),
            ("PUT", $"/api/projects/{project.Id}/withholding", new { category = "ContractingAndSupplies" }),
        ];

        foreach ((string method, string route, object? body) in money)
        {
            (await SendAsync(new HttpMethod(method), route, _prepEngineer, body)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
        }
    }

    // ---- Verifier F-8: the contract's withholding category has a route --------------------------------

    [Fact]
    public async Task Finance_sets_the_contracts_withholding_category_and_the_change_is_audited()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.None);
        string route = $"/api/projects/{project.Id}/withholding";

        HttpResponseMessage response = await SendAsync(HttpMethod.Put, route, _finance, new { category = "ContractingAndSupplies" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using (JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)))
        {
            body.RootElement.GetProperty("withholdingCategory").GetString().Should().Be("ContractingAndSupplies");
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct)).WithholdingCategory
            .Should().Be(WithholdingCategory.ContractingAndSupplies);
        (await reader.AuditRecords.AnyAsync(
            a => a.EntityType == nameof(Project) && a.EntityId == project.Id && a.ActorUserId == _finance, Ct))
            .Should().BeTrue("who changed the contract's tax setting, and from what to what, is on the trail");

        // The category reaches the next extract's expected withholding: 1% of the 240,000 net.
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        ParseDecimal((await CollectionFiguresAsync(project.Id, extract, _finance)).GetProperty("expectedWithheld")).Should().Be(2_400m);
    }

    [Fact]
    public async Task Only_assigned_finance_and_the_owner_set_the_withholding_category()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.None);
        string route = $"/api/projects/{project.Id}/withholding";
        object body = new { category = "Services" };

        foreach (Guid refused in new[] { _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Put, route, refused, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        await OkAsync(HttpMethod.Put, route, _owner, body);
        HttpResponseMessage missing = await SendAsync(HttpMethod.Put, route, _finance, new { });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(missing)).Should().Be("project.withholding_category_required");
    }

    [Fact]
    public async Task An_individual_clients_contract_cannot_carry_a_withholding_category()
    {
        Project project = await NewProjectAsync(_individualClientId, WithholdingCategory.None, ClientKind.Individual);

        HttpResponseMessage refused = await SendAsync(
            HttpMethod.Put, $"/api/projects/{project.Id}/withholding", _finance, new { category = "ContractingAndSupplies" });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(refused)).Should().Be("master.individual_does_not_withhold");
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct)).WithholdingCategory.Should().Be(WithholdingCategory.None);
    }

    // ---- the journey's server gaps: what the read tells the screen -------------------------------------

    [Fact]
    public async Task The_read_offers_each_collection_action_only_to_the_caller_who_may_take_it_now()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        string[] keys = ["recordCollection", "recordChequeMove", "reverseCollection", "raiseDispute", "resolveDispute"];

        Dictionary<string, bool> Pick(Dictionary<string, bool> all) => keys.ToDictionary(key => key, key => all[key]);

        (Pick(await ActionsAsync(project.Id, extract, _finance))).Should().Equal(new Dictionary<string, bool>
        {
            ["recordCollection"] = true,
            ["recordChequeMove"] = false,
            ["reverseCollection"] = false,
            ["raiseDispute"] = true,
            ["resolveDispute"] = false,
        });
        foreach (Guid other in new[] { _owner, _technicalOffice, _supervisor })
        {
            (Pick(await ActionsAsync(project.Id, extract, other))).Values.Should().AllSatisfy(flag => flag.Should().BeFalse(), "only Finance holds these rows");
        }

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 100_000m, 0m, "CHQ-A"));
        Dictionary<string, bool> withCheque = await ActionsAsync(project.Id, extract, _finance);
        withCheque["recordChequeMove"].Should().BeTrue();
        withCheque["reverseCollection"].Should().BeFalse("a cheque in hand posted nothing to reverse");
        withCheque["recordCollection"].Should().BeTrue("140,000 is still expected");

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 140_000m, 0m, "TRX-A"));
        Dictionary<string, bool> covered = await ActionsAsync(project.Id, extract, _finance);
        covered["recordCollection"].Should().BeFalse("the cheque in hand and the transfer cover the whole net");
        covered["reverseCollection"].Should().BeTrue();

        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "dispute"), _finance, new { reason = "client contests quantities" });
        Dictionary<string, bool> disputed = await ActionsAsync(project.Id, extract, _finance);
        disputed["resolveDispute"].Should().BeTrue();
        disputed["raiseDispute"].Should().BeFalse();
        disputed["recordCollection"].Should().BeFalse("a disputed extract is not collected");
    }

    [Fact]
    public async Task A_raised_credit_note_closes_the_collection_control_until_it_is_decided()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 10_000m);

        (await ActionsAsync(project.Id, extract, _finance))["recordCollection"].Should().BeFalse();
        await OkAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null);
        (await ActionsAsync(project.Id, extract, _finance))["recordCollection"].Should().BeTrue();
    }

    [Fact]
    public async Task Expected_and_remaining_reach_the_money_roles_only()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.ContractingAndSupplies);
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        foreach (Guid money in new[] { _finance, _owner, _technicalOffice })
        {
            JsonElement figures = await CollectionFiguresAsync(project.Id, extract, money);
            ParseDecimal(figures.GetProperty("expected")).Should().Be(240_000m);
            ParseDecimal(figures.GetProperty("expectedWithheld")).Should().Be(2_400m);
            ParseDecimal(figures.GetProperty("remaining")).Should().Be(240_000m);
            ParseDecimal(figures.GetProperty("inCheques")).Should().Be(0m);
        }

        (await ReadAsync(project.Id, extract, _supervisor)).GetProperty("collection").ValueKind
            .Should().Be(JsonValueKind.Null, "a site engineer reads no money");
    }

    [Fact]
    public async Task The_cheque_read_names_its_extract_and_the_project_lists_the_cheques_in_hand()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        foreach (string reference in new[] { "CHQ-1", "CHQ-2", "CHQ-3", "CHQ-4" })
        {
            await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 60_000m, 0m, reference));
        }

        Guid deposited = await ChequeOfAsync(project.Id, "CHQ-2");
        Guid cleared = await ChequeOfAsync(project.Id, "CHQ-3");
        Guid bounced = await ChequeOfAsync(project.Id, "CHQ-4");
        foreach (Guid cheque in new[] { deposited, cleared, bounced })
        {
            await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "deposit"), _finance, null);
        }

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cleared, "clearance"), _finance, null);
        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, bounced, "bounce"), _finance, new { reason = "returned" });

        HttpResponseMessage one = await SendAsync(HttpMethod.Get, $"{Cheques(project.Id)}/{deposited}", _finance, null);
        using (JsonDocument document = JsonDocument.Parse(await one.Content.ReadAsStringAsync(Ct)))
        {
            document.RootElement.GetProperty("extractNumber").GetInt32().Should().Be(1);
        }

        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Get, Cheques(project.Id), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        HttpResponseMessage list = await SendAsync(HttpMethod.Get, Cheques(project.Id), _finance, null);
        list.StatusCode.Should().Be(HttpStatusCode.OK, await list.Content.ReadAsStringAsync(Ct));
        using JsonDocument rows = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct));
        rows.RootElement.EnumerateArray().Select(row => row.GetProperty("collection").GetProperty("reference").GetString())
            .Should().BeEquivalentTo(["CHQ-1", "CHQ-2"], "received and deposited are in hand; cleared and bounced are not");
        rows.RootElement.EnumerateArray().Should().AllSatisfy(row =>
        {
            row.GetProperty("extractNumber").GetInt32().Should().Be(1);
            ParseDecimal(row.GetProperty("collection").GetProperty("cashReceived")).Should().Be(60_000m);
        });
    }

    // ---- ux/journey-extract-chain-2026-11-18.md section 9, module 5, as real requests ------------------

    /// <summary>
    /// The collections chain: a cheque for part of the net is not cash until it clears; a transfer with withholding
    /// completes the extract; a second extract's cheque bounces and posts nothing; an approved credit note reduces
    /// what is expected and the reduced amount collects it. The ledger is read after every step.
    /// </summary>
    [Fact]
    public async Task Collections_chain_cheque_then_transfer_with_withholding_then_a_bounce_and_a_credit_note()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.None);
        Guid id = project.Id;
        await OkAsync(HttpMethod.Put, $"/api/projects/{id}/withholding", _finance, new { category = "ContractingAndSupplies" });

        async Task LedgerAsync(string step, decimal safe, decimal receivable, decimal tax, decimal hold)
        {
            (await NetIntoAsync(id, AccountType.Safe)).Should().Be(safe, $"safe after {step}");
            (await BalanceAsync(id, AccountType.ClientReceivable)).Should().Be(receivable, $"receivable after {step}");
            (await NetIntoAsync(id, AccountType.TaxWithheldAtSource)).Should().Be(tax, $"tax withheld after {step}");
            (await BalanceAsync(id, AccountType.Hold)).Should().Be(hold, $"hold after {step}");
        }

        // 1. Extract 1 issued: net 240,000 (spec.md section 15). The safe holds only the 250,000 advance.
        Guid first = await DriveAsync(project, ExtractStatus.Issued);
        await LedgerAsync("issue", safe: 250_000m, receivable: 240_000m, tax: 0m, hold: 60_000m);
        ParseDecimal((await CollectionFiguresAsync(id, first, _finance)).GetProperty("expectedWithheld")).Should().Be(2_400m);

        // 2. A cheque for part of the net: nothing moves, the extract is not Collected.
        await OkAsync(HttpMethod.Post, Step(id, first, "collect"), _finance, Collect("Cheque", 100_000m, 0m, "CHQ-100"));
        Guid cheque = await ChequeOfAsync(id, "CHQ-100");
        (await StatusOfAsync(first)).Should().Be(ExtractStatus.Issued);
        await LedgerAsync("cheque received", safe: 250_000m, receivable: 240_000m, tax: 0m, hold: 60_000m);

        // 3. Deposit, then clear: the safe rises by the cheque.
        await OkAsync(HttpMethod.Post, ChequeStep(id, cheque, "deposit"), _finance, null);
        await LedgerAsync("deposit", safe: 250_000m, receivable: 240_000m, tax: 0m, hold: 60_000m);
        await OkAsync(HttpMethod.Post, ChequeStep(id, cheque, "clearance"), _finance, null);
        (await StatusOfAsync(first)).Should().Be(ExtractStatus.Issued, "140,000 is still owed");
        await LedgerAsync("clearance", safe: 350_000m, receivable: 140_000m, tax: 0m, hold: 60_000m);

        // 4. The rest by transfer, the client withholding 1% of the net: Collected, withholding posted.
        await OkAsync(HttpMethod.Post, Step(id, first, "collect"), _finance, Collect("Transfer", 137_600m, 2_400m, "TRX-REST"));
        (await StatusOfAsync(first)).Should().Be(ExtractStatus.Collected);
        await LedgerAsync("transfer", safe: 487_600m, receivable: 0m, tax: 2_400m, hold: 60_000m);

        // 5. Extract 2 (section 15: net 120,000): its cheque bounces. Back to Issued with the reason, the safe unchanged.
        await ExecuteAsync(project, 600m);
        Guid second = await PrepareAsync(project);
        await OkAsync(HttpMethod.Post, Step(id, second, "qc-report"), _supervisor, QcBody(0m, 60_000m));
        await StepAsync(_client, project, second, ExtractStatus.QcSubmitted, ExtractStatus.Issued);
        await LedgerAsync("issue 2", safe: 487_600m, receivable: 120_000m, tax: 2_400m, hold: 120_000m);

        await OkAsync(HttpMethod.Post, Step(id, second, "collect"), _finance, Collect("Cheque", 118_800m, 1_200m, "CHQ-B2"));
        Guid bounced = await ChequeOfAsync(id, "CHQ-B2");
        await OkAsync(HttpMethod.Post, ChequeStep(id, bounced, "deposit"), _finance, null);
        await OkAsync(HttpMethod.Post, ChequeStep(id, bounced, "bounce"), _finance, new { reason = "returned unpaid" });
        (await StatusOfAsync(second)).Should().Be(ExtractStatus.Issued);
        JsonElement history = (await ReadAsync(id, second, _finance)).GetProperty("gateHistory");
        history.EnumerateArray().Last().GetProperty("reason").GetString().Should().Be("returned unpaid");
        await LedgerAsync("bounce", safe: 487_600m, receivable: 120_000m, tax: 2_400m, hold: 120_000m);

        // 6. A credit note of 20,000 is approved: 100,000 is expected, and paying it collects the extract.
        Guid note = await RaiseCreditNoteAsync(project, second, 20_000m);
        await OkAsync(HttpMethod.Post, AdjustmentStep(id, note, "approval"), _owner, null);
        ParseDecimal((await CollectionFiguresAsync(id, second, _finance)).GetProperty("remaining")).Should().Be(100_000m);
        await LedgerAsync("credit note", safe: 487_600m, receivable: 100_000m, tax: 2_400m, hold: 120_000m);

        await OkAsync(HttpMethod.Post, Step(id, second, "collect"), _finance, Collect("Transfer", 99_000m, 1_000m, "TRX-CN"));
        (await StatusOfAsync(second)).Should().Be(ExtractStatus.Collected);
        await LedgerAsync("collect 2", safe: 586_600m, receivable: 0m, tax: 3_400m, hold: 120_000m);
    }
}
