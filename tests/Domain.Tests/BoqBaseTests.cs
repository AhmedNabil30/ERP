using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-418: the prepared base BOQ, the one-quantity rule, and who holds the base permissions.</summary>
public sealed class BoqBaseTests
{
    [Fact]
    public void A_base_needs_a_name_and_no_line_has_a_negative_quantity()
    {
        BoqBase.Create("  ", TestAccounts.Now).Error.Should().Be(BoqErrors.BaseNameRequired);

        BoqBase boqBase = BoqBase.Create(" شقة 120 ", TestAccounts.Now).Value;
        boqBase.Name.Should().Be("شقة 120");
        boqBase.ReplaceLines([(Guid.NewGuid(), -1m)]).Error.Should().Be(BoqErrors.QuantityMustNotBeNegative);
        boqBase.Lines.Should().BeEmpty("a refused replace changes nothing");
    }

    [Fact]
    public void Replacing_the_lines_numbers_them_from_one_and_an_archived_base_is_read_only()
    {
        BoqBase boqBase = BoqBase.Create("base", TestAccounts.Now).Value;
        boqBase.ReplaceLines([(Guid.NewGuid(), 1m), (Guid.NewGuid(), 2m)]).IsSuccess.Should().BeTrue();
        boqBase.Lines.Select(line => line.Position).Should().Equal(1, 2);

        boqBase.Archive(TestAccounts.Now).IsSuccess.Should().BeTrue();
        boqBase.Archive(TestAccounts.Now).Error.Should().Be(BoqErrors.BaseArchived);
        boqBase.Rename("other").Error.Should().Be(BoqErrors.BaseArchived);
        boqBase.ReplaceLines([]).Error.Should().Be(BoqErrors.BaseArchived);
    }

    [Fact]
    public void A_line_is_priced_on_its_estimated_quantity_until_a_survey_replaces_it()
    {
        Project project = Project.Create("PRJ-Q", "Q", TestAccounts.ClientId, ContractType.LumpSum, TestAccounts.Now).Value;
        Bab bab = Bab.Create("CON", "أعمال خرسانة", "Concrete Works", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            "CON-1", "خرسانة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Concrete").Value;
        Boq boq = Boq.Open(project, TestAccounts.Now).Value;
        BoqLine line = boq.AddCatalogueLine(item, bab, 5m, null).Value;
        BoqQuantityBasis basis = RulingSet.Default.ContractValueSource.QuantityBasis;

        line.QuantityFor(basis).Should().Be(5m);
        boq.Total(RulingSet.Default.ContractValueSource).Value.Should().Be(new Money(8_625m));

        boq.SetQuantities(line.Id, 5m, 4m).IsSuccess.Should().BeTrue();
        line.QuantityFor(basis).Should().Be(4m);

        boq.SetQuantities(line.Id, null, null).IsSuccess.Should().BeTrue();
        boq.FirstLineWithoutQuantity(RulingSet.Default.ContractValueSource).Should().BeSameAs(line);
        boq.Total(RulingSet.Default.ContractValueSource).Error.Should().Be(BoqErrors.LineQuantityMissing);
    }

    [Fact]
    public void Only_the_owner_and_the_technical_office_hold_the_base_boq_permissions()
    {
        foreach (Permission permission in new[] { Permission.BoqBaseRead, Permission.BoqBaseManage })
        {
            PermissionCatalogue.Of(permission).Grants.Select(grant => grant.Role)
                .Should().BeEquivalentTo([Role.Owner, Role.TechnicalOffice]);
            PermissionCatalogue.Of(permission).Scope.Should().Be(PermissionScope.CompanyWide);
            PermissionCatalogue.Of(permission).TouchesMoney.Should().BeFalse();
        }
    }
}
