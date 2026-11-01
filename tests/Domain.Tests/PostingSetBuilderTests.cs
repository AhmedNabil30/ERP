using System.Collections.Frozen;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// The multi-posting builder: the several postings one document produces, validated together before any
/// is saved. Drives spec.md §15's second extract through the rulings module, not through typed figures.
/// </summary>
public sealed class PostingSetBuilderTests
{
    private readonly RulingSet _rulings = RulingSet.Default;
    private readonly Project _project;
    private readonly Account _revenue;
    private readonly Account _receivable;
    private readonly Account _clientAdvance;
    private readonly Account _hold;
    private readonly Account _materialAdvance;
    private readonly Account _safe = TestAccounts.Safe();

    public PostingSetBuilderTests()
    {
        _project = Project.Create("PRJ-SET", "Set", TestAccounts.ClientId, ContractType.LumpSum, TestAccounts.Now).Value;
        _project.TransitionTo(ProjectStatus.Active, TestAccounts.Today).IsSuccess.Should().BeTrue();

        _revenue = Account.Create(
            AccountType.ContractRevenue, "PRJ-REV", "إيراد", "Revenue", Currency.Egp, TestAccounts.Today, _project.Id).Value;
        _receivable = TestAccounts.ClientReceivable(_project.Id);
        _clientAdvance = TestAccounts.ClientAdvance(_project.Id);
        _hold = TestAccounts.Hold(_project.Id);
        _materialAdvance = TestAccounts.MaterialAdvance(_project.Id);
    }

    private PostingSetBuilder NewBuilder(Project? project = null, RulingSet? rulings = null) =>
        new(
            project ?? _project,
            new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT-2"),
            TestAccounts.Today,
            TestAccounts.Actor,
            TestAccounts.Now,
            (rulings ?? _rulings).PostingProjectStates);

    /// <summary>Extract 2 of spec.md §15, every figure derived by a ruling.</summary>
    private PostingSetBuilder Extract2(PostingSetBuilder builder)
    {
        Money work = new(300_000m);
        Money hold = _rulings.DeductionRounding.Deduct(work, ContractDefaults.HoldRate);
        Money advance = _rulings.DeductionRounding.Deduct(work, ContractDefaults.AdvanceRecoveryRate);
        Money tashwinat = _rulings.MaterialAdvanceRecovery.RecoveryFor(new Money(60_000m), new Money(75_000m));
        Money net = _rulings.DeductionRounding.NetPayable(work, [hold, advance, tashwinat]);

        return builder
            .Add(_revenue, _receivable, work, PostingType.RevenueRecognition)
            .Add(_receivable, _hold, hold, PostingType.HoldAccrual)
            .Add(_receivable, _clientAdvance, advance, PostingType.ClientAdvanceRecovery)
            .Add(_receivable, _materialAdvance, tashwinat, PostingType.MaterialAdvanceRecovery)
            .Add(_receivable, _safe, net, PostingType.ClientCollection);
    }

    [Fact]
    public void Section_15_extract_2_builds_as_five_postings_that_share_one_source_document_and_net_the_receivable_to_zero()
    {
        Result<PostingSet> built = Extract2(NewBuilder()).Build();

        built.IsSuccess.Should().BeTrue(built.IsFailure ? built.Error.Code : string.Empty);
        PostingSet set = built.Value;

        set.Postings.Should().HaveCount(5);
        set.Postings.Select(posting => posting.SourceDocumentId).Distinct().Should().HaveCount(1);
        set.Postings.Should().OnlyContain(posting => posting.ProjectId == _project.Id);

        Money received = Money.Sum(set.Postings.Where(p => p.ToAccountId == _safe.Id).Select(p => p.Amount));
        received.Should().Be(new Money(120_000m), "spec.md section 15: the client pays 120,000 on extract 2");

        PostingSetPreview preview = set.Preview(new Dictionary<Guid, Money>());
        preview.Effects.Single(effect => effect.AccountId == _receivable.Id).Delta
            .Should().Be(Money.Zero, "an extract's own postings net the receivable back to zero");
    }

