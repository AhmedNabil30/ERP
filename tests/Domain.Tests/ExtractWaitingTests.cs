using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// What an extract waits for, and on whom, is derived from the state machine and the permission catalogue. These
/// tests fail when a status or a transition is added and the mapping does not follow, and they check the one claim
/// the mapping makes by hand (which steps are decisions) against what the real gate methods do.
/// </summary>
public sealed class ExtractWaitingTests
{
    private static readonly ExtractStatus[] Terminal = [ExtractStatus.Collected, ExtractStatus.Void];

    [Fact]
    public void Every_status_is_either_waiting_for_someone_or_in_the_stated_terminal_set()
    {
        foreach (ExtractStatus status in Enum.GetValues<ExtractStatus>())
        {
            ExtractWaitingOn waiting = ExtractWaiting.For(status);

            if (Terminal.Contains(status))
            {
                waiting.IsTerminal.Should().BeTrue($"{status} ends the chain");
                waiting.Roles.Should().BeEmpty();
            }
            else
            {
                waiting.IsTerminal.Should().BeFalse($"{status} must say what it waits for; if a new status was added, classify it");
                waiting.Roles.Should().NotBeEmpty($"{status} is waiting on a role");
            }
        }
    }

    [Fact]
    public void Every_forward_transition_of_the_machine_has_a_permission_that_performs_it()
    {
        foreach (ExtractStatus from in Enum.GetValues<ExtractStatus>())
        {
            // Void is the end of a dispute resolved by a revision (D-241 item 2), an exit like Draft and Disputed, not a step.
            foreach (ExtractStatus to in Extract.LegalTargetsFrom(from)
                .Where(target => target is not (ExtractStatus.Draft or ExtractStatus.Disputed or ExtractStatus.Void)))
            {
                ExtractWaiting.PermissionToEnter.Should().ContainKey(to, $"{from} to {to} is a legal transition");
            }
        }
    }

    [Fact]
    public void The_mapping_holds_no_row_for_a_status_the_machine_never_enters()
    {
        IEnumerable<ExtractStatus> entered = Enum.GetValues<ExtractStatus>().SelectMany(Extract.LegalTargetsFrom);

        ExtractWaiting.PermissionToEnter.Keys.Should().BeSubsetOf(entered);
    }

    [Fact]
    public void Who_holds_each_step_is_read_from_the_catalogue()
    {
        foreach (ExtractStatus status in Enum.GetValues<ExtractStatus>().Where(s => !Terminal.Contains(s)))
        {
            ExtractWaitingOn waiting = ExtractWaiting.For(status);
            IEnumerable<Role> expected = PermissionCatalogue.Of(waiting.Permission!.Value).Grants
                .Where(grant => grant.Role is not null).Select(grant => grant.Role!.Value);

            waiting.Roles.Select(role => role.Role).Should().BeEquivalentTo(expected, $"the roles for {status} come from the catalogue");
        }
    }

    // Issued waits for the collection, which Finance records (Q97, assumed). OwnerApproved waits to be issued,
    // which the Owner does. The assertion is on what spec.md section 7 names, so a regrant in the catalogue shows here.
    [Theory]
    [InlineData(ExtractStatus.Draft, Role.SiteEngineer, AssignmentLevel.Supervisor)]
    [InlineData(ExtractStatus.QcSubmitted, Role.TechnicalOffice, AssignmentLevel.Standard)]
    [InlineData(ExtractStatus.TechnicalApproved, Role.Finance, AssignmentLevel.Standard)]
    [InlineData(ExtractStatus.PartiallyApproved, Role.Finance, AssignmentLevel.Standard)]
    [InlineData(ExtractStatus.DeductionsApplied, Role.Owner, AssignmentLevel.Standard)]
    [InlineData(ExtractStatus.OwnerApproved, Role.Owner, AssignmentLevel.Standard)]
    [InlineData(ExtractStatus.Issued, Role.Finance, AssignmentLevel.Standard)]
    [InlineData(ExtractStatus.Disputed, Role.Finance, AssignmentLevel.Standard)]
    public void The_chain_waits_on_the_role_spec_section_7_names(ExtractStatus status, Role role, AssignmentLevel level)
    {
        ExtractWaiting.For(status).Roles.Should().ContainSingle().Which.Should().Be(new WaitingRole(role, level));
    }

