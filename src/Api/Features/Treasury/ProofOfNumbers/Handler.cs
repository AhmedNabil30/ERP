using System.Globalization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Treasury.ProofOfNumbers;

/// <summary>
/// Builds the actual side of إثبات الأرقام from the project's postings, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>The expected side is never derived from the actual side.</b> The per-event expectation has to come
/// from <c>IBillingCalculator</c>, and every calculator still returns <c>CalculatorNotImplemented</c>,
/// so <see cref="Response.ExpectedState"/> says so and no event carries an expectation. Fitting a rule to the numbers
/// that were posted would make every row match by construction and prove nothing.
/// </para>
/// <para>
/// The checks that need only the contract (hold is the hold rate times the contract value; client cash
/// equals the contract value) are expected figures that do not come from the postings, so they are live
/// today. Reversals count negatively in every column, so a corrected posting nets out and both stay in
/// the postings list.
/// </para>
/// <para>
/// <c>ponytail:</c> loads the project's postings into memory and groups them there. Fine at one
/// project's volume; move to SQL aggregation if a project's history ever outgrows memory.
/// </para>
/// </remarks>
internal static class Handler
{
    private static readonly string[] LedgerKeys =
        ["ClientAdvance", "Hold", "FirmAdvance", "PettyCashAdvance", "OwnerCurrentAccount", "MaterialAdvance"];

    private static readonly PostingType[] LedgerColumnTypes =
    [
        PostingType.HoldAccrual,
        PostingType.HoldRelease,
        PostingType.ClientAdvanceRecovery,
        PostingType.MaterialAdvanceIssue,
        PostingType.MaterialAdvanceRecovery,
    ];

    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Project? project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        List<Posting> postings = await database.Postings
            .Where(posting => posting.ProjectId == projectId)
            .OrderBy(posting => posting.PostingDate)
            .ThenBy(posting => posting.CreatedAt)
            .ThenBy(posting => posting.Id)
            .ToListAsync(cancellationToken);

