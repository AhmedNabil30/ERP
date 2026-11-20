using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Auditing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// D-260, after <c>ux/consultant-review-extracts-app-2026-11-20-r2.md</c>: the NCR description, the split and deductions
/// previews, the credit note's effect, the list's credit note line and the dispute resolutions offered. The test project
/// certifies 300,000 of period work on extract one.
/// </summary>
public sealed partial class ExtractEndpointTests
{
    private const string Defect = "honeycombing in column C3";

    private static string PortionPreview(Guid project, Guid extract, string portion) =>
        $"/api/projects/{project}/extracts/{extract}/portion-preview?portion={portion}";

    [Fact]
    public async Task A_split_without_a_description_is_refused_and_opens_no_NCR_and_the_description_is_read_and_audited()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        string route = Step(project.Id, extract, "technical-approval");

        foreach (object blank in new object[] { new { approvedPortion = "0.6" }, new { approvedPortion = "0.6", ncrDescription = "  " } })
        {
            HttpResponseMessage refused = await SendAsync(HttpMethod.Post, route, _technicalOffice, blank);
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync(Ct));
            (await ErrorCodeAsync(refused)).Should().Be("extract.ncr_description_required");
        }

        await using (KaffDbContext context = _database.CreateBareContext())
        {
            (await context.Ncrs.CountAsync(ncr => ncr.ProjectId == project.Id, Ct)).Should().Be(0, "a refused split opens no NCR");
            (await context.Extracts.SingleAsync(candidate => candidate.Id == extract, Ct)).Status.Should().Be(ExtractStatus.QcSubmitted);
        }

        (await SendAsync(HttpMethod.Post, route, _technicalOffice, new { approvedPortion = "1" })).StatusCode
            .Should().Be(HttpStatusCode.OK, "a whole approval needs no description");

        Project second = await NewProjectAsync();
        Guid split = await DriveAsync(second, ExtractStatus.QcSubmitted);
        HttpResponseMessage passed = await SendAsync(
            HttpMethod.Post, Step(second.Id, split, "technical-approval"), _technicalOffice,
            new { approvedPortion = "0.6", ncrDescription = $"  {Defect} " });
        passed.StatusCode.Should().Be(HttpStatusCode.OK, await passed.Content.ReadAsStringAsync(Ct));

        JsonElement open = (await ReadAsync(second.Id, split, _owner)).GetProperty("openNcr");
        open.GetProperty("description").GetString().Should().Be(Defect);
        (await ReadAsync(second.Id, split, _prepEngineer)).GetProperty("openNcr").GetProperty("description").GetString()
            .Should().Be(Defect, "the description is not money");
        (await GetJsonAsync(Ncrs(second.Id), _technicalOffice)).EnumerateArray().Single().GetProperty("description").GetString()
            .Should().Be(Defect);

        await using KaffDbContext reader = _database.CreateBareContext();
        Guid ncrId = open.GetProperty("id").GetGuid();
        AuditRecord created = await reader.AuditRecords.SingleAsync(record => record.EntityId == ncrId && record.Action == AuditAction.Created, Ct);
        created.AfterJson.Should().Contain(Defect);
    }

    [Fact]
    public async Task The_portion_preview_gives_the_split_the_read_then_shows_and_only_the_gate_reaches_it()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);

        JsonElement preview = await GetJsonAsync(PortionPreview(project.Id, extract, "0.7"), _technicalOffice);
        ParseDecimal(preview.GetProperty("approvedWork")).Should().Be(210_000m);
        ParseDecimal(preview.GetProperty("heldWork")).Should().Be(90_000m);

        foreach (Guid refused in new[] { _owner, _finance, _supervisor, _prepEngineer, _hr, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Get, PortionPreview(project.Id, extract, "0.7"), refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        HttpResponseMessage outOfRange = await SendAsync(HttpMethod.Get, PortionPreview(project.Id, extract, "1.5"), _technicalOffice, null);
        outOfRange.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(outOfRange)).Should().Be("extract.approved_portion_out_of_range");

        (await SendAsync(
                HttpMethod.Post, Step(project.Id, extract, "technical-approval"), _technicalOffice,
                new { approvedPortion = "0.7", ncrDescription = Defect }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement read = await ReadAsync(project.Id, extract, _technicalOffice);
        ParseDecimal(read.GetProperty("approvedWork")).Should().Be(210_000m, "the preview and the read derive the split once");
        ParseDecimal(read.GetProperty("heldWork")).Should().Be(90_000m);

        (await SendAsync(HttpMethod.Get, PortionPreview(project.Id, extract, "0.7"), _technicalOffice, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the gate has passed");
    }

    [Fact]
    public async Task The_deductions_preview_shows_the_figures_applying_them_then_writes_and_only_Finance_reaches_it()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        string route = $"/api/projects/{project.Id}/extracts/{extract}/deductions-preview";

        (await SendAsync(HttpMethod.Get, route, _finance, null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "the extract is not at Accounts yet");

        await StepAsync(_client, project, extract, ExtractStatus.QcSubmitted, ExtractStatus.TechnicalApproved);
        foreach (Guid refused in new[] { _owner, _technicalOffice, _supervisor, _prepEngineer, _hr, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Get, route, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        long postings = await PostingCountAsync(project.Id);
        JsonElement preview = await GetJsonAsync(route, _finance);
        (await PostingCountAsync(project.Id)).Should().Be(postings, "a preview posts nothing");
        ParseDecimal(preview.GetProperty("gross")).Should().Be(300_000m);
        ParseDecimal(preview.GetProperty("holdThisPeriod")).Should().Be(60_000m, "20% of the period's work");

        await StepAsync(_client, project, extract, ExtractStatus.TechnicalApproved, ExtractStatus.DeductionsApplied);
        JsonElement document = (await ReadAsync(project.Id, extract, _finance)).GetProperty("document");
        static List<(string?, string?, bool, decimal)> Rows(JsonElement column) =>
        [
            .. column.EnumerateArray().Select(row => (
                row.GetProperty("key").GetString(), row.GetProperty("labelKey").GetString(),
                row.GetProperty("isDeduction").GetBoolean(), ParseDecimal(row.GetProperty("amount")))),
        ];
        Rows(preview.GetProperty("column")).Should().Equal(Rows(document.GetProperty("column")), "the same rows are printed");
        ParseDecimal(preview.GetProperty("netPayable")).Should().Be(ParseDecimal(document.GetProperty("printedTotal")));
        ParseDecimal(preview.GetProperty("holdToDate")).Should().Be(Decimal(document.GetProperty("figures"), "holdToDate"));
    }

    [Fact]
    public async Task A_raised_credit_note_carries_the_extract_figure_after_it_and_the_list_names_the_Owner_as_the_page_does()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.Issued);
        decimal expected = ParseDecimal((await ReadAsync(project.Id, extract, _owner)).GetProperty("collection").GetProperty("expected"));
        Guid note = await RaiseCreditNoteAsync(project, extract, 10_000m);
        string adjustment = $"{Adjustments(project.Id)}/{note}";

        ParseDecimal((await GetJsonAsync(adjustment, _owner)).GetProperty("extractDueAfter")).Should().Be(expected - 10_000m);
        ParseDecimal((await GetJsonAsync(Adjustments(project.Id), _owner)).EnumerateArray().Single().GetProperty("extractDueAfter"))
            .Should().Be(expected - 10_000m, "the list read derives it the same way");

        // Finding F5: the list row and the page name the same step and the same people.
        JsonElement page = (await ReadAsync(project.Id, extract, _owner)).GetProperty("creditNoteWaitingOn");
        page.GetProperty("taskKey").GetString().Should().Be("waiting.task.adjustment_approval");
        foreach (Guid viewer in new[] { _owner, _prepEngineer })
        {
            JsonElement row = (await GetJsonAsync(Extracts(project.Id), viewer)).EnumerateArray().Single();
            row.GetProperty("creditNoteWaitingOn").GetRawText().Should().Be(page.GetRawText());
        }

        (await SendAsync(HttpMethod.Post, AdjustmentStep(project.Id, note, "approval"), _owner, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        ParseDecimal((await GetJsonAsync(adjustment, _owner)).GetProperty("extractDueAfter")).Should().Be(expected - 10_000m);
        ParseDecimal((await ReadAsync(project.Id, extract, _owner)).GetProperty("collection").GetProperty("expected"))
            .Should().Be(expected - 10_000m, "after approval the extract's own figure is the one the note showed");
        (await GetJsonAsync(Extracts(project.Id), _owner)).EnumerateArray().Single().GetProperty("creditNoteWaitingOn").ValueKind
            .Should().Be(JsonValueKind.Null, "a decided note holds nothing");
    }

    private async Task<string[]> ResolutionsAsync(Guid project, Guid extract, Guid actor) =>
        [.. (await ReadAsync(project, extract, actor)).GetProperty("disputeResolutions").EnumerateArray().Select(kind => kind.GetString()!)];

    [Fact]
    public async Task The_read_offers_only_the_dispute_resolutions_the_route_accepts()
    {
        Project alone = await NewProjectAsync();
        Guid only = await DriveAsync(alone, ExtractStatus.Issued);
        (await SendAsync(HttpMethod.Post, Step(alone.Id, only, "dispute"), _finance, new { reason = "quantities" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await ResolutionsAsync(alone.Id, only, _finance)).Should().Equal("CreditNote", "RevisedExtract");
        (await ResolutionsAsync(alone.Id, only, _owner)).Should().BeEmpty("the Owner does not resolve a dispute");

        Project project = await NewProjectAsync();
        Guid first = await DriveAsync(project, ExtractStatus.Issued);
        await ExecuteAsync(project, 400m);
        await PrepareAsync(project);
        (await SendAsync(HttpMethod.Post, Step(project.Id, first, "dispute"), _finance, new { reason = "quantities" })).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await ResolutionsAsync(project.Id, first, _finance)).Should().Equal(["CreditNote"], "a later extract exists");
        HttpResponseMessage revised = await SendAsync(
            HttpMethod.Post, Step(project.Id, first, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "quantities", amount = "0" });
        revised.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(revised)).Should().Be("extract.revision_not_latest", "the read and the route agree");
    }
}
