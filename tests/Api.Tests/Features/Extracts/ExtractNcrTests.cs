using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Auditing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
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

    private async Task<Guid> NewUserAsync(string name, Role role)
    {
        User user = Make(name, role, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        await using KaffDbContext context = _database.CreateBareContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }
}
