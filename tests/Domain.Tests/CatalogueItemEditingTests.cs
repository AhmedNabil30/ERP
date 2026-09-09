using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-202's domain half — creation, re-pricing and the correction surface the entity did not have
/// until this story.
/// </summary>
/// <remarks>
/// <para>
/// <c>CatalogueItem.Create</c>, <c>Reprice</c> and <c>Archive</c> already existed (verified in the
/// story's own "what already exists" table) but carried no test at this layer. <c>SetDescription</c>
/// and <c>SetUnit</c> are new — KAFF-202's own acceptance criteria require editing a description
/// alongside a price in one request (<c>AC-202-I</c>, <c>TC-2-026</c>), and nothing on the entity could
/// do that before this story.
/// </para>
/// <para>
/// These are entity tests on purpose. The endpoint half — the permission gate, the unique-code race,
/// and what the trail contains afterwards — is in <c>Api.Tests/CreateCatalogueItemTests.cs</c> and
/// <c>Api.Tests/EditCatalogueItemTests.cs</c>.
/// </para>
/// </remarks>
public sealed class CatalogueItemEditingTests
{
    private static readonly Guid BabId = Guid.NewGuid();

    // ---- AC-202-A / AC-202-C · creation carries exactly §4.1's fields, at four decimals ----------

    [Fact]
    public void An_item_is_created_with_exactly_the_given_values_and_is_active()
    {
        CatalogueItem item = NewItem("CONC-100", cost: 987.6543m, sell: 1234.5678m);

        item.Code.Should().Be("CONC-100");
        item.DescriptionAr.Should().Be("خرسانة عادية");
        item.Unit.Should().Be("م٣");
        item.BabId.Should().Be(BabId);
        item.CostPrice.Amount.Should().Be(987.6543m);
        item.BaseSellRate.Amount.Should().Be(1234.5678m);
        item.Status.Should().Be(CatalogueItemStatus.Active);
    }

    [Fact]
    public void The_code_is_trimmed_and_upper_invariant_so_a_lowercase_duplicate_cannot_hide()
    {
        CatalogueItem item = NewItem("  conc-100  ");

        item.Code.Should().Be("CONC-100", "AC-202-B's case-insensitivity depends on the stored form being normalised");
    }

    // ---- AC-202-D · a negative price is refused, and only a negative one ------------------------

