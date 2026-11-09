using Kaff.Api.Features.Extracts;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;

namespace Kaff.Api.Tests.Features.Extracts;

/// <summary>
/// The waiting-on read separates the role that holds a gate from the people who may actually take it: a person
/// with the role who already acted on the extract this cycle is listed as barred, by the same
/// <c>Extract.EnsureMayDecide</c> the gate itself calls. No database: the view is a pure function of the extract
/// and the staff list.
/// </summary>
public sealed class ExtractWaitingOnViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    private static Extract SubmittedBy(Guid preparer)
    {
        Project project = Project.Create("PRJ-W", "W", Guid.CreateVersion7(), ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        Extract extract = Extract.Prepare(
            project, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            new Money(300_000m), Money.Zero, new Money(100_000m), Money.Zero, preparer, Now).Value;
        extract.SubmitQcReport(Guid.CreateVersion7(), Now).IsSuccess.Should().BeTrue();
        return extract;
    }

    [Fact]
    public void A_holder_of_the_role_who_already_acted_is_barred_and_the_others_still_hold_the_gate()
    {
        // Contrived on purpose: a Technical Office user who also prepared this extract (a role changed mid-flow).
        StaffMember prepared = new(Guid.CreateVersion7(), "Prepared It", Role.TechnicalOffice, AssignmentLevel.Standard);
        StaffMember fresh = new(Guid.CreateVersion7(), "Fresh", Role.TechnicalOffice, AssignmentLevel.Standard);
        StaffMember finance = new(Guid.CreateVersion7(), "Finance", Role.Finance, AssignmentLevel.Standard);
        Extract extract = SubmittedBy(prepared.UserId);

        WaitingOnResponse waiting = ExtractWaitingOnView.Build(extract, [prepared, fresh, finance]);

        waiting.IsDecisionGate.Should().BeTrue();
        waiting.Holders.Should().ContainSingle().Which.Should().Be(fresh);
        waiting.BarredHolders.Should().ContainSingle().Which.Should().Be(prepared);
    }

    [Fact]
    public void A_site_engineer_below_the_supervisor_level_does_not_hold_the_qc_report_step()
    {
        Project project = Project.Create("PRJ-W2", "W2", Guid.CreateVersion7(), ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        Extract draft = Extract.Prepare(
            project, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            new Money(300_000m), Money.Zero, new Money(100_000m), Money.Zero, Guid.CreateVersion7(), Now).Value;
        StaffMember junior = new(Guid.CreateVersion7(), "Junior", Role.SiteEngineer, AssignmentLevel.Junior);
        StaffMember supervisor = new(Guid.CreateVersion7(), "Supervisor", Role.SiteEngineer, AssignmentLevel.Supervisor);

        WaitingOnResponse waiting = ExtractWaitingOnView.Build(draft, [junior, supervisor]);

        waiting.Holders.Should().ContainSingle().Which.Should().Be(supervisor);
        waiting.BarredHolders.Should().BeEmpty();
    }
}
