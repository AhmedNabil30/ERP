using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// spec.md §6.7, after Karim's ruling of 2026-08-21 moved the rate onto the contract.
/// </summary>
/// <remarks>
/// <para>
/// §6.7 exists to stop one specific thing: *"collections will never match issued extracts and staff
/// will invent adjustments to close the gap."* A wrong rate is not a validation nicety — it is a
/// permanent 1–5% shortfall on every collection for that contract, small enough to be closed by hand
/// and large enough to matter by the end of a project.
/// </para>
/// <para>
/// The rate lived on <c>Client</c> until 2026-08-21. §5.4 lets one client hold a design contract and
/// an execution contract at the same time, and §6.7 sets the rate by what is supplied, so a single
/// value per client could not express both. See decisions.md D-049.
/// </para>
/// </remarks>
public sealed class WithholdingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_three_statutory_rates_are_one_three_and_five_percent()
    {
        // spec.md §6.7, Law 91/2005 and Decree 308/2018. Named here so a change to the table is a
        // deliberate edit to a test rather than a quiet edit to a switch.
        WithholdingRates.For(WithholdingCategory.None).Should().Be(Percentage.Zero);
        WithholdingRates.For(WithholdingCategory.ContractingAndSupplies).Should().Be(Percentage.FromPercent(1m));
        WithholdingRates.For(WithholdingCategory.Services).Should().Be(Percentage.FromPercent(3m));
        WithholdingRates.For(WithholdingCategory.ProfessionalFees).Should().Be(Percentage.FromPercent(5m));
    }

    [Fact]
    public void One_client_can_hold_two_contracts_at_two_different_rates()
    {
        // The case that forced the move, stated as a test so it cannot regress into a client-level
        // field again: spec.md §5.4 links a design project to its execution project for one client,
        // and §6.7 rates them differently — 5% professional fees against 1% contracting.
        Guid clientId = Guid.CreateVersion7();

        Project design = NewProject(clientId, ContractType.Design);
        Project execution = NewProject(clientId, ContractType.LumpSum);

        design.SetWithholding(WithholdingCategory.ProfessionalFees, ClientKind.Corporate)
            .IsSuccess.Should().BeTrue();
        execution.SetWithholding(WithholdingCategory.ContractingAndSupplies, ClientKind.Corporate)
            .IsSuccess.Should().BeTrue();

        design.WithholdingCategory.Should().Be(WithholdingCategory.ProfessionalFees);
        execution.WithholdingCategory.Should().Be(WithholdingCategory.ContractingAndSupplies);

        WithholdingRates.For(design.WithholdingCategory)
            .Should().NotBe(WithholdingRates.For(execution.WithholdingCategory),
                "the same client withholds at different rates on different contracts");
    }

    [Fact]
    public void A_contract_for_an_individual_client_cannot_withhold()
    {
        // spec.md §6.7: "Individual clients do not withhold." Refused in the entity rather than in a
        // validator — a validator guards one endpoint, and this invariant has to hold wherever the
        // project is reached from.
        Project project = NewProject(Guid.CreateVersion7(), ContractType.LumpSum);

        foreach (WithholdingCategory category in new[]
                 {
                     WithholdingCategory.ContractingAndSupplies,
                     WithholdingCategory.Services,
                     WithholdingCategory.ProfessionalFees,
                 })
        {
            Result result = project.SetWithholding(category, ClientKind.Individual);

            result.IsFailure.Should().BeTrue($"{category} on an individual contradicts §6.7");
            result.Error.Should().Be(MasterDataErrors.IndividualDoesNotWithhold);
        }

        project.WithholdingCategory.Should().Be(WithholdingCategory.None, "no refused value was stored");
    }

    [Fact]
    public void None_is_always_allowed_including_for_an_individual()
    {
        Project project = NewProject(Guid.CreateVersion7(), ContractType.LumpSum);

        project.SetWithholding(WithholdingCategory.None, ClientKind.Individual)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_new_contract_withholds_nothing_until_finance_says_otherwise()
    {
        // The default has to be the safe one. Karim, 2026-08-21, put the rate in Finance's hands
        // "during contract creation/approval", so between creation and that moment the contract must
        // claim no withholding rather than guess one — a guessed rate is indistinguishable from a
        // decided one by the time an extract is issued.
        NewProject(Guid.CreateVersion7(), ContractType.LumpSum)
            .WithholdingCategory.Should().Be(WithholdingCategory.None);
    }

    [Fact]
    public void An_individual_client_cannot_carry_a_tax_registration_number()
    {
        // The same claim by another field. A registration number on an individual asserts exactly
        // what §6.7 denies, so it is refused in the same place the rate is.
        Client individual = NewClient(ClientKind.Individual);

        Result refused = individual.SetTaxRegistration("123-456-789");

        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(MasterDataErrors.IndividualDoesNotWithhold);
        individual.TaxRegistrationNumber.Should().BeNull();

        // Clearing it is not the same claim, so it stays legal.
        individual.SetTaxRegistration(null).IsSuccess.Should().BeTrue();

        Client corporate = NewClient(ClientKind.Corporate);
        corporate.SetTaxRegistration("123-456-789").IsSuccess.Should().BeTrue();
        corporate.TaxRegistrationNumber.Should().Be("123-456-789");
    }

    private static Project NewProject(Guid clientId, ContractType contractType) =>
        Project.Create(
            $"P{Guid.CreateVersion7():N}"[..12],
            "مشروع",
            clientId,
            contractType,
            Now).Value;

    private static Client NewClient(ClientKind kind) =>
        Client.Create(
            $"C{Guid.CreateVersion7():N}"[..12],
            "عميل",
            PhoneNumber.Create("01000000000").Value,
            kind,
            Now).Value;
}
