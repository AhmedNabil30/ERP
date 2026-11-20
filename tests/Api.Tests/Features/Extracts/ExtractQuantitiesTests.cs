using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// D-248: an extract keeps each BOQ line's cumulative executed quantity, copied at prepare. The period quantity is that
/// figure less the same line on the previous extract (a revision uses the original's previous). Money per line follows
/// the frozen BOQ rate and must foot to the extract's own certified work. The test project's one line is priced at 1,000.
/// </summary>
public sealed partial class ExtractEndpointTests
{
    private static string Quantities(Guid project, Guid extract) => $"{Single(project, extract)}/quantities";

    private static string QuantitiesPreview(Guid project) => $"{Extracts(project)}/quantities-preview";

    private async Task<JsonElement> QuantityLineAsync(string route, Guid actor) =>
        (await GetJsonAsync(route, actor)).EnumerateArray().Single().Clone();

    private static decimal? Nullable(JsonElement row, string name) =>
        row.GetProperty(name).ValueKind == JsonValueKind.Null ? null : ParseDecimal(row.GetProperty(name));

    private static void ShouldRead(JsonElement row, decimal? previous, decimal? current, decimal cumulative)
    {
        Nullable(row, "previousQuantity").Should().Be(previous);
        Nullable(row, "currentQuantity").Should().Be(current);
        ParseDecimal(row.GetProperty("cumulativeQuantity")).Should().Be(cumulative);
    }

    private async Task<(decimal Cumulative, decimal Previous)> EvidenceAsync(Guid project, Guid extract)
    {
        JsonElement evidence = (await ReadAsync(project, extract, _owner)).GetProperty("evidence");
        return (ParseDecimal(evidence.GetProperty("cumulativeCertifiedWork")), ParseDecimal(evidence.GetProperty("previouslyCertifiedWork")));
    }

