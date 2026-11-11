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

    [Fact]
    public void ExtractRead_is_every_project_reader_except_the_junior_engineer()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.ExtractRead);

        definition.Scope.Should().Be(PermissionScope.ProjectScoped);
        definition.TouchesMoney.Should().BeFalse("the extract body keeps its own money rule");
        Granted(definition).Should().BeEquivalentTo(
        [
            (Role.Owner, AssignmentLevel.Standard), (Role.Finance, AssignmentLevel.Standard),
            (Role.TechnicalOffice, AssignmentLevel.Standard), (Role.MarketingSales, AssignmentLevel.Standard),
            (Role.SiteEngineer, AssignmentLevel.Supervisor),
            // Head of Design keeps the extracts reach ProjectRead gave him before D-221.
            (Role.HeadOfDesign, AssignmentLevel.Standard),
        ]);
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
