using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Auditing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// AC-500-E, D-250 item 1: a split Technical Office gate opens an NCR, and closing the NCR releases the held remainder.
/// The Technical Office closes it (<c>NcrCloser</c>, assumed), with the assignment. The test project certifies 300,000
/// of period work, so a 60% approval holds 120,000.
/// </summary>
public sealed partial class ExtractEndpointTests
{
    private static string Ncrs(Guid project) => $"/api/projects/{project}/ncrs";

    private static string CloseNcr(Guid project, Guid ncr) => $"{Ncrs(project)}/{ncr}/close";

    [Fact]
    public async Task A_split_gate_opens_an_NCR_and_closing_it_releases_the_held_portion_and_posts_nothing()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);

        HttpResponseMessage split = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "technical-approval"), _technicalOffice, new { approvedPortion = "0.6" });
        split.StatusCode.Should().Be(HttpStatusCode.OK, await split.Content.ReadAsStringAsync(Ct));

        JsonElement asOffice = await ReadAsync(project.Id, extract, _technicalOffice);
        asOffice.GetProperty("status").GetString().Should().Be(nameof(ExtractStatus.PartiallyApproved));
        JsonElement open = asOffice.GetProperty("openNcr");
        Guid ncr = open.GetProperty("id").GetGuid();
        asOffice.GetProperty("heldForNcrId").GetGuid().Should().Be(ncr, "the extract holds against the NCR the split opened");
        ParseDecimal(open.GetProperty("heldAmount")).Should().Be(120_000m);
        open.GetProperty("actions").GetProperty("close").GetBoolean().Should().BeTrue();

        JsonElement asFinance = (await ReadAsync(project.Id, extract, _finance)).GetProperty("openNcr");
        asFinance.GetProperty("actions").GetProperty("close").GetBoolean().Should().BeFalse("Finance does not close an NCR");
        (await ReadAsync(project.Id, extract, _prepEngineer)).GetProperty("openNcr").GetProperty("heldAmount").ValueKind
            .Should().Be(JsonValueKind.Null, "a site engineer sees no extract money");

        JsonElement listed = (await GetJsonAsync(Ncrs(project.Id), _technicalOffice)).EnumerateArray().Single();
        listed.GetProperty("id").GetGuid().Should().Be(ncr);
        listed.GetProperty("status").GetString().Should().Be(nameof(NcrStatus.Open));
        listed.GetProperty("extractNumber").GetInt32().Should().Be(1);

        long postings = await PostingCountAsync(project.Id);

        // Role and assignment: only an assigned Technical Office closes.
        Guid unassignedOffice = await NewUserAsync("ex-to-un", Role.TechnicalOffice);
        foreach (Guid refused in new[] { _owner, _finance, _supervisor, _prepEngineer, _hr, unassignedOffice })
        {
            (await SendAsync(HttpMethod.Post, CloseNcr(project.Id, ncr), refused, new { note = "fixed" })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        foreach (Guid refused in new[] { _hr, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Get, Ncrs(project.Id), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        HttpResponseMessage noNote = await SendAsync(HttpMethod.Post, CloseNcr(project.Id, ncr), _technicalOffice, new { note = " " });
        noNote.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(noNote)).Should().Be("extract.ncr_close_note_required");
        (await ReadAsync(project.Id, extract, _technicalOffice)).GetProperty("heldPortionReleasedAt").ValueKind
            .Should().Be(JsonValueKind.Null, "a refused close releases nothing");

        HttpResponseMessage closed = await SendAsync(HttpMethod.Post, CloseNcr(project.Id, ncr), _technicalOffice, new { note = "columns recast" });
        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync(Ct));
        using (JsonDocument body = JsonDocument.Parse(await closed.Content.ReadAsStringAsync(Ct)))
        {
            body.RootElement.GetProperty("status").GetString().Should().Be(nameof(NcrStatus.Closed));
            body.RootElement.GetProperty("closeNote").GetString().Should().Be("columns recast");
            body.RootElement.GetProperty("closedByUserId").GetGuid().Should().Be(_technicalOffice);
        }

        JsonElement after = await ReadAsync(project.Id, extract, _technicalOffice);
        after.GetProperty("openNcr").ValueKind.Should().Be(JsonValueKind.Null);
        after.GetProperty("heldPortionReleasedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        after.GetProperty("gateHistory").EnumerateArray().Last().GetProperty("gate").GetString()
            .Should().Be(nameof(ExtractGate.HeldPortionRelease));
        (await PostingCountAsync(project.Id)).Should().Be(postings, "the release is recorded and bills nothing");

        HttpResponseMessage again = await SendAsync(HttpMethod.Post, CloseNcr(project.Id, ncr), _technicalOffice, new { note = "again" });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(again)).Should().Be("extract.ncr_already_closed");

        await using KaffDbContext reader = _database.CreateBareContext();
        List<AuditRecord> trail = await reader.AuditRecords.Where(record => record.EntityId == ncr).ToListAsync(Ct);
        trail.Should().Contain(record => record.Action == AuditAction.Created);
        trail.Should().Contain(record => record.Action == AuditAction.Modified && record.Reason == "columns recast");
    }

    [Fact]
    public async Task A_second_split_of_the_same_extract_reuses_its_open_NCR_and_another_projects_NCR_is_not_found()
    {
        Project project = await NewProjectAsync();
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        object split = new { approvedPortion = "0.5" };
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "technical-approval"), _technicalOffice, split)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        Guid first = (await ReadAsync(project.Id, extract, _technicalOffice)).GetProperty("heldForNcrId").GetGuid();

        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "accounts-rejection"), _finance, new { reason = "material values wrong", kind = "MaterialValues" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await StepAsync(_client, project, extract, ExtractStatus.Draft, ExtractStatus.QcSubmitted);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "technical-approval"), _technicalOffice, split)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await ReadAsync(project.Id, extract, _technicalOffice)).GetProperty("heldForNcrId").GetGuid().Should().Be(first);
        (await GetJsonAsync(Ncrs(project.Id), _technicalOffice)).GetArrayLength().Should().Be(1);

        // A closed NCR is never reused: the next split opens a new one.
        (await SendAsync(HttpMethod.Post, CloseNcr(project.Id, first), _technicalOffice, new { note = "fixed" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "accounts-rejection"), _finance, new { reason = "material values wrong", kind = "MaterialValues" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await StepAsync(_client, project, extract, ExtractStatus.Draft, ExtractStatus.QcSubmitted);
        (await SendAsync(HttpMethod.Post, Step(project.Id, extract, "technical-approval"), _technicalOffice, split)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        JsonElement reread = await ReadAsync(project.Id, extract, _technicalOffice);
        reread.GetProperty("heldForNcrId").GetGuid().Should().NotBe(first);
        reread.GetProperty("openNcr").GetProperty("id").GetGuid().Should().Be(reread.GetProperty("heldForNcrId").GetGuid());
        (await GetJsonAsync(Ncrs(project.Id), _technicalOffice)).GetArrayLength().Should().Be(2);

        Project other = await NewProjectAsync();
        HttpResponseMessage foreign = await SendAsync(HttpMethod.Post, CloseNcr(other.Id, first), _technicalOffice, new { note = "x" });
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Splits extract one at 60% (300,000 of work, 120,000 held) and returns it with its NCR.</summary>
    private async Task<(Guid Extract, Guid Ncr)> SplitFirstAsync(Project project)
    {
        Guid extract = await DriveAsync(project, ExtractStatus.QcSubmitted);
        HttpResponseMessage split = await SendAsync(
            HttpMethod.Post, Step(project.Id, extract, "technical-approval"), _technicalOffice, new { approvedPortion = "0.6" });
        split.StatusCode.Should().Be(HttpStatusCode.OK, await split.Content.ReadAsStringAsync(Ct));
        return (extract, (await ReadAsync(project.Id, extract, _technicalOffice)).GetProperty("heldForNcrId").GetGuid());
    }

    private async Task CloseAsync(Project project, Guid ncr) =>
        (await SendAsync(HttpMethod.Post, CloseNcr(project.Id, ncr), _technicalOffice, new { note = "columns recast" })).StatusCode
            .Should().Be(HttpStatusCode.OK);

    private async Task<decimal> WorkValueAsync(Project project, Guid extract) =>
        Decimal((await ReadAsync(project.Id, extract, _owner)).GetProperty("document").GetProperty("figures"), "workValue");

    /// <summary>
    /// D-250 item 1, D-253, verification r2 proof P1: the remainder a split gate held is billed on the next extract
    /// prepared after the NCR closes. 180,000 billed on extract one; extract two bills the released 120,000 plus its own
    /// 100,000, and the hold accrues on the released work when it is billed.
    /// </summary>
    [Fact]
    public async Task The_released_remainder_is_billed_on_the_next_extract_prepared_after_the_NCR_closes()
    {
        Project project = await NewProjectAsync();
        (Guid first, Guid ncr) = await SplitFirstAsync(project);

        JsonElement split = await ReadAsync(project.Id, first, _technicalOffice);
        ParseDecimal(split.GetProperty("approvedWork")).Should().Be(180_000m);
        ParseDecimal(split.GetProperty("heldWork")).Should().Be(120_000m);
        JsonElement blind = await ReadAsync(project.Id, first, _prepEngineer);
        blind.GetProperty("approvedWork").ValueKind.Should().Be(JsonValueKind.Null, "a site engineer sees no extract money");
        blind.GetProperty("heldWork").ValueKind.Should().Be(JsonValueKind.Null);

        await CloseAsync(project, ncr);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);
        (await WorkValueAsync(project, first)).Should().Be(180_000m, "extract one bills only the approved portion");

        await ExecuteAsync(project, 400m);
        Guid second = await PrepareAsync(project);
        (decimal cumulative, decimal previous) = await EvidenceAsync(project.Id, second);
        previous.Should().Be(180_000m, "previously certified is what earlier extracts billed");
        (cumulative - previous).Should().Be(220_000m);

        await StepAsync(_client, project, second, ExtractStatus.Draft, ExtractStatus.Issued);
        JsonElement figures = (await ReadAsync(project.Id, second, _owner)).GetProperty("document").GetProperty("figures");
        Decimal(figures, "workValue").Should().Be(220_000m);
        Decimal(figures, "holdThisPeriod").Should().Be(44_000m, "the hold is taken on the released work when it is billed");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(80_000m, "20% of the 400,000 billed in all");
    }

    /// <summary>
    /// D-253: while the NCR is open the remainder stays held and the next extract does not bill it. An NCR closed after
    /// that extract was prepared is billed on the next one prepared after the close, never on the one already in flight.
    /// </summary>
    [Fact]
    public async Task An_open_NCR_keeps_the_remainder_held_and_a_late_close_bills_it_on_the_next_extract_prepared()
    {
        Project project = await NewProjectAsync();
        (Guid first, Guid ncr) = await SplitFirstAsync(project);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);

        await ExecuteAsync(project, 400m);
        Guid second = await PrepareAsync(project);
        (decimal cumulative, decimal previous) = await EvidenceAsync(project.Id, second);
        previous.Should().Be(300_000m, "the open NCR's 120,000 stays held, off this extract too");
        (cumulative - previous).Should().Be(100_000m);

        await CloseAsync(project, ncr);
        await StepAsync(_client, project, second, ExtractStatus.Draft, ExtractStatus.Issued);
        (await WorkValueAsync(project, second)).Should().Be(100_000m, "a close after prepare does not reopen the extract in flight");

        await ExecuteAsync(project, 450m);
        Guid third = await PrepareAsync(project);
        (cumulative, previous) = await EvidenceAsync(project.Id, third);
        previous.Should().Be(280_000m, "180,000 on extract one and 100,000 on extract two");
        (cumulative - previous).Should().Be(170_000m, "the released 120,000 plus the period's own 50,000");
    }

    /// <summary>
    /// spec.md section 15's invariant over a project with a split gate: every unit of contract work is billed exactly once,
    /// so client cash can still equal the contract value, and the hold accrues on all of it.
    /// </summary>
    [Fact]
    public async Task A_project_with_a_split_gate_still_bills_its_whole_contract_value_exactly_once()
    {
        Project project = await NewProjectAsync();
        (Guid first, Guid ncr) = await SplitFirstAsync(project);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);
        await CloseAsync(project, ncr);

        await ExecuteAsync(project, 1_000m);
        Guid second = await DriveAsync(project, ExtractStatus.Issued);

        ((await WorkValueAsync(project, first)) + (await WorkValueAsync(project, second)))
            .Should().Be(1_000_000m, "the frozen BOQ's contract value: 1,000 m3 at 1,000");
        (await BalanceAsync(project.Id, AccountType.Hold)).Should().Be(200_000m, "20% of the contract value");
    }

    // ---- D-254 item 2: handover waits on open NCRs and on released remainders not yet billed --------------

    private async Task<HttpResponseMessage> HandOverAsync(Project project)
    {
        await using (KaffDbContext context = _database.CreateContext())
        {
            Project loaded = await context.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct);
            loaded.TransitionTo(ProjectStatus.HandoverPending, new DateOnly(2026, 12, 1), "test").IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        return await SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/handover", _technicalOffice, null);
    }

    private static async Task<string?> ErrorOfAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync(Ct));
        return await ErrorCodeAsync(response);
    }

    [Fact]
    public async Task Handover_is_refused_while_an_NCR_on_the_project_is_open()
    {
        Project project = await NewProjectAsync();
        (Guid first, _) = await SplitFirstAsync(project);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);

        (await ErrorOfAsync(await HandOverAsync(project))).Should().Be("closure.ncr_open");
    }

    [Fact]
    public async Task Handover_is_refused_while_a_released_remainder_is_not_yet_billed_on_an_extract()
    {
        Project project = await NewProjectAsync();
        (Guid first, Guid ncr) = await SplitFirstAsync(project);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);
        await CloseAsync(project, ncr);

        (await ErrorOfAsync(await HandOverAsync(project))).Should().Be("closure.released_remainder_unbilled");
    }

    /// <summary>D-253 item 4: a remainder released after the next extract was prepared is still unbilled on that extract.</summary>
    [Fact]
    public async Task Handover_is_refused_when_the_NCR_closed_after_the_last_extract_was_prepared()
    {
        Project project = await NewProjectAsync();
        (Guid first, Guid ncr) = await SplitFirstAsync(project);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);
        await ExecuteAsync(project, 1_000m);
        Guid second = await DriveAsync(project, ExtractStatus.Issued);
        await CloseAsync(project, ncr);

        (await ErrorOfAsync(await HandOverAsync(project))).Should().Be("closure.released_remainder_unbilled");
        (await WorkValueAsync(project, second)).Should().Be(700_000m, "the extract in flight did not bill the late release");
    }

    [Fact]
    public async Task Handover_proceeds_once_the_NCR_is_closed_and_its_remainder_is_billed()
    {
        Project project = await NewProjectAsync();
        (Guid first, Guid ncr) = await SplitFirstAsync(project);
        await StepAsync(_client, project, first, ExtractStatus.TechnicalApproved, ExtractStatus.Issued);
        await CloseAsync(project, ncr);
        await ExecuteAsync(project, 1_000m);
        await DriveAsync(project, ExtractStatus.Issued);

        HttpResponseMessage response = await HandOverAsync(project);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// D-257, an open question for Nabil, not a rule: no extract gate checks the project's status, so after handover an
    /// extract can still be prepared and split, and the split opens an NCR on a handed-over project. That bypasses the
    /// D-255 refusal, which runs only at handover. This test records today's behaviour so a ruling has to change it on
    /// purpose; when Nabil rules, it is rewritten to assert the ruling.
    /// </summary>
    [Fact]
    public async Task After_handover_an_extract_can_still_be_split_and_open_an_NCR_pending_a_ruling()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ExtractStatus.Issued);
        HttpResponseMessage handover = await HandOverAsync(project);
        handover.StatusCode.Should().Be(HttpStatusCode.OK, await handover.Content.ReadAsStringAsync(Ct));

        await ExecuteAsync(project, 1_000m);
        Guid second = await DriveAsync(project, ExtractStatus.QcSubmitted);
        HttpResponseMessage split = await SendAsync(
            HttpMethod.Post, Step(project.Id, second, "technical-approval"), _technicalOffice, new { approvedPortion = "0.6" });

        split.StatusCode.Should().Be(HttpStatusCode.OK, await split.Content.ReadAsStringAsync(Ct));
        await using KaffDbContext context = _database.CreateContext();
        (await context.Projects.SingleAsync(candidate => candidate.Id == project.Id, Ct)).Status.Should().Be(ProjectStatus.Handover);
        (await context.Ncrs.CountAsync(ncr => ncr.ProjectId == project.Id && ncr.Status == NcrStatus.Open, Ct)).Should().Be(1);
    }

    private async Task<Guid> NewUserAsync(string name, Role role)
    {
        User user = Make(name, role, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        await using KaffDbContext context = _database.CreateBareContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }
}