    [Fact]
    public void One_bad_leg_fails_the_whole_set_so_nothing_half_commits()
    {
        PostingSetBuilder builder = NewBuilder()
            .Add(_revenue, _receivable, new Money(300_000m), PostingType.RevenueRecognition)
            // A hold account may only be touched by HoldAccrual / HoldRelease: this leg is illegal.
            .Add(_receivable, _hold, new Money(60_000m), PostingType.ClientCollection);

        Result<PostingSet> built = builder.Build();

        built.IsFailure.Should().BeTrue();
        built.Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
    }

    [Fact]
    public void An_empty_set_is_refused()
    {
        NewBuilder().Build().Error.Should().Be(TreasuryErrors.PostingSetEmpty);
    }

    [Fact]
    public void Add_if_positive_skips_a_zero_leg_such_as_an_advance_that_is_already_fully_recovered()
    {
        Result<PostingSet> built = NewBuilder()
            .Add(_revenue, _receivable, new Money(100m), PostingType.RevenueRecognition)
            .AddIfPositive(_receivable, _clientAdvance, Money.Zero, PostingType.ClientAdvanceRecovery)
            .Build();

        built.Value.Postings.Should().HaveCount(1);
    }

    [Fact]
    public void A_stopped_project_refuses_the_extract_postings_through_the_builder()
    {
        _project.TransitionTo(ProjectStatus.Stopped, TestAccounts.Today, "site closed by the authority").IsSuccess
            .Should().BeTrue();

        Result<PostingSet> built = Extract2(NewBuilder()).Build();

        built.Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);
    }

    [Fact]
    public void Ruling_the_state_table_differently_changes_the_builder_with_no_code_change()
    {
        _project.TransitionTo(ProjectStatus.Stopped, TestAccounts.Today, "stop").IsSuccess.Should().BeTrue();

        var everythingAllowed = new PostingProjectStatePolicy(
            Enum.GetValues<ProjectStatus>().ToDictionary(
                status => status,
                _ => PostingTypes.AllDefined.ToFrozenSet()),
            reversalsFollowTheTable: false);

        Result<PostingSet> built = Extract2(NewBuilder(rulings: _rulings with { PostingProjectStates = everythingAllowed })).Build();

        built.IsSuccess.Should().BeTrue();
    }

    // ---- the dry run ---------------------------------------------------------------------------------

    [Fact]
    public void The_preview_shows_before_and_after_for_each_touched_account_in_its_own_normal_direction()
    {
        PostingSet set = Extract2(NewBuilder()).Build().Value;

        var current = new Dictionary<Guid, Money>
        {
            [_clientAdvance.Id] = new Money(250_000m),
            [_materialAdvance.Id] = new Money(75_000m),
            [_hold.Id] = new Money(60_000m),
        };

        PostingSetPreview preview = set.Preview(current);

        AccountEffect advance = preview.Effects.Single(effect => effect.AccountId == _clientAdvance.Id);
        advance.Before.Should().Be(new Money(250_000m));
        advance.After.Should().Be(new Money(175_000m), "the advance ledger is credit-normal and 75,000 is recovered");

        preview.Effects.Single(effect => effect.AccountId == _hold.Id).After.Should().Be(new Money(120_000m));
        preview.Effects.Single(effect => effect.AccountId == _materialAdvance.Id).After.Should().Be(new Money(30_000m));
        preview.WouldBreachFloor.Should().BeFalse();
    }

    [Fact]
    public void The_preview_flags_a_floored_account_that_would_go_negative_without_deciding_for_the_database()
    {
        PostingSet set = Extract2(NewBuilder()).Build().Value;

        // Only 50,000 of advance left, 75,000 to recover: the client advance floor would be breached.
        var current = new Dictionary<Guid, Money> { [_clientAdvance.Id] = new Money(50_000m) };

        PostingSetPreview preview = set.Preview(current);

        preview.WouldBreachFloor.Should().BeTrue();
        preview.Effects.Single(effect => effect.WouldBreachFloor).AccountId.Should().Be(_clientAdvance.Id);
    }
}
