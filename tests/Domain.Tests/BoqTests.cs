using System.Reflection;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>
/// The BOQ (spec.md §4) and the contract-value derivation at signature.
/// </summary>
public sealed class BoqTests
{
    private readonly RulingSet _rulings = RulingSet.Default;
    private readonly Project _project =
        Project.Create("PRJ-BOQ", "Boq", TestAccounts.ClientId, ContractType.LumpSum, TestAccounts.Now).Value;
    private readonly Bab _bab = Bab.Create("CON", "أعمال خرسانة", "Concrete Works", Percentage.FromPercent(15m)).Value;
    private readonly CatalogueItem _item;

    public BoqTests()
    {
        _item = CatalogueItem.Create(
            "CON-1030", "خرسانة مسلحة", "م٣", _bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;
    }

    private Boq OpenBoq() => Boq.Open(_project, TestAccounts.Now).Value;

    // ---- copy, not reference -----------------------------------------------------------------------

    [Fact]
    public void A_boq_line_holds_no_key_to_the_catalogue_or_to_a_bab()
    {
        string[] forbidden = ["CatalogueItemId", "ItemId", "BabId", "CatalogueId"];

        typeof(BoqLine).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().NotIntersectWith(forbidden, "spec.md section 4.4: the BOQ copies catalogue values and holds no reference");
    }

    [Fact]
    public void Editing_the_catalogue_after_the_line_was_added_leaves_the_line_unchanged()
    {
        Boq boq = OpenBoq();
        BoqLine line = boq.AddCatalogueLine(_item, _bab, 10m, 12m).Value;

        _item.Reprice(new Money(9_999m), new Money(8_888m)).IsSuccess.Should().BeTrue();
        _item.SetDescription("وصف جديد", "New description");
        _item.SetUnit("م٢");
        _bab.SetDefaultMarkup(Percentage.FromPercent(90m));

        line.CostPrice.Should().Be(new Money(1_000m));
        line.BaseSellRate.Should().Be(new Money(1_500m));
        line.DescriptionAr.Should().Be("خرسانة مسلحة");
        line.Unit.Should().Be("م٣");
        line.LineMarkup.Should().Be(Percentage.FromPercent(15m));
    }

    [Fact]
    public void A_catalogue_line_copies_code_text_unit_prices_and_the_bab_snapshot()
    {
        BoqLine line = OpenBoq().AddCatalogueLine(_item, _bab, 10m, 12m).Value;

        line.ItemCode.Should().Be("CON-1030");
        line.DescriptionEn.Should().Be("Reinforced concrete");
        line.BabCode.Should().Be("CON");
        line.BabNameAr.Should().Be("أعمال خرسانة");
        line.IsCustom.Should().BeFalse();
        line.PendingCatalogueReview.Should().BeFalse();
    }

    [Fact]
    public void An_archived_catalogue_item_cannot_be_added()
    {
        _item.Archive().IsSuccess.Should().BeTrue();

        OpenBoq().AddCatalogueLine(_item, _bab, 1m, 1m).Error.Should().Be(BoqErrors.CatalogueItemArchived);
    }

    // ---- pricing (spec.md 4.2) ------------------------------------------------------------------------

    [Fact]
    public void The_sell_rate_is_base_times_one_plus_conditions_times_one_plus_markup()
    {
        // 1,500 x (1 + 10%) x (1 + 15%) = 1,897.50
        BoqLine line = OpenBoq()
            .AddCatalogueLine(_item, _bab, 1m, 1m, conditionsRate: Percentage.FromPercent(10m)).Value;

        line.SellRate.Should().Be(new Money(1_897.5m));
    }

    [Fact]
    public void A_markup_override_replaces_the_bab_default_for_that_line_only()
    {
        Boq boq = OpenBoq();
        BoqLine overridden = boq.AddCatalogueLine(_item, _bab, 1m, 1m, markupOverride: Percentage.FromPercent(30m)).Value;
        BoqLine defaulted = boq.AddCatalogueLine(_item, _bab, 1m, 1m).Value;

        overridden.LineMarkup.Should().Be(Percentage.FromPercent(30m));
        defaulted.LineMarkup.Should().Be(Percentage.FromPercent(15m));
    }

    // ---- three quantity states (spec.md 4.3) --------------------------------------------------------

    [Fact]
    public void A_line_keeps_estimated_surveyed_and_executed_quantities_side_by_side()
    {
        Boq boq = OpenBoq();
        BoqLine line = boq.AddCatalogueLine(_item, _bab, 10m, null).Value;

        line.QuantityEstimated.Should().Be(10m);
        line.QuantitySurveyed.Should().BeNull("the survey has not happened yet");
        line.QuantityExecuted.Should().Be(0m);

        boq.SetQuantities(line.Id, 10m, 12.5m).IsSuccess.Should().BeTrue();

        line.QuantityEstimated.Should().Be(10m, "the estimate is retained, not overwritten by the survey");
        line.QuantitySurveyed.Should().Be(12.5m);
    }

    [Fact]
    public void A_negative_quantity_is_refused()
    {
        Boq boq = OpenBoq();
        BoqLine line = boq.AddCatalogueLine(_item, _bab, 1m, 1m).Value;

        boq.SetQuantities(line.Id, -1m, 1m).Error.Should().Be(BoqErrors.QuantityMustNotBeNegative);
        boq.AddCatalogueLine(_item, _bab, 1m, -3m).Error.Should().Be(BoqErrors.QuantityMustNotBeNegative);
    }

    // ---- custom lines (spec.md 4.5) ---------------------------------------------------------------

    [Fact]
    public void A_custom_line_is_flagged_for_catalogue_review_and_touches_no_catalogue_row()
    {
        Boq boq = OpenBoq();

        BoqLine line = boq.AddCustomLine(
            null, "حفر خاص", "Special excavation", "م٣", _bab, new Money(50m), new Money(80m), 5m, 5m).Value;

        line.IsCustom.Should().BeTrue();
        line.PendingCatalogueReview.Should().BeTrue();
        line.ItemCode.Should().BeNull();
        line.LineMarkup.Should().Be(_bab.DefaultMarkup);
        _item.BaseSellRate.Should().Be(new Money(1_500m), "a custom line must never write the catalogue");
    }

    [Fact]
    public void A_custom_line_needs_a_description_and_a_unit()
    {
        Boq boq = OpenBoq();

        boq.AddCustomLine(null, " ", null, "م٣", _bab, Money.Zero, Money.Zero, 1m, 1m)
            .Error.Should().Be(MasterDataErrors.DescriptionRequired);
        boq.AddCustomLine(null, "بند", null, "", _bab, Money.Zero, Money.Zero, 1m, 1m)
            .Error.Should().Be(MasterDataErrors.UnitRequired);
    }

    // ---- the total ----------------------------------------------------------------------------------

    [Fact]
    public void The_total_is_priced_on_the_surveyed_quantity_not_the_estimate()
    {
        Boq boq = OpenBoq();
        boq.AddCatalogueLine(_item, _bab, 100m, 2m); // 1,725.00 x 2 = 3,450.00 (surveyed), not 172,500.00

        boq.Total(_rulings.ContractValueSource).Value.Should().Be(new Money(3_450m));
    }

    [Fact]
    public void The_total_is_the_sum_of_lines_each_rounded_to_two_decimals()
    {
        Boq boq = OpenBoq();
        Bab flat = Bab.Create("FLT", "ب", "Flat", Percentage.Zero).Value;
        CatalogueItem odd = CatalogueItem.Create("X-1", "بند", "عدد", flat.Id, Money.Zero, new Money(10.005m)).Value;

        boq.AddCatalogueLine(odd, flat, 1m, 3m);
        boq.AddCatalogueLine(odd, flat, 1m, 3m);

        // each line: 10.005 x 3 = 30.015 -> 30.02; two lines = 60.04, not 60.03
        boq.Total(_rulings.ContractValueSource).Value.Should().Be(new Money(60.04m));
    }

    [Fact]
    public void An_empty_boq_has_no_total()
    {
        OpenBoq().Total(_rulings.ContractValueSource).Error.Should().Be(BoqErrors.Empty);
    }

    [Fact]
    public void A_line_with_no_quantity_at_all_stops_the_total_rather_than_counting_as_zero()
    {
        Boq boq = OpenBoq();
        boq.AddCatalogueLine(_item, _bab, null, null);

        boq.Total(_rulings.ContractValueSource).Error.Should().Be(BoqErrors.LineQuantityMissing);
    }

    [Fact]
    public void Ruling_the_quantity_basis_differently_is_one_replacement()
    {
        Boq boq = OpenBoq();
        boq.AddCatalogueLine(_item, _bab, 10m, null);

        ContractValueSource onEstimate = new(BoqQuantityBasis.Estimated, 2);

        boq.Total(onEstimate).Value.Should().Be(new Money(17_250m));
    }

    // ---- the freeze ---------------------------------------------------------------------------------

    [Fact]
    public void A_frozen_boq_refuses_every_change()
    {
        Boq boq = OpenBoq();
        BoqLine line = boq.AddCatalogueLine(_item, _bab, 1m, 1m).Value;

        boq.Freeze(TestAccounts.Today).IsSuccess.Should().BeTrue();

        boq.AddCatalogueLine(_item, _bab, 1m, 1m).Error.Should().Be(BoqErrors.Frozen);
        boq.AddCustomLine(null, "بند", null, "عدد", _bab, Money.Zero, Money.Zero, 1m, 1m).Error.Should().Be(BoqErrors.Frozen);
        boq.SetQuantities(line.Id, 2m, 2m).Error.Should().Be(BoqErrors.Frozen);
        boq.SetPricing(line.Id, Percentage.Zero, Percentage.Zero).Error.Should().Be(BoqErrors.Frozen);
        boq.RemoveLine(line.Id).Error.Should().Be(BoqErrors.Frozen);
        boq.Lines.Should().HaveCount(1);
    }

    [Fact]
    public void A_boq_freezes_once_and_there_is_no_thaw()
    {
        Boq boq = OpenBoq();
        boq.Freeze(TestAccounts.Today).IsSuccess.Should().BeTrue();

        boq.Freeze(TestAccounts.Today).Error.Should().Be(BoqErrors.AlreadyFrozen);
        typeof(Boq).GetMethods().Select(method => method.Name).Should().NotContain(["Unfreeze", "Thaw", "Reopen"]);
    }

    [Fact]
    public void A_design_project_cannot_have_a_boq()
    {
        Project design = Project.Create("PRJ-DES", "Design", TestAccounts.ClientId, ContractType.Design, TestAccounts.Now).Value;

        Boq.Open(design, TestAccounts.Now).Error.Should().Be(BoqErrors.NotForThisContractType);
    }

    [Fact]
    public void A_signed_project_cannot_open_a_new_boq()
    {
        Project cp = Project.Create("PRJ-CP", "Cost plus", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;
        cp.Sign(TestAccounts.Today, null).IsSuccess.Should().BeTrue();

        Boq.Open(cp, TestAccounts.Now).Error.Should().Be(ProjectErrors.AlreadySigned);
    }

    // ---- Project.Sign: the value is taken, never typed ------------------------------------------------

    [Fact]
    public void Signing_a_lump_sum_takes_the_boq_total_as_the_contract_value()
    {
        _project.ContractValue.Should().BeNull("a new Lump Sum project has no value until it is signed");

        _project.Sign(TestAccounts.Today, new Money(1_000_000m)).IsSuccess.Should().BeTrue();

        _project.ContractValue.Should().Be(new Money(1_000_000m));
        _project.SignedOn.Should().Be(TestAccounts.Today);
    }

    [Fact]
    public void Signing_a_lump_sum_without_a_positive_boq_total_is_refused()
    {
        _project.Sign(TestAccounts.Today, null).Error.Should().Be(ProjectErrors.ContractValueRequired);
        _project.Sign(TestAccounts.Today, Money.Zero).Error.Should().Be(ProjectErrors.ContractValueRequired);
        _project.IsSigned.Should().BeFalse();
    }

    [Fact]
    public void A_signed_project_cannot_be_signed_again_or_have_its_terms_reset()
    {
        _project.Sign(TestAccounts.Today, new Money(10m)).IsSuccess.Should().BeTrue();

        _project.Sign(TestAccounts.Today, new Money(20m)).Error.Should().Be(ProjectErrors.AlreadySigned);
        _project.ContractValue.Should().Be(new Money(10m));
    }

    [Fact]
    public void Signing_a_design_project_keeps_area_times_rate_and_ignores_any_boq_total()
    {
        Project design = Project.Create("PRJ-D2", "Design", TestAccounts.ClientId, ContractType.Design, TestAccounts.Now).Value;
        design.SetDesignTerms(200m, new Money(450m)).IsSuccess.Should().BeTrue();

        design.Sign(TestAccounts.Today, new Money(1m)).IsSuccess.Should().BeTrue();

        design.ContractValue.Should().Be(new Money(90_000m));
    }

    [Fact]
    public void Signing_a_design_project_before_its_terms_are_set_is_refused()
    {
        Project design = Project.Create("PRJ-D3", "Design", TestAccounts.ClientId, ContractType.Design, TestAccounts.Now).Value;

        design.Sign(TestAccounts.Today, null).Error.Should().Be(ProjectErrors.ContractValueRequired);
    }

    [Fact]
    public void Signing_a_cost_plus_project_leaves_it_with_no_contract_value()
    {
        Project cp = Project.Create("PRJ-CP2", "Cost plus", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;

        cp.Sign(TestAccounts.Today, new Money(5m)).IsSuccess.Should().BeTrue();

        cp.ContractValue.Should().BeNull("spec.md section 5.2: Cost Plus has no fixed value");
    }

    [Fact]
    public void Lump_sum_terms_no_longer_take_a_contract_value()
    {
        typeof(Project).GetMethod(nameof(Project.SetLumpSumTerms))!.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().NotContain(typeof(Money), "the contract value is derived at signature, never typed");
    }
}
