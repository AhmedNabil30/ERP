using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Tests.Features.Boqs;

/// <summary>
/// The BOQ and contract signature (spec.md §4, project journey study step 2): the contract value is
/// derived from the BOQ, signature freezes it in one transaction, and the freeze is real.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BoqAndSignatureTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _technicalOfficeUnassigned;
    private Guid _finance;
    private Guid _siteEngineer;
    private Guid _babId;
    private Guid _itemId;

    public BoqAndSignatureTests(PostgresDatabase database) => _database = database;

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

    // ---- sign refuses without a BOQ -----------------------------------------------------------------

    [Fact]
    public async Task Signing_a_lump_sum_project_with_no_boq_is_refused_and_signs_nothing()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.boq.required");

        Project stored = await LoadProjectAsync(project);
        stored.IsSigned.Should().BeFalse();
        stored.ContractValue.Should().BeNull();
    }

    // ---- sign: value from the BOQ, freeze, date — one transaction -----------------------------------------

    [Fact]
    public async Task Signing_takes_the_boq_total_as_the_contract_value_freezes_the_boq_and_stamps_the_date()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);

        // Item sells at 1,500 with the CON باب markup of 15%: 1,725.00 a unit. 4 units = 6,900.00.
        (await AddCatalogueLineAsync(project, estimated: 5m, surveyed: 4m)).StatusCode.Should().Be(HttpStatusCode.Created);
        // Custom: 80 x (1 + 15%) = 92.00 a unit. 10 units = 920.00.
        (await AddCustomLineAsync(project, surveyed: 10m)).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { signedOn = "2026-09-30", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("contractValue").ValueKind.Should().Be(JsonValueKind.String, "D-135");
        body.RootElement.GetProperty("contractValue").GetString().Should().Be("7820.00");

        Project stored = await LoadProjectAsync(project);
        stored.ContractValue.Should().Be(new Money(7_820m));
        stored.SignedOn.Should().Be(new DateOnly(2026, 9, 30));

        Boq boq = await LoadBoqAsync(project);
        boq.FrozenOn.Should().Be(new DateOnly(2026, 9, 30), "the BOQ freezes on the signature date, in the same save");
    }

    [Fact]
    public async Task A_signed_project_cannot_be_signed_again()
    {
        Guid project = await NewSignedProjectAsync();

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.project.already_signed");
    }

    // ---- the slice gate: prices provably frozen --------------------------------------------------------------

    [Fact]
    public async Task Editing_the_catalogue_after_signing_leaves_the_signed_boq_unchanged()
    {
        Guid project = await NewSignedProjectAsync();
        BoqSnapshot before = await SnapshotAsync(project);

        await using (KaffDbContext editor = _database.CreateContext())
        {
            CatalogueItem item = await editor.CatalogueItems.SingleAsync(candidate => candidate.Id == _itemId, Ct);
            item.Reprice(new Money(9_999m), new Money(8_888m)).IsSuccess.Should().BeTrue();
            item.SetDescription("وصف معدل بعد التوقيع", "Edited after signature");
            item.SetUnit("م٢");

            Bab bab = await editor.Babs.SingleAsync(candidate => candidate.Id == _babId, Ct);
            bab.SetDefaultMarkup(Percentage.FromPercent(80m));
            bab.Rename("اسم جديد", "New name").IsSuccess.Should().BeTrue();

            await editor.SaveChangesAsync(Ct);
        }

        BoqSnapshot after = await SnapshotAsync(project);

        after.Should().Be(before, "spec.md section 4.4: a later catalogue change cannot reach a signed contract");
        after.Total.Should().Be(new Money(6_900m));
        (await LoadProjectAsync(project)).ContractValue.Should().Be(new Money(6_900m));
    }

    [Fact]
    public async Task The_boq_lines_carry_no_foreign_key_to_the_catalogue_or_to_the_babs()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        List<string> targets = await reader.Database.SqlQueryRaw<string>(
                """
                SELECT confrelid::regclass::text AS "Value"
                FROM pg_constraint
                WHERE conrelid = 'boq_lines'::regclass AND contype = 'f'
                """)
            .ToListAsync(Ct);

        targets.Should().NotContain(["catalogue_items", "babs"]);
        targets.Should().BeEquivalentTo(["boqs", "projects"]);
    }

    // ---- the freeze is real: refused by the API ... ---------------------------------------------------------

    [Fact]
    public async Task A_frozen_boq_refuses_add_update_and_remove_through_the_api()
    {
        Guid project = await NewSignedProjectAsync();
        BoqSnapshot snapshot = await SnapshotAsync(project);
        Guid lineId = snapshot.LineIds[0];

        HttpResponseMessage add = await AddCatalogueLineAsync(project, 1m, 1m);
        HttpResponseMessage update = await SendAsync(
            HttpMethod.Put,
            $"/api/projects/{project}/boq/lines/{lineId}",
            _owner,
            new { quantityEstimated = 1, quantitySurveyed = 1, conditionsRate = "0", lineMarkup = "0" });
        HttpResponseMessage remove = await SendAsync(HttpMethod.Delete, $"/api/projects/{project}/boq/lines/{lineId}", _owner, null);

        foreach (HttpResponseMessage response in new[] { add, update, remove })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await MessageKeyAsync(response)).Should().Be("errors.boq.frozen");
        }

        (await SnapshotAsync(project)).Should().Be(snapshot);
    }

    // ---- ... and by the database, for every other path ---------------------------------------------------------

    [Fact]
    public async Task The_database_refuses_to_change_insert_or_delete_a_frozen_line_whatever_the_path()
    {
        Guid project = await NewSignedProjectAsync();
        Boq boq = await LoadBoqAsync(project);
        Guid lineId = boq.Lines[0].Id;

        await ExpectFrozenRefusalAsync($"UPDATE boq_lines SET sell_rate = 1 WHERE id = {lineId}");
        await ExpectFrozenRefusalAsync($"UPDATE boq_lines SET quantity_surveyed = 999 WHERE id = {lineId}");
        await ExpectFrozenRefusalAsync($"UPDATE boq_lines SET description_ar = 'x' WHERE id = {lineId}");
        await ExpectFrozenRefusalAsync($"DELETE FROM boq_lines WHERE id = {lineId}");
        await ExpectFrozenRefusalAsync($"TRUNCATE boq_lines");
        await ExpectFrozenRefusalAsync($"UPDATE boqs SET frozen_on = NULL WHERE id = {boq.Id}");
        await ExpectFrozenRefusalAsync($"UPDATE boqs SET frozen_on = '2020-01-01' WHERE id = {boq.Id}");
        await ExpectFrozenRefusalAsync($"DELETE FROM boqs WHERE id = {boq.Id}");

        Guid newLineId = Guid.CreateVersion7();
        await ExpectFrozenRefusalAsync(
            $"""
            INSERT INTO boq_lines (
                id, boq_id, project_id, position, description_ar, unit, bab_code, bab_name_ar, bab_name_en,
                cost_price, base_sell_rate, conditions_rate, line_markup, sell_rate,
                quantity_executed, is_custom, pending_catalogue_review)
            VALUES (
                {newLineId}, {boq.Id}, {project}, 99, 'x', 'u', 'b', 'b', 'b',
                1, 1, 0, 0, 1, 0, false, false)
            """);

        (await LoadBoqAsync(project)).Lines.Should().HaveCount(boq.Lines.Count);
    }

    [Fact]
    public async Task The_executed_quantity_is_the_one_column_that_still_moves_on_a_frozen_line()
    {
        Guid project = await NewSignedProjectAsync();
        Boq boq = await LoadBoqAsync(project);
        Guid lineId = boq.Lines[0].Id;

        await using KaffDbContext writer = _database.CreateBareContext();
        await writer.Database.ExecuteSqlAsync(
            $"UPDATE boq_lines SET quantity_executed = quantity_executed + 1.5 WHERE id = {lineId}", Ct);

        (await LoadBoqAsync(project)).Lines.Single(line => line.Id == lineId).QuantityExecuted.Should().Be(1.5m);
    }

    [Fact]
    public async Task A_draft_boq_is_still_editable_at_the_database()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        (await AddCatalogueLineAsync(project, 1m, 1m)).StatusCode.Should().Be(HttpStatusCode.Created);
        Boq boq = await LoadBoqAsync(project);

        await using KaffDbContext writer = _database.CreateBareContext();
        Guid lineId = boq.Lines[0].Id;
        int changed = await writer.Database.ExecuteSqlAsync(
            $"UPDATE boq_lines SET quantity_surveyed = 7 WHERE id = {lineId}", Ct);

        changed.Should().Be(1);
    }

    // ---- building the BOQ -----------------------------------------------------------------------------------

    [Fact]
    public async Task A_catalogue_line_copies_the_item_and_defaults_its_markup_from_the_bab()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);

        HttpResponseMessage response = await AddCatalogueLineAsync(project, 5m, 4m);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement line = body.RootElement;

        line.GetProperty("itemCode").GetString().Should().Be(_itemCode);
        line.GetProperty("baseSellRate").GetString().Should().Be("1500.0000");
        decimal.Parse(line.GetProperty("lineMarkup").GetString()!, CultureInfo.InvariantCulture).Should().Be(0.15m);
        line.GetProperty("sellRate").GetString().Should().Be("1725.0000");
        line.GetProperty("lineTotal").GetString().Should().Be("6900.00");
        decimal.Parse(line.GetProperty("quantityEstimated").GetString()!, CultureInfo.InvariantCulture).Should().Be(5m);
        decimal.Parse(line.GetProperty("quantitySurveyed").GetString()!, CultureInfo.InvariantCulture).Should().Be(4m);
        decimal.Parse(line.GetProperty("quantityExecuted").GetString()!, CultureInfo.InvariantCulture).Should().Be(0m);
        line.GetProperty("pendingCatalogueReview").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_custom_line_is_flagged_for_review_and_the_catalogue_is_not_written()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);

        int catalogueBefore = await CatalogueCountAsync();

        HttpResponseMessage response = await AddCustomLineAsync(project, surveyed: 3m);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("isCustom").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("pendingCatalogueReview").GetBoolean().Should().BeTrue();

        (await CatalogueCountAsync()).Should().Be(catalogueBefore, "spec.md section 4.5: a custom item never writes the catalogue");
    }

    [Fact]
    public async Task A_line_must_name_a_catalogue_item_or_be_a_complete_custom_item_not_both()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);

        HttpResponseMessage neither = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/lines", _owner, new { quantitySurveyed = 1 });
        HttpResponseMessage both = await SendAsync(
            HttpMethod.Post,
            $"/api/projects/{project}/boq/lines",
            _owner,
            new { catalogueItemId = _itemId, descriptionAr = "بند", unit = "عدد" });

        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(both)).Should().Be("errors.boq.line_shape_invalid");
    }

    [Fact]
    public async Task A_design_project_has_no_boq()
    {
        Guid project = await NewProjectAsync(ContractType.Design);

        HttpResponseMessage response = await AddCatalogueLineAsync(project, 1m, 1m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.boq.not_for_this_contract_type");
    }

    [Fact]
    public async Task Removing_a_draft_line_renumbers_the_rest()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        await AddCatalogueLineAsync(project, 1m, 1m);
        await AddCatalogueLineAsync(project, 2m, 2m);
        await AddCatalogueLineAsync(project, 3m, 3m);

        Boq boq = await LoadBoqAsync(project);
        Guid second = boq.Lines.Single(line => line.Position == 2).Id;

        (await SendAsync(HttpMethod.Delete, $"/api/projects/{project}/boq/lines/{second}", _owner, null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LoadBoqAsync(project)).Lines.Select(line => line.Position).Order().Should().Equal(1, 2);
    }

    [Fact]
    public async Task Get_boq_shows_a_total_only_once_every_line_has_a_quantity()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        await AddCatalogueLineAsync(project, null, null);

        using (JsonDocument partial = JsonDocument.Parse(
                   await (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/boq", _owner, null)).Content.ReadAsStringAsync(Ct)))
        {
            partial.RootElement.GetProperty("total").ValueKind.Should().Be(JsonValueKind.Null);
        }

        Boq boq = await LoadBoqAsync(project);
        (await SendAsync(
            HttpMethod.Put,
            $"/api/projects/{project}/boq/lines/{boq.Lines[0].Id}",
            _owner,
            new { quantityEstimated = 5, quantitySurveyed = 2, conditionsRate = "0.1", lineMarkup = "0.15" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument full = JsonDocument.Parse(
            await (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/boq", _owner, null)).Content.ReadAsStringAsync(Ct));

        // 1,500 x 1.10 x 1.15 = 1,897.50; x 2 = 3,795.00
        full.RootElement.GetProperty("total").GetString().Should().Be("3795.00");
    }

    // ---- other contract types ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_cost_plus_project_signs_without_a_boq_and_has_no_contract_value()
    {
        Guid project = await NewProjectAsync(ContractType.CostPlus);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Project stored = await LoadProjectAsync(project);
        stored.IsSigned.Should().BeTrue();
        stored.ContractValue.Should().BeNull();
    }

    [Fact]
    public async Task A_design_project_signs_on_area_times_rate()
    {
        Guid project = await NewProjectAsync(ContractType.Design, designArea: 200m);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadProjectAsync(project)).ContractValue.Should().Be(new Money(90_000m));
    }

    // ---- permissions: role x assignment, hitting the endpoints directly ----------------------------------------------------

    [Fact]
    public async Task Only_a_staffed_owner_or_technical_office_may_build_read_or_sign()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);

        foreach (Guid refused in new[] { _finance, _siteEngineer, _technicalOfficeUnassigned })
        {
            (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/boq", refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/lines", refused, new { catalogueItemId = _itemId }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Put, $"/api/projects/{project}/boq/lines/{Guid.NewGuid()}", refused, new { conditionsRate = "0", lineMarkup = "0" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Delete, $"/api/projects/{project}/boq/lines/{Guid.NewGuid()}", refused, null))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", refused, new { holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await LoadProjectAsync(project)).IsSigned.Should().BeFalse();
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/lines", _technicalOffice, LineBody(1m, 1m)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- KAFF-417: terms move to signature ------------------------------------------------------------------------------

    [Fact]
    public async Task Signing_a_lump_sum_project_with_hold_and_advance_applies_them_and_audits_before_and_after()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        (await AddCatalogueLineAsync(project, 1m, 1m)).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { holdPercentage = "0.10", clientAdvance = "0.30", advanceRecoveryRate = "0.25" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Project stored = await LoadProjectAsync(project);
        stored.IsSigned.Should().BeTrue();
        stored.HoldRate.Should().Be(Percentage.FromPercent(10m));
        stored.AdvanceRate.Should().Be(Percentage.FromPercent(30m));
        stored.MaterialAdvanceRate.Should().Be(Percentage.FromPercent(75m), "the other terms are left alone");

        await using KaffDbContext reader = _database.CreateBareContext();
        AuditRecord record = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Project) && r.EntityId == project && r.Action == AuditAction.Modified, Ct);
        record.BeforeJson.Should().NotBeNullOrEmpty();
        record.AfterJson.Should().NotBeNullOrEmpty();
        record.ChangedProperties.Should().Contain(["HoldRate", "AdvanceRate"]);
    }

    [Fact]
    public async Task D_227_a_lump_sum_signature_without_the_advance_recovery_rate_is_refused_and_no_default_is_applied()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        (await AddCatalogueLineAsync(project, 1m, 1m)).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = "0.25" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.advance_recovery_rate_required");
        (await LoadProjectAsync(project)).IsSigned.Should().BeFalse();

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/sign", _owner,
                new { holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "-0.01" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/sign", _owner,
                new { holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.30" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadProjectAsync(project)).AdvanceRecoveryRate.Should().Be(Percentage.FromPercent(30m), "the typed rate is stored, not the default");
    }

    [Theory]
    [InlineData(ContractType.CostPlus)]
    [InlineData(ContractType.Design)]
    public async Task Signing_a_cost_plus_or_design_project_with_hold_or_advance_is_refused_and_signs_nothing(ContractType type)
    {
        Guid project = await NewProjectAsync(type, designArea: 200m);

        foreach (object body in new object[] { new { holdPercentage = "0.20" }, new { clientAdvance = "0.25", advanceRecoveryRate = "0.25" }, new { advanceRecoveryRate = "0.25" } })
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, body);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.project.terms_do_not_match_contract_type");
        }

        (await LoadProjectAsync(project)).IsSigned.Should().BeFalse();
    }

    [Fact]
    public async Task Signing_with_terms_is_refused_for_the_right_role_without_an_assignment_and_changes_nothing()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        (await AddCatalogueLineAsync(project, 1m, 1m)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOfficeUnassigned, new { holdPercentage = "0.01", clientAdvance = "0.01", advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Project stored = await LoadProjectAsync(project);
        stored.IsSigned.Should().BeFalse();
        stored.HoldRate.Should().Be(Percentage.FromPercent(20m));
    }

    // ---- audit -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Signing_audits_the_project_and_the_boq_header_and_writes_no_row_per_line()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        for (int index = 0; index < 5; index++)
        {
            (await AddCatalogueLineAsync(project, 1m, 1m)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        Boq boq = await LoadBoqAsync(project);

        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            (await reader.AuditRecords.CountAsync(
                r => r.EntityType == nameof(BoqLine) && r.ProjectId == project && r.Action == AuditAction.Created, Ct))
                .Should().Be(5, "each line added while drafting is a state change and is audited");
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext after = _database.CreateBareContext();

        (await after.AuditRecords.CountAsync(
            r => r.EntityType == nameof(BoqLine) && r.ProjectId == project && r.Action != AuditAction.Created, Ct))
            .Should().Be(0, "freezing is a flag on the header: 400 lines must not write 400 audit rows");

        AuditRecord boqRecord = await after.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Boq) && r.EntityId == boq.Id && r.Action == AuditAction.Modified, Ct);
        boqRecord.ActorUserId.Should().Be(_owner);

        AuditRecord projectRecord = await after.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Project) && r.EntityId == project && r.Action == AuditAction.Modified, Ct);
        projectRecord.CorrelationId.Should().Be(boqRecord.CorrelationId, "one transaction, one correlation id");
    }

    // ---- helpers -----------------------------------------------------------------------------------------------------

    private const string CatalogueCode = "BOQ-CON-1";

    private string _itemCode = string.Empty;

    private sealed record BoqSnapshot(
        Money? Total, Guid[] LineIds, string[] Descriptions, string[] Units, Money[] CostPrices, Money[] SellRates, string[] BabNames, DateOnly? FrozenOn)
    {
        public bool Equals(BoqSnapshot? other) =>
            other is not null
            && Total == other.Total
            && FrozenOn == other.FrozenOn
            && LineIds.SequenceEqual(other.LineIds)
            && Descriptions.SequenceEqual(other.Descriptions)
            && Units.SequenceEqual(other.Units)
            && CostPrices.SequenceEqual(other.CostPrices)
            && SellRates.SequenceEqual(other.SellRates)
            && BabNames.SequenceEqual(other.BabNames);

        public override int GetHashCode() => HashCode.Combine(Total, FrozenOn, LineIds.Length);
    }

    private async Task<BoqSnapshot> SnapshotAsync(Guid project)
    {
        Boq boq = await LoadBoqAsync(project);
        BoqLine[] lines = [.. boq.Lines.OrderBy(line => line.Position)];

        return new BoqSnapshot(
            boq.Total(Kaff.Domain.Rulings.ContractValueSource.Default).Value,
            [.. lines.Select(line => line.Id)],
            [.. lines.Select(line => line.DescriptionAr)],
            [.. lines.Select(line => line.Unit)],
            [.. lines.Select(line => line.CostPrice)],
            [.. lines.Select(line => line.SellRate)],
            [.. lines.Select(line => line.BabNameAr)],
            boq.FrozenOn);
    }

    private async Task ExpectFrozenRefusalAsync(FormattableString sql)
    {
        await using KaffDbContext writer = _database.CreateBareAdminContext();

        Exception? thrown = null;

        try
        {
            await writer.Database.ExecuteSqlAsync(sql, Ct);
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        thrown.Should().NotBeNull($"the database must refuse: {sql.Format}");

        for (Exception? current = thrown; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                postgres.MessageText.Should().Contain("KAFF_BOQ_FROZEN", $"for: {sql.Format}");
                return;
            }
        }

        Assert.Fail($"No PostgresException in the chain for: {sql.Format} ({thrown})");
    }

    private async Task<Guid> NewSignedProjectAsync()
    {
        Guid project = await NewProjectAsync(ContractType.LumpSum);
        (await AddCatalogueLineAsync(project, 5m, 4m)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { signedOn = "2026-09-30", holdPercentage = "0.20", clientAdvance = "0.25", advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return project;
    }

    private async Task<Guid> NewProjectAsync(ContractType type, decimal? designArea = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        Project project = Project.Create(UniqueNames.Code("BQ-P"), UniqueNames.Code("مشروع المقايسة"), _clientId, type, DateTimeOffset.UtcNow).Value;

        if (type == ContractType.LumpSum)
        {
            project.SetLumpSumTerms(
                Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
                Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        }

        if (type == ContractType.Design && designArea is { } area)
        {
            project.SetDesignTerms(area, new Money(450m)).IsSuccess.Should().BeTrue();
        }

        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        User technicalOfficeUser = await context.Users.SingleAsync(user => user.Id == _technicalOffice, Ct);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(project.Id, technicalOfficeUser, AssignmentLevel.Standard, _owner, Now).Value);
        await context.SaveChangesAsync(Ct);

        return project.Id;
    }

    private Task<HttpResponseMessage> AddCatalogueLineAsync(Guid project, decimal? estimated, decimal? surveyed) =>
        SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/lines", _owner, LineBody(estimated, surveyed));

    private object LineBody(decimal? estimated, decimal? surveyed) => new
    {
        catalogueItemId = _itemId,
        quantityEstimated = estimated,
        quantitySurveyed = surveyed,
    };

    private Task<HttpResponseMessage> AddCustomLineAsync(Guid project, decimal surveyed) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/projects/{project}/boq/lines",
            _owner,
            new
            {
                descriptionAr = "بند خاص",
                descriptionEn = "Custom item",
                unit = "عدد",
                babId = _babId,
                costPrice = "50.0000",
                baseSellRate = "80.0000",
                quantitySurveyed = surveyed,
            });

    private async Task<int> CatalogueCountAsync()
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.CatalogueItems.CountAsync(Ct);
    }

    private async Task<Project> LoadProjectAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.SingleAsync(project => project.Id == id, Ct);
    }

    private async Task<Boq> LoadBoqAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Boqs.Include(boq => boq.Lines).SingleAsync(boq => boq.ProjectId == project, Ct);
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

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("BQ-C"), "عميل المقايسة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("bq-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "bq-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User technicalOfficeUnassigned = MakeUser(
            "bq-tech-un", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("bq-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User siteEngineer = MakeUser(
            "bq-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

        Bab bab = Bab.Create(UniqueNames.Code("BQB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code(CatalogueCode), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;

        context.Clients.Add(client);
        context.Users.AddRange(owner, technicalOffice, technicalOfficeUnassigned, finance, siteEngineer);
        context.Babs.Add(bab);
        context.CatalogueItems.Add(item);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _technicalOfficeUnassigned = technicalOfficeUnassigned.Id;
        _finance = finance.Id;
        _siteEngineer = siteEngineer.Id;
        _babId = bab.Id;
        _itemId = item.Id;
        _itemCode = item.Code;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
