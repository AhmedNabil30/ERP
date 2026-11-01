using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Treasury;

/// <summary>
/// The several postings one business document produces (an extract approval is about six), built so they
/// can be committed in a single save. Grouping is by the shared <see cref="SourceDocument"/>; there is no
/// batch entity (see <see cref="SourceDocument"/>).
/// </summary>
public sealed class PostingSet
{
    internal PostingSet(IReadOnlyList<Posting> postings, IReadOnlyDictionary<Guid, Account> accounts)
    {
        Postings = postings;
        Accounts = accounts;
    }

    public IReadOnlyList<Posting> Postings { get; }

    /// <summary>Every account the set touches, by id.</summary>
    public IReadOnlyDictionary<Guid, Account> Accounts { get; }

    /// <summary>Ids of every account the set touches. Feed to <see cref="BalanceQueries.GetSignedBalances"/>.</summary>
    public IReadOnlyCollection<Guid> TouchedAccountIds => [.. Accounts.Keys];

    /// <summary>
    /// What the set would do to each touched account, given the balances read now. A consequence
    /// statement, not a promise: the database decides at commit, and balances may move before then.
    /// </summary>
    /// <param name="currentSignedBalances">Signed balance by account id. A missing account counts as zero.</param>
    public PostingSetPreview Preview(IReadOnlyDictionary<Guid, Money> currentSignedBalances)
    {
        ArgumentNullException.ThrowIfNull(currentSignedBalances);

        var effects = new List<AccountEffect>();

        foreach ((Guid id, Account account) in Accounts.OrderBy(pair => pair.Key))
        {
            decimal raw = 0m;

            foreach (Posting posting in Postings)
            {
                if (posting.ToAccountId == id)
                {
                    raw += posting.Amount.Amount;
                }

                if (posting.FromAccountId == id)
                {
                    raw -= posting.Amount.Amount;
                }
            }

            Money delta = new(account.NormalBalance == NormalBalance.Debit ? raw : -raw);
            Money before = currentSignedBalances.TryGetValue(id, out Money known) ? known : Money.Zero;
            Money after = before + delta;

            effects.Add(new AccountEffect(
                id, account.Code, account.Type, before, delta, after, account.EnforceNonNegative, account.EnforceNonNegative && after.IsNegative));
        }

        return new PostingSetPreview(effects);
    }
}

/// <summary>One account's before and after under a <see cref="PostingSet"/>.</summary>
public sealed record AccountEffect(
    Guid AccountId,
    string AccountCode,
    AccountType AccountType,
    Money Before,
    Money Delta,
    Money After,
    bool HasFloor,
    bool WouldBreachFloor);

public sealed record PostingSetPreview(IReadOnlyList<AccountEffect> Effects)
{
    /// <summary>True when any floored account (safe, client advance, عهدة) would go negative.</summary>
    public bool WouldBreachFloor => Effects.Any(effect => effect.WouldBreachFloor);
}

/// <summary>
/// Collects the legs of one business document and validates them all before any is saved. Each leg goes
/// through <see cref="Posting.Create"/>, so every rule a single posting obeys applies to every leg; the
/// builder adds the project-state check, which <see cref="Posting"/> cannot see.
/// </summary>
public sealed class PostingSetBuilder
{
    private readonly SourceDocument _source;
    private readonly DateOnly _postingDate;
    private readonly Guid _createdByUserId;
    private readonly DateTimeOffset _createdAt;
    private readonly Project _project;
    private readonly PostingProjectStatePolicy _statePolicy;
    private readonly List<(Account From, Account To, Money Amount, PostingType Type)> _legs = [];

    public PostingSetBuilder(
        Project project,
        SourceDocument source,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        PostingProjectStatePolicy statePolicy)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(statePolicy);

        _project = project;
        _source = source;
        _postingDate = postingDate;
        _createdByUserId = createdByUserId;
        _createdAt = createdAt;
        _statePolicy = statePolicy;
    }

    public PostingSetBuilder Add(Account from, Account to, Money amount, PostingType type)
    {
        _legs.Add((from, to, amount, type));
        return this;
    }

    /// <summary>Adds the leg unless the amount is zero — an extract with no advance left to recover has no such leg.</summary>
    public PostingSetBuilder AddIfPositive(Account from, Account to, Money amount, PostingType type) =>
        amount.IsZero ? this : Add(from, to, amount, type);

    /// <summary>Validates every leg and returns the set, or the first failure. Nothing is saved.</summary>
    public Result<PostingSet> Build()
    {
        if (_legs.Count == 0)
        {
            return Result.Failure<PostingSet>(TreasuryErrors.PostingSetEmpty);
        }

        var postings = new List<Posting>(_legs.Count);
        var accounts = new Dictionary<Guid, Account>();

        foreach ((Account from, Account to, Money amount, PostingType type) in _legs)
        {
            Result state = PostingProjectStateLegality.Validate(_statePolicy, _project.Status, type, isReversal: false);
            if (state.IsFailure)
            {
                return Result.Failure<PostingSet>(state.Error);
            }

            Result<Posting> created = Posting.Create(
                from, to, amount, type, _source, _postingDate, _createdByUserId, _createdAt, _project.Id);
            if (created.IsFailure)
            {
                return Result.Failure<PostingSet>(created.Error);
            }

            postings.Add(created.Value);
            accounts[from.Id] = from;
            accounts[to.Id] = to;
        }

        return Result.Success(new PostingSet(postings, accounts));
    }
}
