using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-511 and KAFF-512 in the domain. The two stories share one shape: a dispute that resolves as a credit note
/// raises an <see cref="Adjustment"/>, and the adjustment is a new posting, never an edit and never the hold.
/// The stage each posting is written at is asserted, not only the totals.
/// </summary>
public sealed class AdjustmentTests
{
    private static readonly DateTimeOffset Now = ExtractHarness.Now;
    private static readonly DateOnly Date = new(2026, 2, 1);

    private static readonly Dictionary<ProjectStatus, ProjectStatus[]> FromActive = new()
    {
        [ProjectStatus.Active] = [],
        [ProjectStatus.HandoverPending] = [ProjectStatus.HandoverPending],
        [ProjectStatus.Handover] = [ProjectStatus.HandoverPending, ProjectStatus.Handover],
        [ProjectStatus.UnderWarranty] = [ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty],
        [ProjectStatus.Closed] =
            [ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty, ProjectStatus.Closed],
        [ProjectStatus.Stopped] = [ProjectStatus.Stopped],
        [ProjectStatus.Terminated] = [ProjectStatus.Terminated],
    };

    public static TheoryData<ProjectStatus> StatesAfterActive() => [.. FromActive.Keys];

    private static Result<Adjustment> Raise(
        ExtractHarness h, Extract extract, decimal amount, string? reason = "client disputes", Account? target = null,
        RulingSet? rulings = null, Guid? actor = null) =>
        Adjustment.RaiseCreditNote(
            h.Project, 1, extract, target ?? h.Revenue, new Money(amount), reason,
            rulings ?? h.Rulings, actor ?? ExtractHarness.NewUser(), Now);

    // ---- the dispute state machine: every legal move and every illegal one ----

    [Fact]
    public async Task A_dispute_is_raised_on_an_issued_extract_and_on_no_other_state()
    {
        foreach (ExtractStatus status in Enum.GetValues<ExtractStatus>().Where(status => status != ExtractStatus.Void))
        {
            var h = new ExtractHarness();
            Extract extract = await h.DriveAsync(status);
            int history = extract.GateHistory.Count;

            Result raised = extract.RaiseDispute(ExtractHarness.NewUser(), "quantities are wrong", Now);

            if (status == ExtractStatus.Issued)
            {
                raised.IsSuccess.Should().BeTrue();
                extract.Status.Should().Be(ExtractStatus.Disputed);
                extract.GateHistory.Count.Should().Be(history + 1);
                extract.GateHistory[^1].Gate.Should().Be(ExtractGate.Dispute);
                extract.GateHistory[^1].Reason.Should().Be("quantities are wrong");
            }
            else
            {
                raised.Error.Should().Be(ExtractErrors.IllegalTransition, $"from {status}");
                extract.Status.Should().Be(status, "a refused dispute changes nothing");
                extract.GateHistory.Count.Should().Be(history);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_dispute_without_a_reason_is_refused_and_changes_nothing(string? reason)
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        extract.RaiseDispute(ExtractHarness.NewUser(), reason, Now).Error.Should().Be(ExtractErrors.ReasonRequired);

        extract.Status.Should().Be(ExtractStatus.Issued);
    }

    [Fact]
    public async Task Raising_a_dispute_writes_no_posting_and_leaves_the_hold_and_the_receivable_as_they_were()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Money hold = h.Balance(h.Hold);
        Money receivable = h.Balance(h.Receivable);
        int postings = h.Ledger.Count;

        extract.RaiseDispute(ExtractHarness.NewUser(), "disputed", Now).IsSuccess.Should().BeTrue();

        h.Ledger.Count.Should().Be(postings, "the extract's postings stand while it is disputed");
        h.Balance(h.Hold).Should().Be(hold).And.Be(new Money(60_000m));
        h.Balance(h.Receivable).Should().Be(receivable);
    }

    [Fact]
    public async Task A_disputed_extract_cannot_be_collected_issued_again_or_rejected()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Disputed);
        int history = extract.GateHistory.Count;

        extract.Collect(ExtractHarness.NewUser(), h.Context(), Now).Error.Should().Be(ExtractErrors.IllegalTransition);
        extract.Issue(ExtractHarness.NewUser(), h.Context(), Now).Error.Should().Be(ExtractErrors.IllegalTransition);
        extract.Reject(ExtractHarness.NewUser(), "no", ExtractRejectionKind.MaterialValues, h.Context(), [], Now).Error.Should().Be(ExtractErrors.IllegalTransition);

