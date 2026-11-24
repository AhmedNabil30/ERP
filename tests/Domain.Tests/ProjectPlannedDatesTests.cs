using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Tests;

/// <summary>Nabil, 2026-11-24: the planned start and end are entered at signing, both optional, and the end is never before the start.</summary>
public sealed class ProjectPlannedDatesTests
{
    [Fact]
    public void Signing_stamps_the_planned_dates_and_they_are_optional()
    {
        Project dated = Project.Create("PRJ-PD1", "Dated", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;
        dated.Sign(TestAccounts.Today, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).IsSuccess.Should().BeTrue();
        dated.PlannedStart.Should().Be(new DateOnly(2026, 1, 1));
        dated.PlannedEnd.Should().Be(new DateOnly(2026, 12, 31));

        Project undated = Project.Create("PRJ-PD2", "Undated", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;
        undated.Sign(TestAccounts.Today, null).IsSuccess.Should().BeTrue();
        undated.PlannedStart.Should().BeNull();
        undated.PlannedEnd.Should().BeNull();
    }

    [Fact]
    public void A_planned_end_before_the_planned_start_is_refused_and_nothing_is_signed()
    {
        Project project = Project.Create("PRJ-PD3", "Backwards", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;

        project.Sign(TestAccounts.Today, null, new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 31)).Error.Should().Be(ProjectErrors.PlannedDatesInvalid);

        project.IsSigned.Should().BeFalse();
        project.PlannedStart.Should().BeNull();
    }
}