    [Fact]
    public void A_negative_cost_price_is_refused_on_creation()
    {
        Result<CatalogueItem> result = CatalogueItem.Create(
            "CONC-101", "خرسانة", "م٣", BabId, new Money(-1m), new Money(100m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.CostPriceMustNotBeNegative);
    }

    [Fact]
    public void A_negative_sell_rate_is_refused_on_creation()
    {
        Result<CatalogueItem> result = CatalogueItem.Create(
            "CONC-102", "خرسانة", "م٣", BabId, new Money(100m), new Money(-1m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.SellRateMustNotBeNegative);
    }

    /// <summary>AC-202-D's positive control — nothing in spec.md forbids selling at a loss.</summary>
    [Fact]
    public void A_sell_rate_below_cost_is_accepted()
    {
        Result<CatalogueItem> result = CatalogueItem.Create(
            "CONC-103", "خرسانة", "م٣", BabId, new Money(200m), new Money(150m));

        result.IsSuccess.Should().BeTrue("nothing in spec.md forbids selling at a loss");
        result.Value.CostPrice.Amount.Should().Be(200m);
        result.Value.BaseSellRate.Amount.Should().Be(150m);
    }

    // ---- SetDescription — new behaviour this story adds -------------------------------------------

    [Fact]
    public void A_description_can_be_corrected()
    {
        CatalogueItem item = NewItem("CONC-104");

        item.SetDescription("خرسانة مسلحة", "Reinforced concrete").IsSuccess.Should().BeTrue();

        item.DescriptionAr.Should().Be("خرسانة مسلحة");
        item.DescriptionEn.Should().Be("Reinforced concrete");
    }

    [Fact]
    public void A_corrected_description_is_trimmed_exactly_as_a_created_one_is()
    {
        CatalogueItem item = NewItem("CONC-105");

        item.SetDescription("  خرسانة نظيفة  ", null).IsSuccess.Should().BeTrue();

        item.DescriptionAr.Should().Be(
            "خرسانة نظيفة",
            "Create trims, so the edit path must too — a second, laxer route to one column is how an "
            + "invariant stops being one");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_description_cannot_be_edited_away(string? blank)
    {
        CatalogueItem item = NewItem("CONC-106");

        Result result = item.SetDescription(blank!, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.DescriptionRequired);
        item.DescriptionAr.Should().Be("خرسانة عادية", "a refused edit changes nothing");
    }

    [Fact]
    public void A_description_longer_than_the_column_is_refused()
    {
        CatalogueItem item = NewItem("CONC-107");

        Result result = item.SetDescription(new string('خ', CatalogueItem.MaxDescriptionLength + 1), null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.DescriptionRequired);
    }

    // ---- SetUnit — new behaviour this story adds ---------------------------------------------------

    [Fact]
    public void A_unit_can_be_corrected()
    {
        CatalogueItem item = NewItem("CONC-108");

        item.SetUnit("م٢").IsSuccess.Should().BeTrue();

        item.Unit.Should().Be("م٢");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_unit_cannot_be_edited_away(string? blank)
    {
        CatalogueItem item = NewItem("CONC-109");

        Result result = item.SetUnit(blank!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.UnitRequired);
        item.Unit.Should().Be("م٣", "a refused edit changes nothing");
    }

    // ---- AC-202-C · re-pricing keeps four decimals ---------------------------------------------

    [Fact]
    public void Reprice_keeps_both_values_exact_to_the_fourth_decimal()
    {
        CatalogueItem item = NewItem("CONC-110");

        item.Reprice(new Money(555.1234m), new Money(777.9876m)).IsSuccess.Should().BeTrue();

        item.CostPrice.Amount.Should().Be(555.1234m);
        item.BaseSellRate.Amount.Should().Be(777.9876m);
    }

    // ---- AC-202-D applied to Reprice too ---------------------------------------------------------

    [Fact]
    public void Reprice_refuses_a_negative_cost_price_and_changes_nothing()
    {
        CatalogueItem item = NewItem("CONC-111", cost: 100m, sell: 200m);

        Result result = item.Reprice(new Money(-1m), new Money(200m));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.CostPriceMustNotBeNegative);
        item.CostPrice.Amount.Should().Be(100m, "a refused reprice moves neither price");
        item.BaseSellRate.Amount.Should().Be(200m);
    }

    [Fact]
    public void Reprice_accepts_a_sell_rate_below_cost()
    {
        CatalogueItem item = NewItem("CONC-112");

        item.Reprice(new Money(500m), new Money(400m)).IsSuccess.Should().BeTrue(
            "nothing in spec.md forbids re-pricing into a loss either");
    }

    // ---- AC-206-A / AC-206-E · Archive, and archiving twice is refused ---------------------------

    [Fact]
    public void An_active_item_is_archived_and_every_other_field_is_untouched()
    {
        CatalogueItem item = NewItem("CONC-113", cost: 100m, sell: 150m);

        item.Archive().IsSuccess.Should().BeTrue();

        item.Status.Should().Be(CatalogueItemStatus.Archived);
        item.Code.Should().Be("CONC-113");
        item.CostPrice.Amount.Should().Be(100m, "AC-206-A: archiving touches Status and nothing else");
        item.BaseSellRate.Amount.Should().Be(150m);
    }

    [Fact]
    public void Archiving_an_already_archived_item_is_refused_and_changes_nothing()
    {
        CatalogueItem item = NewItem("CONC-114");
        item.Archive();

        Result result = item.Archive();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.AlreadyArchived);
        item.Status.Should().Be(CatalogueItemStatus.Archived, "a refused second archive changes nothing");
    }

    // ---- Q66, D-130 §4 · Unarchive, the mirror of Archive -----------------------------------------

    [Fact]
    public void An_archived_item_is_unarchived_back_to_active()
    {
        CatalogueItem item = NewItem("CONC-115");
        item.Archive();

        item.Unarchive().IsSuccess.Should().BeTrue();

        item.Status.Should().Be(CatalogueItemStatus.Active);
    }

    [Fact]
    public void Unarchiving_an_item_that_is_not_archived_is_refused_and_changes_nothing()
    {
        CatalogueItem item = NewItem("CONC-116");

        Result result = item.Unarchive();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            MasterDataErrors.NotArchived,
            "MasterDataErrors.NotArchived exists for exactly this shape — D-130 §4");
        item.Status.Should().Be(CatalogueItemStatus.Active, "a refused unarchive changes nothing");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static CatalogueItem NewItem(string code, decimal cost = 100m, decimal sell = 150m)
    {
        Result<CatalogueItem> created = CatalogueItem.Create(
            code, "خرسانة عادية", "م٣", BabId, new Money(cost), new Money(sell));

        created.IsSuccess.Should().BeTrue();
        return created.Value;
    }
}