        extract.Status.Should().Be(ExtractStatus.Disputed);
        extract.GateHistory.Count.Should().Be(history);
        Extract.IsLegalTransition(ExtractStatus.Disputed, ExtractStatus.Issued)
            .Should().BeFalse("a way back in the table would make Issue legal from Disputed");
    }

    [Fact]
    public async Task Resolving_as_a_credit_note_returns_the_extract_to_issued_with_a_stored_reason_and_only_from_disputed()
    {
        foreach (ExtractStatus status in Enum.GetValues<ExtractStatus>().Where(status => status != ExtractStatus.Void))
        {
            var h = new ExtractHarness();
            Extract extract = await h.DriveAsync(status);
            int history = extract.GateHistory.Count;
            Guid actor = ExtractHarness.NewUser();

            Result resolved = extract.ResolveDisputeAsCreditNote(actor, "agreed a credit", Now);

            if (status == ExtractStatus.Disputed)
            {
                resolved.IsSuccess.Should().BeTrue();
                extract.Status.Should().Be(ExtractStatus.Issued);
                extract.GateHistory[^1].Gate.Should().Be(ExtractGate.DisputeResolution);
                extract.GateHistory[^1].Reason.Should().Be("agreed a credit");
                extract.DisputeActors().Should().Contain(actor);
            }
            else
            {
                resolved.Error.Should().Be(ExtractErrors.IllegalTransition, $"from {status}");
                extract.Status.Should().Be(status);
                extract.GateHistory.Count.Should().Be(history);
            }
        }
    }

    [Fact]
    public async Task A_resolution_without_a_reason_is_refused_and_the_extract_stays_disputed()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Disputed);

        extract.ResolveDisputeAsCreditNote(ExtractHarness.NewUser(), " ", Now).Error.Should().Be(ExtractErrors.ReasonRequired);

        extract.Status.Should().Be(ExtractStatus.Disputed);
    }

    // ---- the credit note: one new posting, to revenue, never the hold ----

    [Fact]
    public async Task A_credit_note_is_raised_with_all_four_fields_and_posts_nothing_until_it_is_approved()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        int postings = h.Ledger.Count;

        Adjustment note = Raise(h, extract, 10_000m).Value;

        note.Type.Should().Be(AdjustmentType.ClientCreditNote);
        note.Reason.Should().Be("client disputes");
        note.SourceDocument.Should().Be(extract.SourceDocument);
        note.TargetAccountId.Should().Be(h.Revenue.Id);
        note.Amount.Should().Be(new Money(10_000m));
        note.Status.Should().Be(AdjustmentStatus.Raised);
        h.Ledger.Count.Should().Be(postings, "raising posts nothing");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_credit_note_without_a_reason_is_refused(string? reason)
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        Raise(h, extract, 1_000m, reason).Error.Should().Be(AdjustmentErrors.ReasonRequired);
    }

    [Fact]
    public async Task A_credit_note_without_a_target_account_is_refused()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        Adjustment.RaiseCreditNote(
                h.Project, 1, extract, null, new Money(1_000m), "x", h.Rulings, ExtractHarness.NewUser(), Now)
            .Error.Should().Be(AdjustmentErrors.TargetAccountRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10_000)]
    public async Task A_zero_or_negative_adjustment_is_refused_because_the_direction_is_the_types_not_the_sign(int amount)
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        Raise(h, extract, amount).Error.Should().Be(AdjustmentErrors.AmountMustBePositive);
    }

    /// <summary>AC-512-D. The hold is named as the target and the attempt is refused in the domain, with the hold untouched.</summary>
    [Fact]
    public async Task An_adjustment_that_names_the_hold_is_refused_and_the_hold_balance_is_unchanged()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Money hold = h.Balance(h.Hold);
        int postings = h.Ledger.Count;

        Raise(h, extract, 5_000m, target: h.Hold).Error.Should().Be(AdjustmentErrors.HoldUntouchable);

        h.Balance(h.Hold).Should().Be(hold).And.Be(new Money(60_000m));
        h.Ledger.Count.Should().Be(postings);
    }

    /// <summary>
    /// The second lock on the same door: even if an adjustment reached the posting layer with the hold on either side, a
    /// CreditNote cannot debit it. The account-pair table and the direction check each refuse it on their own.
    /// </summary>
    [Fact]
    public async Task A_credit_note_posting_cannot_leave_or_enter_the_hold_even_past_the_adjustment()
    {
        var h = new ExtractHarness();
        await h.DriveAsync(ExtractStatus.Issued);
        var source = new SourceDocument(SourceDocumentType.Adjustment, Guid.NewGuid(), "ADJ-1");

        Posting.Create(h.Hold, h.Revenue, new Money(1m), PostingType.CreditNote, source, Date, ExtractHarness.NewUser(), Now, h.Project.Id)
            .Error.Should().Be(TreasuryErrors.HoldOnlyGrows, "a CreditNote cannot take value out of the hold");
        Posting.Create(h.Receivable, h.Hold, new Money(1m), PostingType.CreditNote, source, Date, ExtractHarness.NewUser(), Now, h.Project.Id)
            .Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch, "only HoldAccrual and HoldRelease touch the hold");
    }

    [Fact]
    public async Task A_credit_note_may_target_only_the_projects_own_contract_revenue_account()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        Raise(h, extract, 1_000m, target: h.Receivable).Error.Should().Be(AdjustmentErrors.TargetAccountNotAllowed);
        Raise(h, extract, 1_000m, target: h.ClientAdvance).Error.Should().Be(AdjustmentErrors.TargetAccountNotAllowed);
        Raise(h, extract, 1_000m, target: h.Safe).Error.Should().Be(AdjustmentErrors.TargetAccountNotAllowed);

        Account otherProjectRevenue = Account.Create(
            AccountType.ContractRevenue, "OTHER-REV", "x", "Other revenue", Currency.Egp, new DateOnly(2026, 1, 1),
            TestAccounts.OtherProjectId).Value;
        Raise(h, extract, 1_000m, target: otherProjectRevenue).Error.Should().Be(AdjustmentErrors.TargetAccountNotAllowed);
    }

    [Fact]
    public async Task A_credit_note_is_raised_against_an_issued_or_disputed_extract_and_no_other_state()
    {
        foreach (ExtractStatus status in new[]
        {
            ExtractStatus.Draft, ExtractStatus.QcSubmitted, ExtractStatus.DeductionsApplied, ExtractStatus.OwnerApproved,
            ExtractStatus.Issued, ExtractStatus.Disputed, ExtractStatus.Collected,
        })
        {
            var h = new ExtractHarness();
            Extract extract = await h.DriveAsync(status);

            Result<Adjustment> raised = Raise(h, extract, 1_000m);

            raised.IsSuccess.Should().Be(status is ExtractStatus.Issued or ExtractStatus.Disputed, $"against {status}");
            if (raised.IsFailure)
            {
                raised.Error.Should().Be(AdjustmentErrors.SourceNotCreditable, status.ToString());
            }
        }
    }

    /// <summary>D-239 item 2: a credit note has no cap. Neither its own amount nor earlier notes are measured against the net.</summary>
    [Fact]
    public async Task A_credit_note_is_not_capped_by_the_extracts_net_payable()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Money net = extract.NetPayable!.Value;

        Raise(h, extract, net.Amount).IsSuccess.Should().BeTrue("the whole net may be credited");
        Raise(h, extract, net.Amount + 1_000m).IsSuccess.Should().BeTrue("D-239 item 2 removes the cap");
    }

    /// <summary>AC-512-F. Acceptance in each project state matches the ruled table, in the same way as every posting.</summary>
    [Theory]
    [MemberData(nameof(StatesAfterActive))]
    public async Task The_project_state_table_decides_whether_a_credit_note_is_accepted(ProjectStatus status)
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        foreach (ProjectStatus step in FromActive[status])
        {
            h.Project.TransitionTo(step, Date, "test").IsSuccess.Should().BeTrue($"{step}");
        }

        Raise(h, extract, 1_000m).IsSuccess.Should().Be(
            h.Rulings.PostingProjectStates.Allows(status, PostingType.CreditNote), $"raising in {status}");
    }

    // ---- approval: the Owner, a different person, and the posting is written then ----

    [Fact]
    public async Task Approving_writes_one_new_credit_note_posting_from_the_receivable_to_revenue_and_edits_nothing()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Adjustment note = Raise(h, extract, 25_000m).Value;
        string[] before = [.. h.Ledger.Select(Snapshot)];
        Money hold = h.Balance(h.Hold);
        Money receivable = h.Balance(h.Receivable);
        Guid owner = ExtractHarness.NewUser();

        Result<PostingSet> approved = note.Approve(owner, [], h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date);

        approved.IsSuccess.Should().BeTrue();
        Posting posting = approved.Value.Postings.Should().ContainSingle().Subject;
        posting.Type.Should().Be(PostingType.CreditNote);
        posting.FromAccountId.Should().Be(h.Receivable.Id);
        posting.ToAccountId.Should().Be(h.Revenue.Id);
        posting.Amount.Should().Be(new Money(25_000m));
        posting.ReversesId.Should().BeNull("a credit note is a new posting, not a reversal of the extract's");
        posting.SourceDocument.Type.Should().Be(SourceDocumentType.Adjustment);
        posting.SourceDocument.Id.Should().Be(note.Id);
        note.Status.Should().Be(AdjustmentStatus.Approved);

        h.Commit(approved.Value);
        h.Balance(h.Receivable).Should().Be(receivable - new Money(25_000m));
        h.Balance(h.Hold).Should().Be(hold, "the hold is untouched by a credit note");
        h.Ledger.Take(before.Length).Select(Snapshot).Should().Equal(before, "every earlier posting is byte-for-byte unchanged");
    }

    [Fact]
    public async Task Nobody_approves_their_own_adjustment_and_nobody_who_handled_the_dispute_does_either()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Guid disputeRaiser = ExtractHarness.NewUser();
        Guid resolver = ExtractHarness.NewUser();
        extract.RaiseDispute(disputeRaiser, "disputed", Now).IsSuccess.Should().BeTrue();
        Adjustment note = Raise(h, extract, 5_000m, actor: resolver).Value;
        extract.ResolveDisputeAsCreditNote(resolver, "credit agreed", Now).IsSuccess.Should().BeTrue();
        IReadOnlyCollection<Guid> disputeActors = extract.DisputeActors();

        foreach (Guid refused in new[] { resolver, disputeRaiser })
        {
            note.Approve(refused, disputeActors, h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date)
                .Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
            note.Reject(refused, disputeActors, "no", Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        }

        note.Status.Should().Be(AdjustmentStatus.Raised, "the refused attempts changed nothing");
        note.Approve(ExtractHarness.NewUser(), disputeActors, h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Approval_is_refused_when_the_projects_state_no_longer_accepts_a_credit_note()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Adjustment note = Raise(h, extract, 5_000m).Value;
        var refusing = new PostingProjectStatePolicy(new Dictionary<ProjectStatus, System.Collections.Frozen.FrozenSet<PostingType>>(), false);

        note.Approve(ExtractHarness.NewUser(), [], h.Project, h.Receivable, h.Revenue, h.Rulings with { PostingProjectStates = refusing }, Now, Date)
            .Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);

        note.Status.Should().Be(AdjustmentStatus.Raised);
    }

    [Fact]
    public async Task Rejecting_stores_a_non_blank_reason_posts_nothing_and_cannot_be_approved_afterwards()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Adjustment note = Raise(h, extract, 5_000m).Value;
        Guid owner = ExtractHarness.NewUser();

        note.Reject(owner, [], "  ", Now).Error.Should().Be(AdjustmentErrors.ReasonRequired);
        note.Status.Should().Be(AdjustmentStatus.Raised);

        note.Reject(owner, [], "amount not agreed", Now).IsSuccess.Should().BeTrue();
        note.Status.Should().Be(AdjustmentStatus.Rejected);
        note.DecisionReason.Should().Be("amount not agreed");
        note.DecidedByUserId.Should().Be(owner);

        note.Approve(ExtractHarness.NewUser(), [], h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date)
            .Error.Should().Be(AdjustmentErrors.IllegalTransition);
    }

    // ---- the adjustment state machine ----

    [Fact]
    public void The_adjustment_table_holds_exactly_the_legal_moves_and_no_other_of_the_sixteen_pairs()
    {
        (AdjustmentStatus, AdjustmentStatus)[] legal =
        [
            (AdjustmentStatus.Raised, AdjustmentStatus.Approved),
            (AdjustmentStatus.Raised, AdjustmentStatus.Rejected),
            (AdjustmentStatus.Approved, AdjustmentStatus.Reversed),
        ];

        foreach (AdjustmentStatus from in Enum.GetValues<AdjustmentStatus>())
        {
            foreach (AdjustmentStatus to in Enum.GetValues<AdjustmentStatus>())
            {
                Adjustment.IsLegalTransition(from, to).Should().Be(legal.Contains((from, to)), $"{from} to {to}");
            }
        }
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Reverse")]
    public async Task Each_step_is_legal_from_its_own_state_and_refused_untouched_from_every_other(string step)
    {
        AdjustmentStatus[] legalFrom = step switch
        {
            "Approve" or "Reject" => [AdjustmentStatus.Raised],
            _ => [AdjustmentStatus.Approved],
        };

        foreach (AdjustmentStatus status in Enum.GetValues<AdjustmentStatus>())
        {
            var h = new ExtractHarness();
            Extract extract = await h.DriveAsync(ExtractStatus.Issued);
            (Adjustment note, List<Posting> written) = await InAsync(h, extract, status);
            Guid actor = ExtractHarness.NewUser();

            Result result = step switch
            {
                "Approve" => note.Approve(actor, [], h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date).IsSuccess
                    ? Result.Success()
                    : Result.Failure(AdjustmentErrors.IllegalTransition),
                "Reject" => note.Reject(actor, [], "no", Now),
                _ => note.Reverse(actor, [], "mistake", [.. written.Take(1).Select(posting => (posting, h.Receivable, h.Revenue))], Now, Date).IsSuccess
                    ? Result.Success()
                    : Result.Failure(AdjustmentErrors.IllegalTransition),
            };

            if (legalFrom.Contains(status))
            {
                result.IsSuccess.Should().BeTrue($"{step} is legal from {status}");
            }
            else
            {
                result.IsFailure.Should().BeTrue($"{step} is illegal from {status}");
                note.Status.Should().Be(status, "a refused step changes nothing");
            }
        }
    }

    // ---- correction: mirror postings through ReversesId ----

    [Fact]
    public async Task Reversing_an_approved_adjustment_mirrors_its_posting_through_ReversesId_and_leaves_the_original_unchanged()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        (Adjustment note, List<Posting> written) = await InAsync(h, extract, AdjustmentStatus.Approved);
        Posting original = written.Single();
        string snapshot = Snapshot(original);
        Money receivableAfterApproval = h.Balance(h.Receivable);
        Guid reverser = ExtractHarness.NewUser();

        Result<IReadOnlyList<Posting>> reversed = note.Reverse(
            reverser, [], "wrong amount", [(original, h.Receivable, h.Revenue)], Now, Date);

        reversed.IsSuccess.Should().BeTrue();
        Posting mirror = reversed.Value.Should().ContainSingle().Subject;
        mirror.ReversesId.Should().Be(original.Id);
        mirror.FromAccountId.Should().Be(original.ToAccountId);
        mirror.ToAccountId.Should().Be(original.FromAccountId);
        mirror.Amount.Should().Be(original.Amount);
        mirror.Type.Should().Be(PostingType.CreditNote);
        Snapshot(original).Should().Be(snapshot, "the original posting is never edited");
        note.Status.Should().Be(AdjustmentStatus.Reversed);
        note.DecisionReason.Should().Be("wrong amount");

        h.Ledger.Add(mirror);
        h.Balance(h.Receivable).Should().Be(receivableAfterApproval + original.Amount, "the correction restores the receivable");
        h.Balance(h.Hold).Should().Be(new Money(60_000m));
    }

    [Fact]
    public async Task Reversal_needs_a_reason_a_different_actor_and_an_approved_adjustment()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        (Adjustment note, List<Posting> written) = await InAsync(h, extract, AdjustmentStatus.Approved);
        (Posting, Account, Account)[] legs = [(written.Single(), h.Receivable, h.Revenue)];

        note.Reverse(ExtractHarness.NewUser(), [], " ", legs, Now, Date).Error.Should().Be(AdjustmentErrors.ReasonRequired);
        note.Reverse(note.RaisedByUserId, [], "x", legs, Now, Date).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        note.Reverse(ExtractHarness.NewUser(), [], "x", [], Now, Date).Error.Should().Be(AdjustmentErrors.ReversalPostingsRequired);
        note.Status.Should().Be(AdjustmentStatus.Approved);

        Adjustment raisedOnly = Raise(h, extract, 1_000m).Value;
        raisedOnly.Reverse(ExtractHarness.NewUser(), [], "x", legs, Now, Date).Error.Should().Be(AdjustmentErrors.IllegalTransition);
    }

    /// <summary>D-239 item 3: a credit note on a Collected extract cannot be reversed; the extract is never reopened by it.</summary>
    [Fact]
    public async Task A_credit_note_on_a_collected_extract_cannot_be_reversed()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        (Adjustment note, List<Posting> written) = await InAsync(h, extract, AdjustmentStatus.Approved);
        (Posting, Account, Account)[] legs = [(written.Single(), h.Receivable, h.Revenue)];

        note.Reverse(ExtractHarness.NewUser(), [], "x", legs, Now, Date, sourceExtractCollected: true)
            .Error.Should().Be(AdjustmentErrors.ExtractCollected);
        note.Status.Should().Be(AdjustmentStatus.Approved);
        note.Reverse(ExtractHarness.NewUser(), [], "x", legs, Now, Date, sourceExtractCollected: false).IsSuccess.Should().BeTrue();
    }

    // ---- the types that are not built ----

    [Fact]
    public void Every_type_but_the_client_credit_note_is_refused_and_a_termination_settlement_by_its_own_error()
    {
        Adjustment.RefusalFor(AdjustmentType.SubcontractorDebitNote).Should().Be(AdjustmentErrors.TypeNotBuilt);
        Adjustment.RefusalFor(AdjustmentType.DesignCredit).Should().Be(AdjustmentErrors.TypeNotBuilt);
        Adjustment.RefusalFor(AdjustmentType.TerminationSettlement).Should().Be(AdjustmentErrors.TerminationSettlementUnruled);
    }

    // ---- helpers ----

    /// <summary>
    /// The check the adjustment read asks for its action flags is the one each decision makes: the state machine, separation
    /// of duties over the raiser and the dispute's actors, and D-239 item 3 for a reversal.
    /// </summary>
    [Fact]
    public async Task The_decision_check_the_read_asks_is_the_one_approve_reject_and_reverse_make()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Guid raiser = ExtractHarness.NewUser();
        Guid disputer = ExtractHarness.NewUser();
        Guid owner = ExtractHarness.NewUser();
        Adjustment note = Raise(h, extract, 10_000m, actor: raiser).Value;

        note.EnsureMayDecide(AdjustmentStatus.Approved, owner, [disputer]).IsSuccess.Should().BeTrue();
        note.EnsureMayDecide(AdjustmentStatus.Rejected, owner, [disputer]).IsSuccess.Should().BeTrue();
        note.EnsureMayDecide(AdjustmentStatus.Reversed, owner, [disputer]).Error.Should().Be(AdjustmentErrors.IllegalTransition);
        note.EnsureMayDecide(AdjustmentStatus.Approved, raiser, []).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        note.EnsureMayDecide(AdjustmentStatus.Rejected, disputer, [disputer]).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);

        note.Approve(owner, [disputer], h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date).IsSuccess.Should().BeTrue();

        note.EnsureMayDecide(AdjustmentStatus.Approved, owner, []).Error.Should().Be(AdjustmentErrors.IllegalTransition);
        note.EnsureMayDecide(AdjustmentStatus.Reversed, owner, []).IsSuccess.Should().BeTrue();
        note.EnsureMayDecide(AdjustmentStatus.Reversed, owner, [], sourceExtractCollected: true).Error.Should().Be(AdjustmentErrors.ExtractCollected);
        note.EnsureMayDecide(AdjustmentStatus.Reversed, raiser, []).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
    }

    /// <summary>An adjustment taken to <paramref name="status"/>; the postings it wrote, if any, are committed to the ledger.</summary>
    private static Task<(Adjustment Note, List<Posting> Written)> InAsync(ExtractHarness h, Extract extract, AdjustmentStatus status)
    {
        Adjustment note = Raise(h, extract, 10_000m).Value;
        var written = new List<Posting>();
        if (status is AdjustmentStatus.Approved or AdjustmentStatus.Reversed)
        {
            PostingSet set = note.Approve(ExtractHarness.NewUser(), [], h.Project, h.Receivable, h.Revenue, h.Rulings, Now, Date).Value;
            h.Commit(set);
            written.AddRange(set.Postings);
        }

        if (status == AdjustmentStatus.Reversed)
        {
            Posting original = written.Single();
            note.Reverse(ExtractHarness.NewUser(), [], "mistake", [(original, h.Receivable, h.Revenue)], Now, Date).IsSuccess
                .Should().BeTrue();
        }

        if (status == AdjustmentStatus.Rejected)
        {
            note.Reject(ExtractHarness.NewUser(), [], "no", Now).IsSuccess.Should().BeTrue();
        }

        return Task.FromResult((note, written));
    }

    private static string Snapshot(Posting posting) =>
        $"{posting.Id}|{posting.FromAccountId}|{posting.ToAccountId}|{posting.Amount.Amount}|{posting.Type}|{posting.ReversesId}|{posting.PostingDate}|{posting.CreatedByUserId}";
}