    [Fact]
    public async Task A_status_is_a_decision_gate_exactly_when_the_real_gate_bars_someone_who_already_acted()
    {
        ExtractHarness harness = new();
        Guid preparer = ExtractHarness.NewUser();
        Guid technical = ExtractHarness.NewUser();
        Guid accounts = ExtractHarness.NewUser();
        Guid owner = ExtractHarness.NewUser();
        DateTimeOffset now = ExtractHarness.Now;
        LumpSumBillingCalculator calculator = new(harness.Rulings);
        Percentage whole = Percentage.FromFraction(1m);

        Extract extract = harness.Prepare(preparer);

        // Draft to QcSubmitted: the preparer may submit the report.
        ExtractWaiting.For(ExtractStatus.Draft).IsDecisionGate.Should().BeFalse();
        extract.SubmitQcReport(preparer, now).IsSuccess.Should().BeTrue();

        // QcSubmitted to approved: the preparer, who has acted, is barred.
        ExtractWaiting.For(ExtractStatus.QcSubmitted).IsDecisionGate.Should().BeTrue();
        extract.ApproveTechnicalOffice(ProjectStatus.Active, preparer, whole, null, harness.Rulings, now).IsFailure.Should().BeTrue();
        extract.ApproveTechnicalOffice(ProjectStatus.Active, technical, whole, null, harness.Rulings, now).IsSuccess.Should().BeTrue();

        // TechnicalApproved to DeductionsApplied: the technical approver is barred.
        ExtractWaiting.For(ExtractStatus.TechnicalApproved).IsDecisionGate.Should().BeTrue();
        (await extract.ApplyDeductionsAsync(technical, calculator, harness.Balances(), harness.Context(), now, CancellationToken.None))
            .IsFailure.Should().BeTrue();
        Result<PostingSet?> applied = await extract.ApplyDeductionsAsync(
            accounts, calculator, harness.Balances(), harness.Context(), now, CancellationToken.None);
        applied.IsSuccess.Should().BeTrue();
        harness.Commit(applied.Value);

        // DeductionsApplied to OwnerApproved: Accounts is barred.
        ExtractWaiting.For(ExtractStatus.DeductionsApplied).IsDecisionGate.Should().BeTrue();
        extract.OwnerApprove(accounts, harness.Context(), now).IsFailure.Should().BeTrue();
        Result<PostingSet?> approved = extract.OwnerApprove(owner, harness.Context(), now);
        approved.IsSuccess.Should().BeTrue();
        harness.Commit(approved.Value);

        // OwnerApproved to Issued: not a decision gate (D-193), the Owner who approved may issue.
        ExtractWaiting.For(ExtractStatus.OwnerApproved).IsDecisionGate.Should().BeFalse();
        Result<PostingSet?> issued = extract.Issue(owner, harness.Context(), now);
        issued.IsSuccess.Should().BeTrue();
        harness.Commit(issued.Value);

        // Issued to Collected: not a decision gate, whoever issued may record the collection.
        ExtractWaiting.For(ExtractStatus.Issued).IsDecisionGate.Should().BeFalse();
        extract.Collect(owner, harness.Context(), now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void The_partial_approval_status_is_held_by_the_same_gate_as_the_full_one()
    {
        ExtractWaiting.For(ExtractStatus.PartiallyApproved).Permission
            .Should().Be(ExtractWaiting.For(ExtractStatus.TechnicalApproved).Permission);
    }
}
