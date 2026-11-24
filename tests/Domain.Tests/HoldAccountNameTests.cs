using FluentAssertions;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>D-274: the project hold account reads «الحجز» in Arabic.</summary>
public sealed class HoldAccountNameTests
{
    [Fact]
    public void A_new_projects_hold_account_is_named_al_hajz()
    {
        Project project = Project.Create("HN-1", "مشروع", Guid.CreateVersion7(), ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        Account hold = ProjectAccountSetFactory.Open(project, AccountType.Hold, new DateOnly(2026, 11, 24)).Value;

        hold.NameAr.Should().Be("الحجز");
    }
}
