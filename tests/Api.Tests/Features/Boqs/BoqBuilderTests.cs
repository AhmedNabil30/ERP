using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Boqs;

/// <summary>
/// KAFF-418, the BOQ builder (D-217 items 19 to 33, D-218 items 10 to 13): prepared bases, the copy route, adding many
/// lines, the one-quantity rule, signing refusals, and the archived BOQ being read-only.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BoqBuilderTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private readonly TestClock _clock = new();
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _otherTechnicalOffice;
    private Guid _finance;
    private Guid _siteEngineer;
    private Guid _babId;
    private Guid _itemA;
    private Guid _itemB;
    private string _codeA = null!;

    public BoqBuilderTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database.ConnectionString, clock: _clock);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- AC-1, AC-2: prepared bases -------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_418_A_the_technical_office_prepares_renames_edits_and_archives_a_base_and_it_holds_no_rate()
    {
        HttpResponseMessage created = await SendAsync(
            HttpMethod.Post, "/api/boq-bases", _technicalOffice,
            new { name = "شقة 120 متر", lines = new[] { new { catalogueItemId = _itemA, quantity = 10m }, new { catalogueItemId = _itemB, quantity = 4.5m } } });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await JsonAsync(created);
        Guid baseId = body.GetProperty("id").GetGuid();
        body.GetProperty("lines").GetArrayLength().Should().Be(2);

        string raw = body.GetRawText();
        raw.Should().NotContain("Rate", "a base holds no rate").And.NotContain("rate").And.NotContain("hold").And.NotContain("advance");

        HttpResponseMessage updated = await SendAsync(
            HttpMethod.Put, $"/api/boq-bases/{baseId}", _technicalOffice,
            new { name = "شقة 120 متر - متوسط", lines = new[] { new { catalogueItemId = _itemB, quantity = 7m } } });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement afterUpdate = await JsonAsync(updated);
        afterUpdate.GetProperty("name").GetString().Should().Be("شقة 120 متر - متوسط");
        afterUpdate.GetProperty("lines").GetArrayLength().Should().Be(1);
        Dec(afterUpdate.GetProperty("lines")[0].GetProperty("quantity")).Should().Be(7m);

        (await SendAsync(HttpMethod.Post, $"/api/boq-bases/{baseId}/archive", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListBaseIdsAsync(_technicalOffice)).Should().NotContain(baseId);
        (await SendAsync(HttpMethod.Get, $"/api/boq-bases/{baseId}", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage editArchived = await SendAsync(
            HttpMethod.Put, $"/api/boq-bases/{baseId}", _technicalOffice, new { name = "x", lines = Array.Empty<object>() });
        editArchived.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(editArchived)).Should().Be("errors.boq.base_archived");
    }

    [Fact]
    public async Task AC_418_A_a_base_needs_a_name_and_an_active_catalogue_item_and_a_quantity_that_is_not_negative()
    {
        HttpResponseMessage noName = await SendAsync(HttpMethod.Post, "/api/boq-bases", _technicalOffice, new { name = " ", lines = Array.Empty<object>() });
        noName.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(noName)).Should().Be("errors.boq.base_name_required");

        (await SendAsync(HttpMethod.Post, "/api/boq-bases", _technicalOffice, new { name = "a", lines = new[] { new { catalogueItemId = Guid.NewGuid(), quantity = 1m } } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage negative = await SendAsync(
            HttpMethod.Post, "/api/boq-bases", _technicalOffice, new { name = "a", lines = new[] { new { catalogueItemId = _itemA, quantity = -1m } } });
        negative.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(negative)).Should().Be("errors.boq.quantity_must_not_be_negative");
    }

    [Fact]
    public async Task AC_418_B_everyone_who_may_build_a_boq_lists_and_reads_bases_and_nobody_else_does()
    {
        Guid baseId = await NewBaseAsync();

        foreach (Guid reader in new[] { _owner, _technicalOffice, _otherTechnicalOffice })
        {
            (await ListBaseIdsAsync(reader)).Should().Contain(baseId);
            (await SendAsync(HttpMethod.Get, $"/api/boq-bases/{baseId}", reader, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        foreach (Guid refused in new[] { _finance, _siteEngineer })
        {
            (await SendAsync(HttpMethod.Get, "/api/boq-bases", refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Get, $"/api/boq-bases/{baseId}", refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, "/api/boq-bases", refused, new { name = "x", lines = Array.Empty<object>() }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Put, $"/api/boq-bases/{baseId}", refused, new { name = "x", lines = Array.Empty<object>() }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/boq-bases/{baseId}/archive", refused, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await ListBaseIdsAsync(_technicalOffice)).Should().Contain(baseId, "the refused writes changed nothing");
    }

    // ---- AC-3: the copy route ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_418_C_a_base_fills_an_empty_boq_with_todays_rates_and_leaves_the_hold_and_advance_alone()
    {
        Guid baseId = await NewBaseAsync();
        await RepriceAsync(_itemA, cost: 1_100m, sell: 1_600m);
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        Project before = await LoadProjectAsync(project);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/copy", _technicalOffice, new { baseId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await JsonAsync(response);
        body.GetProperty("skipped").GetArrayLength().Should().Be(0);
        JsonElement[] lines = [.. body.GetProperty("lines").EnumerateArray()];
        lines.Should().HaveCount(2);
        lines[0].GetProperty("itemCode").GetString().Should().Be(_codeA);
        Dec(lines[0].GetProperty("quantityEstimated")).Should().Be(10m);
        lines[0].GetProperty("quantitySurveyed").ValueKind.Should().Be(JsonValueKind.Null);
        lines[0].GetProperty("baseSellRate").GetString().Should().Be("1600.0000", "the rate is the catalogue's of today");
        lines[0].GetProperty("isCustom").GetBoolean().Should().BeFalse();

        Project after = await LoadProjectAsync(project);
        after.HoldRate.Should().Be(before.HoldRate);
        after.AdvanceRate.Should().Be(before.AdvanceRate);

        // AC-16: a copied line is a frozen copy, with no reach back to the catalogue.
        await RepriceAsync(_itemA, cost: 9_000m, sell: 9_999m);
        Boq stored = await LoadBoqAsync(project);
        stored.Lines.Single(line => line.ItemCode == _codeA).BaseSellRate.Should().Be(new Money(1_600m));
    }

    [Fact]
    public async Task AC_418_C_a_copy_into_a_boq_that_already_has_lines_is_refused_and_changes_nothing()
    {
        Guid baseId = await NewBaseAsync();
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/lines", _technicalOffice, new { catalogueItemId = _itemA, quantityEstimated = 1m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/copy", _technicalOffice, new { baseId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.boq.not_empty");
        (await LoadBoqAsync(project)).Lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task AC_418_C_a_copy_names_exactly_one_source()
    {
        Guid baseId = await NewBaseAsync();
        Guid project = await NewProjectAsync(assigned: _technicalOffice);

        foreach (object body in new object[] { new { }, new { baseId, sourceProjectId = Guid.NewGuid() }, new { sourceProjectId = project } })
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/copy", _technicalOffice, body);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.boq.copy_source_invalid");
        }
    }

    [Fact]
    public async Task AC_418_C_an_item_the_catalogue_has_archived_is_skipped_and_named_not_invented()
    {
        Guid baseId = await NewBaseAsync();
        await ArchiveItemAsync(_itemB);
        Guid project = await NewProjectAsync(assigned: _technicalOffice);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/copy", _technicalOffice, new { baseId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await JsonAsync(response);
        body.GetProperty("lines").GetArrayLength().Should().Be(1);
        body.GetProperty("skipped").GetArrayLength().Should().Be(1);
        body.GetProperty("skipped")[0].GetProperty("reason").GetString().Should().Be("catalogue_item_archived");
        (await LoadBoqAsync(project)).Lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task AC_418_C_an_earlier_project_fills_an_empty_boq_with_its_one_quantity_and_its_custom_lines_as_custom()
    {
        Guid source = await NewProjectAsync(assigned: _technicalOffice);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{source}/boq/lines", _technicalOffice, new { catalogueItemId = _itemA, quantityEstimated = 5m, quantitySurveyed = 4m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{source}/boq/lines", _technicalOffice, new { catalogueItemId = _itemB, quantityEstimated = 6m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(
                HttpMethod.Post, $"/api/projects/{source}/boq/lines", _technicalOffice,
                new { descriptionAr = "بند خاص", unit = "عدد", babId = _babId, costPrice = "50.0000", baseSellRate = "80.0000", quantityEstimated = 3m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{source}/sign", _technicalOffice, new { holdPercentage = "0.10", clientAdvance = 30m, advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await RepriceAsync(_itemA, cost: 1_100m, sell: 1_700m);

        Guid target = await NewProjectAsync(assigned: _technicalOffice);
        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{target}/boq/copy", _technicalOffice, new { sourceProjectId = source });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement[] lines = [.. (await JsonAsync(response)).GetProperty("lines").EnumerateArray()];
        lines.Should().HaveCount(3);
        Dec(lines[0].GetProperty("quantityEstimated")).Should().Be(4m, "the surveyed value replaced the estimate in the source");
        lines[0].GetProperty("baseSellRate").GetString().Should().Be("1700.0000");
        Dec(lines[1].GetProperty("quantityEstimated")).Should().Be(6m);
        lines[2].GetProperty("isCustom").GetBoolean().Should().BeTrue();
        lines[2].GetProperty("baseSellRate").GetString().Should().Be("80.0000", "a custom line keeps the values it stored");
        Dec(lines[2].GetProperty("quantityEstimated")).Should().Be(3m);

        Project copiedTo = await LoadProjectAsync(target);
        copiedTo.HoldRate.Should().Be(Percentage.FromPercent(20m), "the source's 10% hold did not come across");
        copiedTo.AdvanceRate.Should().Be(Percentage.FromPercent(25m));
    }

    [Fact]
    public async Task AC_418_C_a_source_project_the_caller_is_not_assigned_to_is_refused()
    {
        Guid source = await NewProjectAsync(assigned: _technicalOffice);
        (await SendAsync(HttpMethod.Post, $"/api/projects/{source}/boq/lines", _technicalOffice, new { catalogueItemId = _itemA, quantityEstimated = 5m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        Guid target = await NewProjectAsync(assigned: _otherTechnicalOffice);

        // Right role, assigned to the target, not assigned to the source.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{target}/boq/copy", _otherTechnicalOffice, new { sourceProjectId = source }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LoadBoqOrNullAsync(target)).Should().BeNull();

        // Wrong role, and right role without the target assignment.
        foreach (Guid refused in new[] { _finance, _siteEngineer })
        {
            (await SendAsync(HttpMethod.Post, $"/api/projects/{target}/boq/copy", refused, new { sourceProjectId = source }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(HttpMethod.Post, $"/api/projects/{target}/boq/copy", _technicalOffice, new { sourceProjectId = source }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- AC-4: several items in one request -------------------------------------------------------------------------------

    [Fact]
    public async Task AC_418_D_several_catalogue_items_are_added_in_one_request()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines/batch", _technicalOffice,
            new { items = new[] { new { catalogueItemId = _itemA, quantityEstimated = 2m, quantitySurveyed = (decimal?)null }, new { catalogueItemId = _itemB, quantityEstimated = 3m, quantitySurveyed = (decimal?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement[] lines = [.. (await JsonAsync(response)).GetProperty("lines").EnumerateArray()];
        lines.Select(line => line.GetProperty("position").GetInt32()).Should().Equal(1, 2);
        (await LoadBoqAsync(project)).Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task AC_418_D_one_bad_item_in_a_batch_stores_nothing()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines/batch", _technicalOffice,
            new { items = new[] { new { catalogueItemId = _itemA, quantityEstimated = 2m }, new { catalogueItemId = Guid.NewGuid(), quantityEstimated = 3m } } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LoadBoqOrNullAsync(project)).Should().BeNull("not even the BOQ header was stored");

        HttpResponseMessage empty = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines/batch", _technicalOffice, new { items = Array.Empty<object>() });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(empty)).Should().Be("errors.boq.batch_empty");
    }

    [Fact]
    public async Task AC_418_D_the_batch_route_and_the_copy_route_refuse_the_wrong_role_and_the_unassigned()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        Guid baseId = await NewBaseAsync();

        foreach (Guid refused in new[] { _finance, _siteEngineer, _otherTechnicalOffice })
        {
            (await SendAsync(
                    HttpMethod.Post, $"/api/projects/{project}/boq/lines/batch", refused,
                    new { items = new[] { new { catalogueItemId = _itemA, quantityEstimated = 1m } } }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/copy", refused, new { baseId }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await LoadBoqOrNullAsync(project)).Should().BeNull();
    }

    // ---- AC-5, AC-6, AC-8: one quantity, signing ----------------------------------------------------------------------------

    [Fact]
    public async Task AC_418_E_a_line_counts_as_estimated_until_a_survey_replaces_it_and_signing_prices_that_quantity()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        (await AddLineAsync(project, _itemA, estimated: 4m, surveyed: null)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddLineAsync(project, _itemB, estimated: 10m, surveyed: 2m)).StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement boq = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/projects/{project}/boq", _technicalOffice, null));
        // Both items sell at 1,500 with the 15% markup: 1,725 a unit. 4 estimated + 2 surveyed = 6 units = 10,350.
        boq.GetProperty("total").GetString().Should().Be("10350.00");

        HttpResponseMessage signed = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, Terms());
        signed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(signed)).GetProperty("contractValue").GetString().Should().Be("10350.00");
    }

    [Fact]
    public async Task AC_418_F_a_line_with_no_quantity_blocks_signing_and_the_refusal_names_the_line()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        (await AddLineAsync(project, _itemA, estimated: 4m, surveyed: null)).StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement second = await JsonAsync(await AddLineAsync(project, _itemB, estimated: null, surveyed: null));

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, Terms());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement problem = await JsonAsync(response);
        problem.GetProperty("messageKey").GetString().Should().Be("errors.boq.line_quantity_missing");
        problem.GetProperty("lineId").GetGuid().Should().Be(second.GetProperty("id").GetGuid());
        problem.GetProperty("position").GetInt32().Should().Be(2);
        problem.GetProperty("descriptionAr").GetString().Should().NotBeNullOrEmpty();
        (await LoadProjectAsync(project)).IsSigned.Should().BeFalse();
        (await LoadBoqAsync(project)).FrozenOn.Should().BeNull();
    }

    [Fact]
    public async Task AC_418_G_a_custom_line_is_an_ordinary_line_and_signing_goes_through_while_it_waits_for_review()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        JsonElement custom = await JsonAsync(await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines", _technicalOffice,
            new { descriptionAr = "بند خاص", unit = "عدد", babId = _babId, costPrice = "50.0000", baseSellRate = "80.0000", quantityEstimated = 3m }));
        custom.GetProperty("pendingCatalogueReview").GetBoolean().Should().BeTrue();
        Guid lineId = custom.GetProperty("id").GetGuid();

        // Changed like any line.
        (await SendAsync(
                HttpMethod.Put, $"/api/projects/{project}/boq/lines/{lineId}", _technicalOffice,
                new { quantityEstimated = 5m, quantitySurveyed = (decimal?)null, conditionsRate = "0", lineMarkup = "0.15" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // It writes nothing to the catalogue.
        await using (KaffDbContext reader = _database.CreateBareContext())
        {
            (await reader.CatalogueItems.AnyAsync(item => item.DescriptionAr == "بند خاص", Ct)).Should().BeFalse();
        }

        // Signed while pending review.
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, Terms())).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoadBoqAsync(project)).Lines.Single().PendingCatalogueReview.Should().BeTrue();

        // Removed like any line, on a project that is not signed.
        Guid other = await NewProjectAsync(assigned: _technicalOffice);
        Guid otherLine = (await JsonAsync(await SendAsync(
            HttpMethod.Post, $"/api/projects/{other}/boq/lines", _technicalOffice,
            new { descriptionAr = "بند خاص", unit = "عدد", babId = _babId, costPrice = "50.0000", baseSellRate = "80.0000", quantityEstimated = 3m })))
            .GetProperty("id").GetGuid();
        (await SendAsync(HttpMethod.Delete, $"/api/projects/{other}/boq/lines/{otherLine}", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- AC-9: hold and advance are typed at signature ------------------------------------------------------------------------

    [Fact]
    public async Task AC_418_H_a_lump_sum_signature_without_the_hold_or_the_advance_is_refused_and_no_default_is_applied()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        (await AddLineAsync(project, _itemA, estimated: 4m, surveyed: null)).StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (object body in new object[] { new { }, new { holdPercentage = "0.10" }, new { clientAdvance = 30m, advanceRecoveryRate = "0.25" } })
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, body);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.project.signing_terms_required");
        }

        (await LoadProjectAsync(project)).IsSigned.Should().BeFalse();

        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _technicalOffice, new { holdPercentage = "0.10", clientAdvance = 30m, advanceRecoveryRate = "0.25" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        Project stored = await LoadProjectAsync(project);
        stored.HoldRate.Should().Be(Percentage.FromPercent(10m));
        stored.AdvanceRate.Should().Be(Percentage.FromPercent(30m));
    }

    // ---- AC-10: an archived project's BOQ is read-only --------------------------------------------------------------------------

    [Fact]
    public async Task AC_418_I_every_boq_write_on_an_archived_project_is_refused_and_the_boq_can_still_be_read()
    {
        Guid project = await NewProjectAsync(assigned: _technicalOffice);
        JsonElement line = await JsonAsync(await AddLineAsync(project, _itemA, estimated: 4m, surveyed: null));
        Guid lineId = line.GetProperty("id").GetGuid();
        Guid baseId = await NewBaseAsync();
        Guid emptyArchived = await NewProjectAsync(assigned: _technicalOffice);

        _clock.Advance(TimeSpan.FromDays(31));

        (string Name, HttpResponseMessage Response)[] writes =
        [
            ("add", await AddLineAsync(project, _itemB, estimated: 1m, surveyed: null)),
            ("batch", await SendAsync(HttpMethod.Post, $"/api/projects/{project}/boq/lines/batch", _technicalOffice, new { items = new[] { new { catalogueItemId = _itemB, quantityEstimated = 1m } } })),
            ("update", await SendAsync(HttpMethod.Put, $"/api/projects/{project}/boq/lines/{lineId}", _technicalOffice, new { quantityEstimated = 9m, quantitySurveyed = (decimal?)null, conditionsRate = "0", lineMarkup = "0.15" })),
            ("remove", await SendAsync(HttpMethod.Delete, $"/api/projects/{project}/boq/lines/{lineId}", _technicalOffice, null)),
            ("copy", await SendAsync(HttpMethod.Post, $"/api/projects/{emptyArchived}/boq/copy", _technicalOffice, new { baseId })),
        ];

        foreach ((string name, HttpResponseMessage response) in writes)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, name);
            (await MessageKeyAsync(response)).Should().Be("errors.project.archived", name);
        }

        (await LoadBoqAsync(project)).Lines.Should().ContainSingle().Which.QuantityEstimated.Should().Be(4m);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{project}/boq", _technicalOffice, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------------------

    private static decimal Dec(JsonElement element) => decimal.Parse(element.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static object Terms() => new { holdPercentage = "0.20", clientAdvance = 25m, advanceRecoveryRate = "0.25" };

    private Task<HttpResponseMessage> AddLineAsync(Guid project, Guid item, decimal? estimated, decimal? surveyed) =>
        SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines", _technicalOffice,
            new { catalogueItemId = item, quantityEstimated = estimated, quantitySurveyed = surveyed });

    private async Task<Guid> NewBaseAsync()
    {
        HttpResponseMessage created = await SendAsync(
            HttpMethod.Post, "/api/boq-bases", _technicalOffice,
            new { name = "قاعدة " + Guid.NewGuid().ToString("N")[..6], lines = new[] { new { catalogueItemId = _itemA, quantity = 10m }, new { catalogueItemId = _itemB, quantity = 4.5m } } });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await JsonAsync(created)).GetProperty("id").GetGuid();
    }

    private async Task<Guid[]> ListBaseIdsAsync(Guid actor)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/boq-bases", actor, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await JsonAsync(response)).GetProperty("bases").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private async Task RepriceAsync(Guid itemId, decimal cost, decimal sell)
    {
        await using KaffDbContext context = _database.CreateContext();
        CatalogueItem item = await context.CatalogueItems.SingleAsync(candidate => candidate.Id == itemId, Ct);
        item.Reprice(new Money(cost), new Money(sell)).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);
    }

    private async Task ArchiveItemAsync(Guid itemId)
    {
        await using KaffDbContext context = _database.CreateContext();
        CatalogueItem item = await context.CatalogueItems.SingleAsync(candidate => candidate.Id == itemId, Ct);
        item.Archive().IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);
    }

    private async Task<Guid> NewProjectAsync(Guid assigned)
    {
        await using KaffDbContext context = _database.CreateContext();

        Project project = Project.Create(UniqueNames.Code("BB-P"), UniqueNames.Code("مشروع"), _clientId, ContractType.LumpSum, _clock.GetUtcNow()).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        User user = await context.Users.SingleAsync(candidate => candidate.Id == assigned, Ct);
        context.ProjectAssignments.Add(ProjectAssignment.Create(project.Id, user, AssignmentLevel.Standard, _owner, _clock.GetUtcNow()).Value);
        await context.SaveChangesAsync(Ct);

        return project.Id;
    }

    private async Task<Project> LoadProjectAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.SingleAsync(project => project.Id == id, Ct);
    }

    private async Task<Boq> LoadBoqAsync(Guid project) => (await LoadBoqOrNullAsync(project))!;

    private async Task<Boq?> LoadBoqOrNullAsync(Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Boqs.Include(boq => boq.Lines).SingleOrDefaultAsync(boq => boq.ProjectId == project, Ct);
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

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response) =>
        (await JsonAsync(response)).TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Client client = Client.Create(UniqueNames.Code("BB-C"), "عميل", UniqueNames.Phone(), ClientKind.Corporate, now).Value;
        User owner = User.Create(UniqueNames.Code("bb-owner"), "bb-owner", UniqueNames.Phone(), Role.Owner, now).Value;
        User technicalOffice = Technical("bb-tech", now);
        User otherTechnicalOffice = Technical("bb-tech-2", now);
        User finance = User.Create(UniqueNames.Code("bb-fin"), "bb-fin", UniqueNames.Phone(), Role.Finance, now, WellKnownDepartments.FinanceId).Value;
        User siteEngineer = User.Create(
            UniqueNames.Code("bb-eng"), "bb-eng", UniqueNames.Phone(), Role.SiteEngineer, now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;
        Bab bab = Bab.Create(UniqueNames.Code("BBB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem itemA = CatalogueItem.Create(
            UniqueNames.Code("BBA"), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;
        CatalogueItem itemB = CatalogueItem.Create(
            UniqueNames.Code("BBI"), "حديد تسليح", "طن", bab.Id, new Money(1_000m), new Money(1_500m), "Rebar").Value;

        context.Clients.Add(client);
        context.Users.AddRange(owner, technicalOffice, otherTechnicalOffice, finance, siteEngineer);
        context.Babs.Add(bab);
        context.CatalogueItems.AddRange(itemA, itemB);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _otherTechnicalOffice = otherTechnicalOffice.Id;
        _finance = finance.Id;
        _siteEngineer = siteEngineer.Id;
        _babId = bab.Id;
        _itemA = itemA.Id;
        _itemB = itemB.Id;
        _codeA = itemA.Code;
    }

    private static User Technical(string name, DateTimeOffset now) =>
        User.Create(
            UniqueNames.Code(name), name, UniqueNames.Phone(), Role.TechnicalOffice, now,
            WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical).Value;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