    /// <summary>Each money column of the row against the extract's own certified work: the two can never disagree.</summary>
    private async Task ShouldFootAsync(Guid project, Guid extract)
    {
        JsonElement row = await QuantityLineAsync(Quantities(project, extract), _finance);
        (decimal cumulative, decimal previous) = await EvidenceAsync(project, extract);
        ParseDecimal(row.GetProperty("rate")).Should().Be(1_000m, "the frozen BOQ rate");
        ParseDecimal(row.GetProperty("cumulativeAmount")).Should().Be(cumulative);
        ParseDecimal(row.GetProperty("previousAmount")).Should().Be(previous);
        ParseDecimal(row.GetProperty("currentAmount")).Should().Be(cumulative - previous);
    }

    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Prepare_copies_each_lines_quantity_and_a_later_daily_log_does_not_reach_the_extract()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);
        await ExecuteAsync(project, 400m);

        JsonElement row = await QuantityLineAsync(Quantities(project.Id, extract), _finance);
        row.GetProperty("descriptionAr").GetString().Should().Be("خرسانة");
        row.GetProperty("unit").GetString().Should().Be("m3");
        ShouldRead(row, 0m, 300m, 300m);
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            Guid lineId = row.GetProperty("boqLineId").GetGuid();
            Kaff.Domain.Boqs.BoqLine bab = (await reader.Boqs.Include(boq => boq.Lines).Where(boq => boq.ProjectId == project.Id).SingleAsync(Ct))
                .Lines.Single(line => line.Id == lineId);
            row.GetProperty("babCode").GetString().Should().Be(bab.BabCode, "the row carries its line's frozen باب so a screen can band by it");
            row.GetProperty("babNameAr").GetString().Should().Be(bab.BabNameAr);
            row.GetProperty("babNameEn").GetString().Should().Be(bab.BabNameEn);
        }

        await ShouldFootAsync(project.Id, extract);
    }

    [Fact]
    public async Task A_viewer_without_money_access_reads_the_quantities_and_no_rate_or_amount()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);

        foreach (Guid reader in new[] { _prepEngineer, _supervisor })
        {
            JsonElement row = await QuantityLineAsync(Quantities(project.Id, extract), reader);
            ShouldRead(row, 0m, 300m, 300m);
            foreach (string money in new[] { "rate", "previousAmount", "currentAmount", "cumulativeAmount" })
            {
                row.GetProperty(money).ValueKind.Should().Be(JsonValueKind.Null, money);
            }
        }
    }

    [Fact]
    public async Task The_next_extract_subtracts_the_previous_extracts_same_line()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ExtractStatus.Issued);
        await ExecuteAsync(project, 400m);
        Guid second = await PrepareAsync(project);

        ShouldRead(await QuantityLineAsync(Quantities(project.Id, second), _finance), 300m, 100m, 400m);
        await ShouldFootAsync(project.Id, second);
    }

    [Fact]
    public async Task A_revised_rejected_extract_reads_the_quantities_it_was_revised_from()
    {
        Project project = await NewProjectAsync();
        Guid extract = await RejectedAsync(project);
        await ExecuteAsync(project, 350m);
        await OkAsync(HttpMethod.Post, Step(project.Id, extract, "evidence-revision"), _prepEngineer, ReviseBody());

        ShouldRead(await QuantityLineAsync(Quantities(project.Id, extract), _finance), 0m, 350m, 350m);
        await ShouldFootAsync(project.Id, extract);
    }

    [Fact]
    public async Task The_revision_of_a_cancelled_extract_subtracts_the_originals_previous()
    {
        Project project = await NewProjectAsync();
        await DriveAsync(project, ExtractStatus.Issued);
        await ExecuteAsync(project, 400m);
        Guid original = await DriveAsync(project, ExtractStatus.Issued);
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute"), _finance, new { reason = "quantities overstated" });
        await OkAsync(HttpMethod.Post, Step(project.Id, original, "dispute-resolution"), _finance, new { kind = "RevisedExtract", reason = "remeasured" });
        await ExecuteAsync(project, 450m);

        Guid revision = await CreatedIdAsync(await SendAsync(
            HttpMethod.Post, Extracts(project.Id), _prepEngineer, new { periodStart = "2026-09-01", periodEnd = "2026-09-30", revisesExtractId = original }));

        ShouldRead(await QuantityLineAsync(Quantities(project.Id, revision), _finance), 300m, 150m, 450m);
        await ShouldFootAsync(project.Id, revision);
    }

    [Fact]
    public async Task An_extract_prepared_before_quantities_were_kept_reads_an_empty_list()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);
        await using (KaffDbContext owner = _database.CreateBareAdminContext())
        {
            await owner.Database.ExecuteSqlAsync($"DELETE FROM extract_quantity_lines WHERE extract_id = {extract}", Ct);
        }

        (await GetJsonAsync(Quantities(project.Id, extract), _finance)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_application_role_cannot_change_or_remove_a_kept_quantity()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);
        await using KaffDbContext app = _database.CreateBareContext();

        Func<Task> update = () => app.Database.ExecuteSqlAsync(
            $"UPDATE extract_quantity_lines SET cumulative_quantity = 1 WHERE extract_id = {extract}", Ct);
        Func<Task> delete = () => app.Database.ExecuteSqlAsync($"DELETE FROM extract_quantity_lines WHERE extract_id = {extract}", Ct);
        (await update.Should().ThrowAsync<Npgsql.PostgresException>()).Which.SqlState.Should().Be("42501");
        (await delete.Should().ThrowAsync<Npgsql.PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task Quantities_need_extract_read_and_an_assignment()
    {
        Project project = await NewProjectAsync();
        Guid extract = await PrepareAsync(project);

        foreach (Guid refused in new[] { _hr, _engineerUnassigned, _financeUnassigned })
        {
            (await SendAsync(HttpMethod.Get, Quantities(project.Id, extract), refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        Project other = await NewProjectAsync();
        (await SendAsync(HttpMethod.Get, Quantities(other.Id, extract), _finance, null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the extract belongs to another project");
    }

    [Fact]
    public async Task The_preview_is_for_an_assigned_preparer_and_shows_what_prepare_will_copy()
    {
        Project project = await NewProjectAsync();
        string preview = QuantitiesPreview(project.Id);

        foreach (Guid refused in new[] { _owner, _finance, _technicalOffice, _hr, _engineerUnassigned })
        {
            (await SendAsync(HttpMethod.Get, preview, refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        JsonElement first = await QuantityLineAsync(preview, _prepEngineer);
        ShouldRead(first, 0m, 300m, 300m);
        first.GetProperty("rate").ValueKind.Should().Be(JsonValueKind.Null, "the junior engineer does not see money");

        Guid extract = await PrepareAsync(project);
        HttpResponseMessage refusedWhileOpen = await SendAsync(HttpMethod.Get, preview, _prepEngineer, null);
        (await ErrorCodeAsync(refusedWhileOpen)).Should().Be("extract.another_extract_open", "the preview gives the route's own refusal");

        await StepAsync(_client, project, extract, ExtractStatus.Draft, ExtractStatus.Issued);
        await ExecuteAsync(project, 400m);
        ShouldRead(await QuantityLineAsync(preview, _prepEngineer), 300m, 100m, 400m);
        Guid second = await PrepareAsync(project);
        ShouldRead(await QuantityLineAsync(Quantities(project.Id, second), _prepEngineer), 300m, 100m, 400m);
    }
}
