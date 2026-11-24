using Kaff.Domain.Authorization;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-806. The retention is the subcontractor's own account, on its own clock, and it never touches the client hold.
/// The stage is asserted, not only the amount: a release one day early is the defect.
/// </summary>
public sealed class SubcontractorRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly HandoverDay = new(2026, 11, 4);
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly PostingProjectStatePolicy Policy = RulingSet.Default.PostingProjectStates;

    private static readonly ProjectStatus[] Path =
        [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty];

    private static Project ProjectIn(ProjectStatus status)
    {
        Project project = Project.Create("PRJ-R", "Retention", TestAccounts.ClientId, ContractType.LumpSum, Now).Value;
        project.SetLumpSumTerms(
            ContractDefaults.AdvanceRate, ContractDefaults.HoldRate, ContractDefaults.AdvanceRecoveryRate,
            ContractDefaults.MaterialAdvanceRate, delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(HandoverDay, new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        foreach (ProjectStatus step in Path.Take(Array.IndexOf(Path, status) + 1))
        {
            if (project.Status == step)
            {
                continue; // D-230: signing already made the project Active.
            }

            project.TransitionTo(step, HandoverDay, "test").IsSuccess.Should().BeTrue($"{step}");
        }

        return project;
    }

    private static (Account Retention, Account Payable) AccountsFor(Project project, Guid subcontractorId)
    {
        Account retention = Account.Create(
            AccountType.SubcontractorRetention, $"RET-{subcontractorId:N}"[..20], "x", "Retention", Currency.Egp,
            new DateOnly(2026, 1, 1), project.Id, PartyType.Subcontractor, subcontractorId).Value;
        Account payable = Account.Create(
            AccountType.SubcontractorPayable, $"PAY-{subcontractorId:N}"[..20], "x", "Payable", Currency.Egp,
            new DateOnly(2026, 1, 1), project.Id, PartyType.Subcontractor, subcontractorId).Value;
        return (retention, payable);
    }

    private static Result<PostingSet> Release(
        Project project, Guid subcontractorId, Account? retention, Account? payable, decimal retained, DateOnly today) =>
        SubcontractorRetention.Release(
            project, subcontractorId, retention, payable, new Money(retained), Actor, Now, today, Policy);

    [Fact]
    public void The_release_is_the_whole_retained_balance_from_payable_to_retention_and_never_the_hold()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);

        PostingSet set = Release(project, sub, retention, payable, 37_500m, project.WarrantyEndsOn!.Value).Value;

        Posting posting = set.Postings.Should().ContainSingle().Subject;
        posting.Type.Should().Be(PostingType.SubcontractorRetentionRelease);
        posting.Amount.Should().Be(new Money(37_500m));
        posting.FromAccountId.Should().Be(payable.Id);
        posting.ToAccountId.Should().Be(retention.Id);
        posting.ReversesId.Should().BeNull();
        retention.LedgerKind.Should().BeNull("the subcontractor's retention is none of the five ledgers");
        retention.Type.Should().NotBe(AccountType.Hold);
        set.Accounts.Values.Should().NotContain(account => account.Type == AccountType.Hold);
    }

    [Fact]
    public void The_retention_balance_reads_zero_after_the_release_and_a_second_release_finds_nothing()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        var ledger = new List<Posting>();
        decimal[] accruals = [10_000m, 12_500m, 15_000m];
        foreach (decimal part in accruals)
        {
            ledger.Add(Posting.Create(
                retention, payable, new Money(part), PostingType.SubcontractorRetentionAccrual,
                new SourceDocument(SourceDocumentType.SubcontractorExtract, Guid.NewGuid(), "SX"),
                HandoverDay, Actor, Now, project.Id).Value);
        }

        Money Retained() => new(
            ledger.Where(posting => posting.FromAccountId == retention.Id).Sum(posting => posting.Amount.Amount)
            - ledger.Where(posting => posting.ToAccountId == retention.Id).Sum(posting => posting.Amount.Amount));

        Retained().Should().Be(new Money(37_500m), "credit-normal: the three accruals summed");

        PostingSet set = Release(project, sub, retention, payable, Retained().Amount, project.WarrantyEndsOn!.Value).Value;
        ledger.AddRange(set.Postings);

        Retained().Should().Be(Money.Zero);
        Release(project, sub, retention, payable, Retained().Amount, project.WarrantyEndsOn!.Value).Error
            .Should().Be(ClosureErrors.NothingToRelease);
    }

    [Fact]
    public void The_release_is_refused_the_day_before_the_warranty_ends_and_allowed_on_the_day()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        DateOnly ends = project.WarrantyEndsOn!.Value;

        Release(project, sub, retention, payable, 1_000m, ends.AddDays(-1)).Error.Should().Be(ClosureErrors.WarrantyNotEnded);
        Release(project, sub, retention, payable, 1_000m, ends).IsSuccess.Should().BeTrue();
        Release(project, sub, retention, payable, 1_000m, ends.AddDays(30)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.HandoverPending)]
    [InlineData(ProjectStatus.Handover)]
    [InlineData(ProjectStatus.UnderWarranty)]
    public void The_release_follows_the_ruled_project_state_table(ProjectStatus status)
    {
        Project project = ProjectIn(status);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);

        // Whatever the clock says, the ruled table is asked first, so the state refusal does not depend on the date.
        Result<PostingSet> result = Release(project, sub, retention, payable, 1_000m, new DateOnly(2030, 1, 1));

        result.IsSuccess.Should().Be(Policy.Allows(status, PostingType.SubcontractorRetentionRelease), $"in {status}");
        result.IsSuccess.Should().Be(status == ProjectStatus.UnderWarranty);
    }

    [Fact]
    public void A_subcontractor_with_nothing_retained_posts_nothing()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);

        Release(project, sub, retention, payable, 0m, project.WarrantyEndsOn!.Value).Error.Should().Be(ClosureErrors.NothingToRelease);
    }

    [Fact]
    public void Another_subcontractors_accounts_or_missing_accounts_are_refused()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        (Account otherRetention, Account otherPayable) = AccountsFor(project, Guid.CreateVersion7());
        DateOnly ends = project.WarrantyEndsOn!.Value;

        Release(project, sub, null, payable, 1_000m, ends).Error.Should().Be(ClosureErrors.AccountsMissing);
        Release(project, sub, retention, null, 1_000m, ends).Error.Should().Be(ClosureErrors.AccountsMissing);
        Release(project, sub, otherRetention, payable, 1_000m, ends).Error.Should().Be(ClosureErrors.AccountsMissing);
        Release(project, sub, retention, otherPayable, 1_000m, ends).Error.Should().Be(ClosureErrors.AccountsMissing);
    }

    [Fact]
    public void A_mistaken_release_is_corrected_by_a_reversing_posting_through_ReversesId()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        Posting original = Release(project, sub, retention, payable, 5_000m, project.WarrantyEndsOn!.Value).Value.Postings.Single();

        Posting mirror = Posting.Reverse(original, payable, retention, project.WarrantyEndsOn!.Value, Actor, Now).Value;

        mirror.ReversesId.Should().Be(original.Id);
        mirror.Type.Should().Be(PostingType.SubcontractorRetentionRelease);
        mirror.FromAccountId.Should().Be(retention.Id);
        mirror.ToAccountId.Should().Be(payable.Id);
    }

    // ---- AC-806-E · asked by Finance, decided by another user ---------------------------------------

    private static readonly Guid Owner = Guid.CreateVersion7();

    private static Result<SubcontractorRetentionReleaseRequest> Ask(Project project, Guid sub, Account retention, Account payable, bool waiting = false) =>
        SubcontractorRetentionReleaseRequest.Request(
            project, sub, retention, payable, new Money(37_500m), waiting, Policy, Actor, Now, project.WarrantyEndsOn!.Value);

    private static Result<PostingSet> ApproveAs(
        SubcontractorRetentionReleaseRequest request, Project project, Account retention, Account payable, Guid actor, decimal retained) =>
        request.Approve(actor, project, retention, payable, new Money(retained), Policy, Now, project.WarrantyEndsOn!.Value);

    [Fact]
    public void A_request_posts_nothing_and_the_approval_by_another_user_writes_the_one_release_for_the_balance_read_then()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        SubcontractorRetentionReleaseRequest request = Ask(project, sub, retention, payable).Value;
        request.Status.Should().Be(HoldReleaseRequestStatus.Requested);

        PostingSet set = ApproveAs(request, project, retention, payable, Owner, 40_000m).Value;

        Posting posting = set.Postings.Should().ContainSingle().Subject;
        posting.Type.Should().Be(PostingType.SubcontractorRetentionRelease);
        posting.Amount.Should().Be(new Money(40_000m), "the amount is the retention read at approval, never one stored at the request");
        posting.CreatedByUserId.Should().Be(Owner);
        request.Status.Should().Be(HoldReleaseRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(Owner);
    }

    [Fact]
    public void The_user_who_asked_for_the_release_can_neither_approve_nor_reject_it()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        SubcontractorRetentionReleaseRequest request = Ask(project, sub, retention, payable).Value;

        ApproveAs(request, project, retention, payable, Actor, 37_500m).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        request.Reject(Actor, "no", Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        request.Status.Should().Be(HoldReleaseRequestStatus.Requested);
        request.DecidedByUserId.Should().BeNull();
    }

    [Fact]
    public void A_request_that_could_never_be_approved_is_refused_at_once_and_a_second_one_waits_behind_the_first()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);

        SubcontractorRetentionReleaseRequest.Request(
                project, sub, retention, payable, new Money(37_500m), false, Policy, Actor, Now, project.WarrantyEndsOn!.Value.AddDays(-1))
            .Error.Should().Be(ClosureErrors.WarrantyNotEnded);
        SubcontractorRetentionReleaseRequest.Request(
                project, sub, retention, payable, Money.Zero, false, Policy, Actor, Now, project.WarrantyEndsOn!.Value)
            .Error.Should().Be(ClosureErrors.NothingToRelease);
        Ask(project, sub, retention, payable, waiting: true).Error.Should().Be(ClosureErrors.RetentionReleaseAlreadyRequested);
    }

    [Fact]
    public void A_rejection_needs_a_reason_is_stored_and_ends_the_request()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        SubcontractorRetentionReleaseRequest request = Ask(project, sub, retention, payable).Value;

        request.Reject(Owner, "  ", Now).Error.Should().Be(ClosureErrors.ReasonRequired);
        request.Status.Should().Be(HoldReleaseRequestStatus.Requested);

        request.Reject(Owner, "snag open", Now).IsSuccess.Should().BeTrue();
        request.Status.Should().Be(HoldReleaseRequestStatus.Rejected);
        request.RejectionReason.Should().Be("snag open");
        ApproveAs(request, project, retention, payable, Owner, 37_500m).Error.Should().Be(ClosureErrors.RetentionReleaseNotRequested);
        request.Reject(Owner, "again", Now).Error.Should().Be(ClosureErrors.RetentionReleaseNotRequested);
    }

    // ---- AC-806-D · the Owner-approved reversal -------------------------------------------------------

    [Fact]
    public void A_reversal_request_needs_a_reason_a_standing_release_and_the_asker_cannot_approve_it()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        Guid sub = Guid.CreateVersion7();
        (Account retention, Account payable) = AccountsFor(project, sub);
        Posting original = Release(project, sub, retention, payable, 5_000m, project.WarrantyEndsOn!.Value).Value.Postings.Single();

        SubcontractorRetentionReversalRequest.Request(project, sub, original, false, false, " ", Actor, Now).Error
            .Should().Be(ClosureErrors.ReasonRequired);
        SubcontractorRetentionReversalRequest.Request(project, sub, null, false, false, "wrong", Actor, Now).Error
            .Should().Be(ClosureErrors.RetentionReleasePostingNotFound);
        SubcontractorRetentionReversalRequest.Request(project, Guid.CreateVersion7(), original, false, false, "wrong", Actor, Now).Error
            .Should().Be(ClosureErrors.RetentionReleasePostingNotFound, "another subcontractor's release is not this one's");
        SubcontractorRetentionReversalRequest.Request(project, sub, original, true, false, "wrong", Actor, Now).Error
            .Should().Be(ClosureErrors.RetentionReleaseAlreadyReversed);
        SubcontractorRetentionReversalRequest.Request(project, sub, original, false, true, "wrong", Actor, Now).Error
            .Should().Be(ClosureErrors.RetentionReversalAlreadyRequested);

        SubcontractorRetentionReversalRequest request =
            SubcontractorRetentionReversalRequest.Request(project, sub, original, false, false, "wrong", Actor, Now).Value;
        request.Approve(Actor, original, false, payable, retention, Now, DateOnly.FromDateTime(Now.UtcDateTime)).IsFailure
            .Should().BeTrue("the user who asked may not approve");
        request.Status.Should().Be(HoldReleaseRequestStatus.Requested);

        Posting mirror = request.Approve(Owner, original, false, payable, retention, Now, DateOnly.FromDateTime(Now.UtcDateTime)).Value;
        mirror.ReversesId.Should().Be(original.Id);
        mirror.CreatedAt.Should().Be(request.DecidedAt, "the database guard matches the two instants");
        request.Status.Should().Be(HoldReleaseRequestStatus.Approved);
    }
}