        Guid[] accountIds =
        [
            .. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct(),
        ];
        Dictionary<Guid, Account> accounts = await database.Accounts
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        LedgerBalances balances = BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId);

        Percentage holdRate = project.HoldRate ?? ContractDefaults.HoldRate;
        Percentage advanceRecoveryRate = project.AdvanceRecoveryRate ?? ContractDefaults.AdvanceRecoveryRate;
        Money? contractValue = project.ContractValue;

        List<ProofPosting> rows = [.. postings.Select(posting => ToRow(posting, accounts))];
        Dictionary<string, decimal> ledgerSums = LedgerKeys.ToDictionary(key => key, _ => 0m);
        Dictionary<string, List<Guid>> ledgerPostings = LedgerKeys.ToDictionary(key => key, _ => new List<Guid>());

        foreach (ProofPosting row in rows)
        {
            foreach (ProofPostingEffect effect in row.Effects)
            {
                ledgerSums[effect.LedgerKey] += effect.Effect.Amount;
                ledgerPostings[effect.LedgerKey].Add(row.Id);
            }
        }

        List<ProofEvent> events =
        [
            .. postings
                .GroupBy(posting => (posting.SourceDocumentType, posting.SourceDocumentId))
                .Select(group => new ProofEvent(
                    group.Key.SourceDocumentType,
                    group.Key.SourceDocumentId,
                    group.Select(posting => posting.SourceDocumentReference).FirstOrDefault(reference => reference is not null),
                    group.Min(posting => posting.PostingDate),
                    Columns(group),
                    null,
                    null,
                    [.. group.Select(posting => posting.Id)]))
                .OrderBy(@event => @event.Date),
        ];

        bool released = postings.Any(posting => posting.Type == PostingType.HoldRelease && !posting.IsReversal);
        decimal holdAccrued = Net(postings, PostingType.HoldAccrual);
        decimal materialIn = Net(postings, PostingType.MaterialAdvanceIssue);
        decimal materialOut = Net(postings, PostingType.MaterialAdvanceRecovery);
        decimal cashIn = Net(postings, PostingType.ClientCollection) + Net(postings, PostingType.ClientAdvanceReceipt);

        decimal running = 0m;
        decimal lowest = 0m;
        bool anySafePosting = false;
        List<Guid> lowestPostings = [];

        foreach (Posting posting in postings)
        {
            decimal delta = 0m;
            if (accounts.TryGetValue(posting.ToAccountId, out Account? to) && to.Type == AccountType.Safe)
            {
                delta += posting.Amount.Amount;
            }

            if (accounts.TryGetValue(posting.FromAccountId, out Account? from) && from.Type == AccountType.Safe)
            {
                delta -= posting.Amount.Amount;
            }

            if (delta == 0m)
            {
                continue;
            }

            running += delta;
            if (!anySafePosting || running < lowest)
            {
                lowest = running;
                lowestPostings = [posting.Id];
            }

            anySafePosting = true;
        }

        List<ProofCheck> checks =
        [
            Check(
                "holdIsRateOfContract",
                contractValue is null ? null : holdRate.Of(contractValue.Value).Amount,
                holdAccrued,
                released,
                [.. postings.Where(posting => posting.Type == PostingType.HoldAccrual).Select(posting => posting.Id)]),
            Check("advanceEndsAtZero", 0m, ledgerSums["ClientAdvance"], released, ledgerPostings["ClientAdvance"]),
            Check(
                "materialInEqualsOut",
                materialIn,
                materialOut,
                released,
                [
                    .. postings
                        .Where(posting => posting.Type is PostingType.MaterialAdvanceIssue or PostingType.MaterialAdvanceRecovery)
                        .Select(posting => posting.Id),
                ]),
            Check(
                "collectionEqualsContract",
                contractValue?.Amount,
                cashIn,
                released,
                [
                    .. postings
                        .Where(posting => posting.Type is PostingType.ClientCollection or PostingType.ClientAdvanceReceipt)
                        .Select(posting => posting.Id),
                ]),
            new ProofCheck(
                "safeNeverNegative",
                !anySafePosting ? "Pending" : lowest >= 0m ? "Match" : "Mismatch",
                Money.Zero,
                new Money(anySafePosting ? lowest : 0m),
                null,
                lowest < 0m ? lowestPostings : []),
        ];

        List<ProofLedger> ledgers =
        [
            Ledger("ClientAdvance", balances.ClientAdvance, ledgerSums, ledgerPostings),
            Ledger("Hold", balances.Hold, ledgerSums, ledgerPostings),
            Ledger("FirmAdvance", balances.FirmAdvance, ledgerSums, ledgerPostings),
            Ledger("PettyCashAdvance", balances.PettyCashAdvance, ledgerSums, ledgerPostings),
            Ledger("OwnerCurrentAccount", balances.OwnerCurrentAccount, ledgerSums, ledgerPostings),
            Ledger("MaterialAdvance", balances.MaterialAdvance, ledgerSums, ledgerPostings),
        ];

        List<Posting> unclassified =
        [
            .. postings.Where(posting => LedgerColumnTypes.Contains(posting.Type)
                && !TouchesLedger(accounts, posting.FromAccountId)
                && !TouchesLedger(accounts, posting.ToAccountId)),
        ];

        RulingSet rulings = RulingSet.Default;

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(
            DateTimeOffset.UtcNow,
            new ProofProject(
                project.Id,
                project.Code,
                project.Name,
                project.ContractType,
                contractValue,
                Percent(holdRate),
                Percent(advanceRecoveryRate)),
            "CalculatorNotImplemented",
            new Money(running),
            events,
            checks,
            ledgers,
            new ProofUnclassified(
                new Money(unclassified.Sum(posting => posting.Amount.Amount)),
                [.. unclassified.Select(posting => posting.Id)]),
            [
                new ProofRuling("extractPostingMoment", rulings.ExtractPostingMoment.Note.Status.ToString()),
                new ProofRuling("materialAdvanceRecovery", rulings.MaterialAdvanceRecovery.Note.Status.ToString()),
            ],
            rows));
    }

    private static string Percent(Percentage rate) =>
        (rate.Fraction * 100m).ToString("0.######", CultureInfo.InvariantCulture);

    private static decimal Sign(Posting posting) => posting.IsReversal ? -1m : 1m;

    private static decimal Net(IEnumerable<Posting> postings, PostingType type) =>
        postings.Where(posting => posting.Type == type).Sum(posting => Sign(posting) * posting.Amount.Amount);

    private static ProofColumns Columns(IEnumerable<Posting> group)
    {
        List<Posting> list = [.. group];

        return new ProofColumns(
            new Money(Net(list, PostingType.RevenueRecognition)),
            new Money(Net(list, PostingType.HoldRelease) - Net(list, PostingType.HoldAccrual)),
            new Money(-Net(list, PostingType.ClientAdvanceRecovery)),
            new Money(Net(list, PostingType.MaterialAdvanceIssue) - Net(list, PostingType.MaterialAdvanceRecovery)),
            new Money(Net(list, PostingType.ClientCollection) + Net(list, PostingType.ClientAdvanceReceipt)));
    }

    private static ProofCheck Check(
        string key,
        decimal? expected,
        decimal actual,
        bool complete,
        IReadOnlyList<Guid> postingIds)
    {
        if (expected is null)
        {
            return new ProofCheck(key, "Unavailable", null, new Money(actual), null, []);
        }

        decimal difference = actual - expected.Value;
        string status = !complete ? "Pending" : difference == 0m ? "Match" : "Mismatch";

        return new ProofCheck(
            key,
            status,
            new Money(expected.Value),
            new Money(actual),
            new Money(difference),
            status == "Mismatch" ? postingIds : []);
    }

    private static ProofLedger Ledger(
        string key,
        Money balance,
        Dictionary<string, decimal> sums,
        Dictionary<string, List<Guid>> postings) =>
        new(key, balance, new Money(sums[key]), postings[key]);

    private static bool TouchesLedger(Dictionary<Guid, Account> accounts, Guid accountId) =>
        accounts.TryGetValue(accountId, out Account? account) && LedgerKeyOf(account) is not null;

    private static string? LedgerKeyOf(Account account) =>
        account.Type == AccountType.MaterialAdvance ? "MaterialAdvance" : account.LedgerKind?.ToString();

    private static ProofPosting ToRow(Posting posting, Dictionary<Guid, Account> accounts)
    {
        accounts.TryGetValue(posting.FromAccountId, out Account? from);
        accounts.TryGetValue(posting.ToAccountId, out Account? to);

        List<ProofPostingEffect> effects = [];
        foreach ((Account? account, decimal direction) in new[] { (to, 1m), (from, -1m) })
        {
            string? key = account is null ? null : LedgerKeyOf(account);
            if (account is null || key is null)
            {
                continue;
            }

            // A debit-normal account grows when it receives; a credit-normal one grows when it gives.
            decimal toward = account.NormalBalance == NormalBalance.Debit ? direction : -direction;
            effects.Add(new ProofPostingEffect(key, new Money(toward * posting.Amount.Amount)));
        }

        return new ProofPosting(
            posting.Id,
            posting.PostingDate,
            posting.Type,
            posting.SourceDocumentType,
            posting.SourceDocumentId,
            posting.SourceDocumentReference,
            from?.NameAr ?? string.Empty,
            to?.NameAr ?? string.Empty,
            posting.Amount,
            posting.ReversesId,
            effects);
    }
}
