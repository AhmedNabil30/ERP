using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-212 — the supplier entity itself. The permission mechanism is
/// <c>PermissionEvaluatorTests</c>; what belongs here is the profile: creation, editing, and the
/// allow-list that proves no balance and no withholding rate ever land on this record.
/// </summary>
public sealed class SupplierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    // ---- TC-2-142 / AC-212-A -----------------------------------------------------------------

    [Fact]
    public void A_supplier_is_created_with_code_name_phone_and_address_and_is_active()
    {
        Supplier supplier = Supplier.Create(
            "S-1", "مورد", UniqueTestPhone(), Now, address: "القاهرة").Value;

        supplier.Name.Should().Be("مورد");
        supplier.Address.Should().Be("القاهرة");
        supplier.IsActive.Should().BeTrue();
    }

    // ---- TC-2-144 / AC-212-C, TC-2-147 / AC-212-F ----------------------------------------------

    /// <summary>
    /// A whitelist, not a search for suspect words — decisions.md D-106's own reasoning, same as
    /// <c>SubcontractorTests</c>'s equivalent. Any member added to this list, whatever it is called,
    /// fails here.
    /// </summary>
    [Fact]
    public void The_entity_carries_no_balance_no_amount_and_no_withholding_member()
    {
        typeof(Supplier).GetProperties().Select(property => property.Name).Should().BeEquivalentTo(
            [
                nameof(Supplier.Id), nameof(Supplier.Code), nameof(Supplier.Name),
                nameof(Supplier.PhoneEntered), nameof(Supplier.PhoneNormalised),
                nameof(Supplier.Phone), nameof(Supplier.TaxRegistrationNumber),
                nameof(Supplier.Address), nameof(Supplier.IsActive), nameof(Supplier.CreatedAt),
            ],
            "AC-212-C/F: no balance, outstanding, total, purchases-to-date, amount-typed or "
            + "withholding-rate member — D-139 §5 means this record never held a withholding category "
            + "at all, and this story never held a posting or an account");
    }

    // ---- D-139 §5 — SetTaxRegistration trims and blanks to null --------------------------------

    [Fact]
    public void SetTaxRegistration_trims_and_treats_blank_as_null()
    {
        Supplier supplier = Supplier.Create("S-2", "مورد", UniqueTestPhone(), Now).Value;

        supplier.SetTaxRegistration("  123-456  ");
        supplier.TaxRegistrationNumber.Should().Be("123-456");

        supplier.SetTaxRegistration("   ");
        supplier.TaxRegistrationNumber.Should().BeNull("blank clears the number, same as null");

        supplier.SetTaxRegistration(null);
        supplier.TaxRegistrationNumber.Should().BeNull();
    }

    [Fact]
    public void Create_trims_and_blanks_address_and_tax_registration_to_null()
    {
        Supplier supplier = Supplier.Create(
            "S-3", "مورد", UniqueTestPhone(), Now, address: "   ", taxRegistrationNumber: "   ").Value;

        supplier.Address.Should().BeNull();
        supplier.TaxRegistrationNumber.Should().BeNull();
    }

    // ---- KAFF-212 rule 10 / AC-212-J — one Edit call changes both fields ------------------------

    [Fact]
    public void Edit_changes_name_phone_address_and_tax_registration_together()
    {
        Supplier supplier = Supplier.Create("S-4", "القديم", UniqueTestPhone(), Now).Value;
        PhoneNumber newPhone = UniqueTestPhone();

        Result edited = supplier.Edit("الجديد", newPhone, "الإسكندرية", "999-888");

        edited.IsSuccess.Should().BeTrue();
        supplier.Name.Should().Be("الجديد");
        supplier.Phone.Normalised.Should().Be(newPhone.Normalised);
        supplier.Address.Should().Be("الإسكندرية");
        supplier.TaxRegistrationNumber.Should().Be("999-888");
    }

    [Fact]
    public void Edit_refuses_a_blank_name()
    {
        Supplier supplier = Supplier.Create("S-5", "مورد", UniqueTestPhone(), Now).Value;

        Result edited = supplier.Edit(string.Empty, UniqueTestPhone(), null, null);

        edited.IsFailure.Should().BeTrue();
        edited.Error.Should().Be(MasterDataErrors.NameRequired);
    }

    // ---- KAFF-212 rule 8 — archive replaces deletion --------------------------------------------

    [Fact]
    public void Archiving_an_already_archived_supplier_is_refused()
    {
        Supplier supplier = Supplier.Create("S-6", "مورد", UniqueTestPhone(), Now).Value;

        supplier.Archive().IsSuccess.Should().BeTrue();
        supplier.IsActive.Should().BeFalse();

        Result second = supplier.Archive();
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(MasterDataErrors.AlreadyArchived);
    }

    private static PhoneNumber UniqueTestPhone() =>
        PhoneNumber.Create("010" + Random.Shared.Next(10_000_000, 99_999_999).ToString(
            System.Globalization.CultureInfo.InvariantCulture)).Value;
}
