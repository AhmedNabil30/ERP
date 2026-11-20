using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Extracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// D-246's "Not built" list: the reads the extract chain screens still lacked. Each figure reaches the money roles only,
/// and each action flag is the answer the route itself would give the same caller now.
/// </summary>
public sealed partial class ExtractEndpointTests
{
    private static JsonValueKind KindOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? value.ValueKind : JsonValueKind.Undefined;

    private async Task<JsonElement> ProjectReadAsync(Guid project, Guid actor) =>
        await GetJsonAsync($"/api/projects/{project}", actor);

    private async Task<JsonElement> ListRowAsync(Guid project, Guid extract, Guid actor) =>
        (await GetJsonAsync(Extracts(project), actor)).EnumerateArray()
            .Single(row => row.GetProperty("id").GetGuid() == extract).Clone();

    private async Task<Dictionary<string, bool>> FlagsAsync(string route, Guid actor) =>
        (await GetJsonAsync(route, actor)).GetProperty("actions").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetBoolean());

    // ---- item 1: the contract's withholding on the project read -----------------------------------------

    [Fact]
    public async Task The_project_read_carries_the_withholding_category_to_the_money_roles_and_offers_its_edit_to_finance_and_the_owner()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.ContractingAndSupplies);

        foreach (Guid editor in new[] { _finance, _owner })
        {
            JsonElement read = await ProjectReadAsync(project.Id, editor);
            read.GetProperty("withholdingCategory").GetString().Should().Be("ContractingAndSupplies");
            read.GetProperty("actions").GetProperty("editWithholding").GetBoolean().Should().BeTrue();
        }

        JsonElement engineer = await ProjectReadAsync(project.Id, _prepEngineer);
        KindOf(engineer, "withholdingCategory").Should().NotBe(JsonValueKind.String, "a junior engineer does not hold ProjectMoneyRead");
        engineer.GetProperty("actions").GetProperty("editWithholding").GetBoolean().Should().BeFalse();
        (await ProjectReadAsync(project.Id, _technicalOffice)).GetProperty("actions").GetProperty("editWithholding").GetBoolean()
            .Should().BeFalse("the Technical Office does not hold ProjectFinancialsEdit");

        Project individual = await NewProjectAsync(_individualClientId, WithholdingCategory.None, ClientKind.Individual);
        (await ProjectReadAsync(individual.Id, _finance)).GetProperty("actions").GetProperty("editWithholding").GetBoolean()
            .Should().BeFalse("an individual client's contract carries no withholding, so there is nothing to edit");
    }

    // ---- item 6: the prepare flag on the project read ------------------------------------------------

    [Fact]
    public async Task The_prepare_flag_follows_the_prepare_routes_own_refusals()
    {
        Project project = await NewProjectAsync();
        bool Prepare(JsonElement read) => read.GetProperty("actions").GetProperty("prepareExtract").GetBoolean();

        Prepare(await ProjectReadAsync(project.Id, _prepEngineer)).Should().BeTrue();
        foreach (Guid other in new[] { _owner, _finance, _technicalOffice })
        {
            Prepare(await ProjectReadAsync(project.Id, other)).Should().BeFalse("only an assigned site engineer prepares");
        }

        Guid extract = await PrepareAsync(project);
        Prepare(await ProjectReadAsync(project.Id, _prepEngineer)).Should().BeFalse("extract.another_extract_open");
        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, Extracts(project.Id), _prepEngineer, PrepareBody());
        (await ErrorCodeAsync(refused)).Should().Be("extract.another_extract_open", "the flag and the route agree");

        await StepAsync(_client, project, extract, ExtractStatus.Draft, ExtractStatus.Issued);
        Prepare(await ProjectReadAsync(project.Id, _prepEngineer)).Should().BeTrue("the first extract is issued");
    }

    // ---- item 2: cheques in hand, remaining and the revision link on the list ---------------------------

    [Fact]
    public async Task The_list_row_carries_cheques_in_hand_and_remaining_from_the_same_position_as_the_extract()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 100_000m, 0m, "CHQ-L1"));
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 40_000m, 0m, "TRX-L1"));

        JsonElement money = (await ListRowAsync(project.Id, extract, _finance)).GetProperty("money");
        JsonElement position = await CollectionFiguresAsync(project.Id, extract, _finance);
        ParseDecimal(money.GetProperty("inCheques")).Should().Be(100_000m);
        ParseDecimal(money.GetProperty("remaining")).Should().Be(100_000m, "240,000 less 40,000 collected less the 100,000 cheque in hand");
        ParseDecimal(money.GetProperty("inCheques")).Should().Be(ParseDecimal(position.GetProperty("inCheques")));
        ParseDecimal(money.GetProperty("remaining")).Should().Be(ParseDecimal(position.GetProperty("remaining")));

        (await ListRowAsync(project.Id, extract, _supervisor)).GetProperty("money").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_list_links_a_cancelled_extract_and_its_revision_both_ways()
    {
        Project project = await NewProjectAsync();
        Guid original = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute"), _finance, new { reason = "quantities overstated" });
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "remeasured" });
        HttpResponseMessage prepared = await SendAsync(
            HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-09-01", periodEnd = "2026-09-30", revisesExtractId = original });
        prepared.StatusCode.Should().Be(HttpStatusCode.Created, await prepared.Content.ReadAsStringAsync(Ct));
        Guid revision;
        using (JsonDocument body = JsonDocument.Parse(await prepared.Content.ReadAsStringAsync(Ct)))
        {
            revision = body.RootElement.GetProperty("id").GetGuid();
        }

        foreach (Guid reader in new[] { _finance, _supervisor })
        {
            JsonElement originalRow = await ListRowAsync(project.Id, original, reader);
            originalRow.GetProperty("revisedBy").GetProperty("number").GetInt32().Should().Be(2);
            originalRow.GetProperty("revises").ValueKind.Should().Be(JsonValueKind.Null);
            JsonElement revisionRow = await ListRowAsync(project.Id, revision, reader);
            revisionRow.GetProperty("revises").GetProperty("number").GetInt32().Should().Be(1);
            revisionRow.GetProperty("revises").GetProperty("status").GetString().Should().Be("Void");
            revisionRow.GetProperty("revisedBy").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    // ---- item 3: names in the history --------------------------------------------------------------

    [Fact]
    public async Task Each_gate_record_names_its_actor()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        JsonElement history = (await ReadAsync(project.Id, extract, _supervisor)).GetProperty("gateHistory");
        history.EnumerateArray().Select(record => record.GetProperty("actorName").GetString())
            .Should().Equal("ex-prep", "ex-sup", "ex-to", "ex-fin", "ex-owner", "ex-owner");
    }

    // ---- items 4 and 5: the withholding name, the credit notes and their waiting line on the extract ------

    [Fact]
    public async Task The_extract_read_names_the_withholding_category_to_the_money_roles_only()
    {
        Project project = await NewProjectAsync(withholding: WithholdingCategory.Services);
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);

        (await ReadAsync(project.Id, extract, _finance)).GetProperty("withholdingCategory").GetString().Should().Be("Services");
        (await ReadAsync(project.Id, extract, _supervisor)).GetProperty("withholdingCategory").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_extract_read_lists_its_raised_and_approved_credit_notes_and_waits_on_the_owner_for_a_raised_one()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid rejected = await RaiseCreditNoteAsync(project, extract, 5_000m);
        await OkAsync(HttpMethod.Post, AdjustmentStep(project.Id, rejected, "rejection"), _owner, new { reason = "not agreed" });
        Guid note = await RaiseCreditNoteAsync(project, extract, 10_000m);

        JsonElement read = await ReadAsync(project.Id, extract, _finance);
        JsonElement only = read.GetProperty("creditNotes").EnumerateArray().Should().ContainSingle("a rejected note is not on the extract").Subject;
        only.GetProperty("id").GetGuid().Should().Be(note);
        only.GetProperty("status").GetString().Should().Be("Raised");
        ParseDecimal(only.GetProperty("amount")).Should().Be(10_000m);
        JsonElement waiting = read.GetProperty("creditNoteWaitingOn");
        waiting.GetProperty("taskKey").GetString().Should().Be("waiting.task.adjustment_approval");
        waiting.GetProperty("permission").GetString().Should().Be("FinancialMovementApprove");
        Guid[] holders = [.. waiting.GetProperty("holders").EnumerateArray().Select(member => member.GetProperty("userId").GetGuid())];
        holders.Should().Contain(_owner).And.NotContain(_finance, "only the Owner approves a credit note");

        JsonElement engineer = await ReadAsync(project.Id, extract, _supervisor);
        engineer.GetProperty("creditNotes").EnumerateArray().Single().GetProperty("amount").ValueKind
            .Should().Be(JsonValueKind.Null, "a site engineer reads no money");

        await OkAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null);
        JsonElement decided = await ReadAsync(project.Id, extract, _finance);
        decided.GetProperty("creditNotes").EnumerateArray().Single().GetProperty("status").GetString().Should().Be("Approved");
        decided.GetProperty("creditNoteWaitingOn").ValueKind.Should().Be(JsonValueKind.Null, "nothing waits once the Owner decided");
    }

    // ---- item 6: the adjustment's and the cheque's action flags -------------------------------------

    [Fact]
    public async Task The_adjustment_read_offers_each_decision_only_to_the_caller_the_route_would_let_take_it()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        Guid note = await RaiseCreditNoteAsync(project, extract, 10_000m);
        string route = $"{Adjustments(project.Id)}/{note}";

        (await FlagsAsync(route, _owner)).Should().Equal(new Dictionary<string, bool> { ["approve"] = true, ["reject"] = true, ["reverse"] = false });
        (await FlagsAsync(route, _finance)).Values.Should().AllSatisfy(flag => flag.Should().BeFalse("Finance raised it and holds no approval"));
        (await GetJsonAsync(Adjustments(project.Id), _owner)).EnumerateArray().Single().GetProperty("actions").GetProperty("approve")
            .GetBoolean().Should().BeTrue("the list carries the same flags");

        await OkAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null);
        (await FlagsAsync(route, _owner)).Should().Equal(new Dictionary<string, bool> { ["approve"] = false, ["reject"] = false, ["reverse"] = true });

        // D-239 item 3: once the extract is Collected, the reversal is refused, and so the flag is down.
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Transfer", 230_000m, 0m, "TRX-ADJ"));
        (await StatusOfAsync(extract)).Should().Be(ExtractStatus.Collected);
        (await FlagsAsync(route, _owner))["reverse"].Should().BeFalse();
        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "reversal"), _owner, new { reason = "mistake" });
        refused.StatusCode.Should().NotBe(HttpStatusCode.OK, "the flag and the route agree");
    }

    [Fact]
    public async Task The_cheque_read_offers_deposit_then_clear_or_bounce_to_finance_only()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "collect"), _finance, Collect("Cheque", 100_000m, 0m, "CHQ-F1"));
        Guid cheque = await ChequeOfAsync(project.Id, "CHQ-F1");
        string route = $"{Cheques(project.Id)}/{cheque}";

        (await FlagsAsync(route, _finance)).Should().Equal(new Dictionary<string, bool> { ["deposit"] = true, ["clear"] = false, ["bounce"] = false });
        (await FlagsAsync(route, _owner)).Values.Should().AllSatisfy(flag => flag.Should().BeFalse("the Owner reads cheques and moves none"));
        (await GetJsonAsync(Cheques(project.Id), _finance)).EnumerateArray().Single().GetProperty("actions").GetProperty("deposit")
            .GetBoolean().Should().BeTrue("the list carries the same flags");

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "deposit"), _finance, null);
        (await FlagsAsync(route, _finance)).Should().Equal(new Dictionary<string, bool> { ["deposit"] = false, ["clear"] = true, ["bounce"] = true });

        await OkAsync(HttpMethod.Post, ChequeStep(project.Id, cheque, "clearance"), _finance, null);
        (await FlagsAsync(route, _finance)).Values.Should().AllSatisfy(flag => flag.Should().BeFalse("a cleared cheque moves no more"));
    }
}
