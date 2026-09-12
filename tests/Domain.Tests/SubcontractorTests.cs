using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-211 — the مقاول باطن entity itself. The permission mechanism is
/// <c>PermissionEvaluatorTests</c>; what belongs here is the profile: the default retention, its
/// round trip, and the allow-lists that prove no rate card and no withholding rate ever land on this
/// record.
/// </summary>
public sealed class SubcontractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    // ---- TC-2-127 / AC-211-A ---------------------------------------------------------------

    [Fact]
    public void A_subcontractor_created_with_no_retention_given_defaults_to_five_percent_and_is_active()
    {
        Subcontractor subcontractor = Subcontractor.Create(
            "SC-1", "مقاول باطن", UniqueTestPhone(), Now).Value;

        subcontractor.RetentionRate.Fraction.Should().Be(0.05m);
        subcontractor.IsActive.Should().BeTrue();
    }

    // ---- TC-2-129 / AC-211-C ----------------------------------------------------------------

    [Fact]
    public void A_retention_entered_as_a_whole_percent_is_stored_as_the_fraction()
    {
        Subcontractor subcontractor = Subcontractor.Create(
            "SC-2", "مقاول باطن", UniqueTestPhone(), Now, retentionRate: Percentage.FromPercent(5m)).Value;

        subcontractor.RetentionRate.Fraction.Should().Be(
            0.05m, "5 means five percent, not the integer 5 — the stored form is the fraction");
    }

    [Fact]
    public void A_fractional_percent_survives_its_round_trip_exactly()
    {
        Subcontractor subcontractor = Subcontractor.Create(
            "SC-3", "مقاول باطن", UniqueTestPhone(), Now, retentionRate: Percentage.FromPercent(2.5m)).Value;

        subcontractor.RetentionRate.Fraction.Should().Be(0.025m);
    }

    // ---- TC-2-128 / AC-211-B ----------------------------------------------------------------

    [Fact]
    public void Zeroing_one_subcontractors_retention_does_not_touch_another_or_any_default()
    {
        Subcontractor first = Subcontractor.Create("SC-4", "الأول", UniqueTestPhone(), Now).Value;
        Subcontractor second = Subcontractor.Create("SC-5", "الثاني", UniqueTestPhone(), Now).Value;

        first.SetRetentionRate(Percentage.FromPercent(0m));

        first.RetentionRate.Fraction.Should().Be(0m);
        second.RetentionRate.Fraction.Should().Be(0.05m, "zeroing one firm changes nothing for another");
        Subcontractor.DefaultRetentionRate.Fraction.Should().Be(
            0.05m, "the static default itself is untouched by any instance's edit");
    }

    // ---- TC-2-131 / AC-211-E, TC-2-133 / AC-211-G ---------------------------------------------

    /// <summary>
    /// A whitelist, not a search for suspect words — decisions.md D-106's own reasoning. Any member
    /// added to this list, whatever it is called, fails here.
    /// </summary>
    [Fact]
    public void The_entity_carries_no_balance_no_amount_and_no_withholding_member()
    {
        typeof(Subcontractor).GetProperties().Select(property => property.Name).Should().BeEquivalentTo(
            [
                nameof(Subcontractor.Id), nameof(Subcontractor.Code), nameof(Subcontractor.Name),
                nameof(Subcontractor.PhoneEntered), nameof(Subcontractor.PhoneNormalised),
                nameof(Subcontractor.Phone), nameof(Subcontractor.TradeBabId),
                nameof(Subcontractor.RetentionRate), nameof(Subcontractor.TaxRegistrationNumber),
                nameof(Subcontractor.IsActive), nameof(Subcontractor.CreatedAt),
            ],
            "AC-211-E/G: no balance, outstanding, total, amount-typed or withholding-rate member — "
            + "D-139 §5 moved the withholding category off this record entirely, and this story never "
            + "held a posting or an account");
    }

    // ---- D-147 point 4 — SetTaxRegistration trims and blanks to null --------------------------

    [Fact]
    public void SetTaxRegistration_trims_and_treats_blank_as_null()
    {
        Subcontractor subcontractor = Subcontractor.Create("SC-6", "مقاول", UniqueTestPhone(), Now).Value;

        subcontractor.SetTaxRegistration("  123-456  ");
        subcontractor.TaxRegistrationNumber.Should().Be("123-456");

        subcontractor.SetTaxRegistration("   ");
        subcontractor.TaxRegistrationNumber.Should().BeNull("blank clears the number, same as null");

        subcontractor.SetTaxRegistration(null);
        subcontractor.TaxRegistrationNumber.Should().BeNull();
    }

    [Fact]
    public void Edit_never_touches_the_tax_registration_number()
    {
        Subcontractor subcontractor = Subcontractor.Create("SC-7", "مقاول", UniqueTestPhone(), Now).Value;
        subcontractor.SetTaxRegistration("123-456");

        subcontractor.Edit("اسم جديد", UniqueTestPhone(), null).IsSuccess.Should().BeTrue();

        subcontractor.TaxRegistrationNumber.Should().Be(
            "123-456", "D-147: the profile edit and the tax registration number are two different "
            + "requests through two different endpoints — one must never move the other");
    }

    private static PhoneNumber UniqueTestPhone() =>
        PhoneNumber.Create("010" + Random.Shared.Next(10_000_000, 99_999_999).ToString(
            System.Globalization.CultureInfo.InvariantCulture)).Value;
}
