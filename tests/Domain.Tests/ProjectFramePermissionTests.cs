using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-420: the three permissions that decide the project frame's tabs and money. D-217 item 37, D-218 items 5, 6.</summary>
public sealed class ProjectFramePermissionTests
{
    [Fact]
    public void BoqRead_goes_to_the_owner_the_technical_office_and_the_supervising_engineer_only()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.BoqRead);

        definition.Scope.Should().Be(PermissionScope.ProjectScoped);
        definition.TouchesMoney.Should().BeFalse();
        Granted(definition).Should().BeEquivalentTo(
            [(Role.Owner, AssignmentLevel.Standard), (Role.TechnicalOffice, AssignmentLevel.Standard), (Role.SiteEngineer, AssignmentLevel.Supervisor)]);
    }

    /// <summary>D-241 item 1: the junior site engineer reads the extracts list too; its money stays null for him.</summary>
    [Fact]
    public void ExtractRead_is_every_project_reader_the_junior_engineer_included()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.ExtractRead);

        definition.Scope.Should().Be(PermissionScope.ProjectScoped);
        definition.TouchesMoney.Should().BeFalse("the extract body keeps its own money rule");
        Granted(definition).Should().BeEquivalentTo(
        [
            (Role.Owner, AssignmentLevel.Standard), (Role.Finance, AssignmentLevel.Standard),
            (Role.TechnicalOffice, AssignmentLevel.Standard), (Role.MarketingSales, AssignmentLevel.Standard),
            (Role.SiteEngineer, AssignmentLevel.Junior),
            // Head of Design keeps the extracts reach ProjectRead gave him before D-221.
            (Role.HeadOfDesign, AssignmentLevel.Standard),
        ]);
    }

    /// <summary>D-239 item 4: the Owner and Finance read the cheques in hand; moving one stays Finance's alone.</summary>
    [Fact]
    public void ChequeRead_is_the_owner_and_finance_and_only_finance_moves_a_cheque()
    {
        PermissionDefinition read = PermissionCatalogue.Of(Permission.ChequeRead);

        read.Scope.Should().Be(PermissionScope.ProjectScoped);
        read.TouchesMoney.Should().BeTrue();
        Granted(read).Should().BeEquivalentTo([(Role.Owner, AssignmentLevel.Standard), (Role.Finance, AssignmentLevel.Standard)]);
        Granted(PermissionCatalogue.Of(Permission.ExtractCollect)).Should().BeEquivalentTo([(Role.Finance, AssignmentLevel.Standard)]);
    }

    /// <summary>D-241 item 4: the assigned supervisor records the client's change-order approval, not the Technical Office or the Owner.</summary>
    [Fact]
    public void ChangeOrderClientApprovalRecord_is_the_supervising_engineer_alone()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.ChangeOrderClientApprovalRecord);

        definition.Unresolved.Should().BeFalse("D-241 item 4 rules it");
        definition.TouchesMoney.Should().BeFalse();
        Granted(definition).Should().BeEquivalentTo([(Role.SiteEngineer, AssignmentLevel.Supervisor)]);
    }

    [Fact]
    public void ProjectMoneyRead_touches_money_and_is_not_given_to_the_junior_engineer()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.ProjectMoneyRead);

        definition.Scope.Should().Be(PermissionScope.ProjectScoped);
        definition.TouchesMoney.Should().BeTrue();
        Granted(definition).Should().BeEquivalentTo(
        [
            (Role.Owner, AssignmentLevel.Standard), (Role.Finance, AssignmentLevel.Standard),
            (Role.TechnicalOffice, AssignmentLevel.Standard), (Role.SiteEngineer, AssignmentLevel.Supervisor),
        ]);
    }

    private static IEnumerable<(Role? Role, AssignmentLevel Level)> Granted(PermissionDefinition definition) =>
        definition.Grants.Select(grant => (grant.Role, grant.MinimumAssignmentLevel));
}
