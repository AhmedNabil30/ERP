using System.Globalization;
using Kaff.Api.Common.Results;
using Kaff.Api.Features.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
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
/// <b>The expected side is never derived from the actual side.</b> Fitting a rule to the numbers that were
/// posted would make every row match by construction and prove nothing.
/// </para>
/// <para>
/// The per-event expectation comes from the project's <c>IBillingCalculator</c>, run over the extract's own
/// stored inputs (<c>Extract.BillingInputFor</c>, the same method the deductions step uses) and over the ledger
/// balances as they stood before that extract's standing accrual postings, summed from the postings. An event whose
/// source document is an extract is judged; any other event (advance receipt, handover) has no calculator
/// behind it and keeps a null expectation. An extract that has not reached the ruled accrual moment, or was
/// rejected back to Draft, should have posted nothing, so its expectation is zero in every column; its collection
/// is expected only once it is <c>Collected</c> (spec.md section 7).
/// <see cref="Response.ExpectedState"/> is <c>Available</c> when at least one event carries an expectation.
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
        IContractTypeDispatcher dispatcher,
        RulingSet rulings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(rulings);

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
        // Revised: the signed value plus the approved change orders (ChangeOrderDisplayedValue), so the hold and
        // collection checks measure what the contract is worth now.
        Money? contractValue = project.RevisedContractValue(
            await ChangeOrderQueries.ApprovedTotalAsync(database, projectId, cancellationToken));

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

        Dictionary<Guid, Extract> extracts = (await database.Extracts
                .Include(extract => extract.ChangeOrderLines)
                .Where(extract => extract.ProjectId == projectId)
                .ToListAsync(cancellationToken))
            .ToDictionary(extract => extract.Id);
        Dictionary<Guid, ProofPosting> rowById = rows.ToDictionary(row => row.Id);
        Result<IBillingCalculator> calculator = dispatcher.BillingCalculatorFor(project.ContractType);

        List<ProofEvent> events = [];
        foreach (IGrouping<(SourceDocumentType Type, Guid Id), Posting> group in postings
                     .GroupBy(posting => (posting.SourceDocumentType, posting.SourceDocumentId)))
        {
            ProofColumns actual = Columns(group);
            ProofColumns? expected = null;
            ProofColumns? difference = null;
            string? unavailableReason = null;

            if (group.Key.Type == SourceDocumentType.Extract
                && extracts.TryGetValue(group.Key.Id, out Extract? extract))
            {
                if (calculator.IsFailure)
                {
                    unavailableReason = calculator.Error.Code;
                }
                else
                {
                    (expected, unavailableReason) = await ExpectedAsync(
                        extract, project, calculator.Value, rulings, postings, rowById, cancellationToken);
                    difference = expected is null ? null : Subtract(actual, expected);
                }
            }

            events.Add(new ProofEvent(
                group.Key.Type,
                group.Key.Id,
                group.Select(posting => posting.SourceDocumentReference).FirstOrDefault(reference => reference is not null),
                group.Min(posting => posting.PostingDate),
                actual,
                expected,
                difference,
                [.. group.Select(posting => posting.Id)],
                unavailableReason));
        }

        events = [.. events.OrderBy(@event => @event.Date)];

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
            events.Any(@event => @event.Expected is not null) ? "Available" : "NoExtractsToCompare",
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

    /// <summary>
    /// What this extract's postings should be, from the calculator over the extract's stored inputs. Null with a
    /// reason (the calculator's error code) when the calculator refuses; never fitted to the postings.
    /// </summary>
    private static async Task<(ProofColumns? Expected, string? Reason)> ExpectedAsync(
        Extract extract,
        Project project,
        IBillingCalculator calculator,
        RulingSet rulings,
        List<Posting> postings,
        Dictionary<Guid, ProofPosting> rowById,
        CancellationToken cancellationToken)
    {
        ExtractStage? reached = extract.Status switch
        {
            ExtractStatus.DeductionsApplied => ExtractStage.AccountsDeductions,
            ExtractStatus.OwnerApproved => ExtractStage.OwnerApproval,
            ExtractStatus.Issued => ExtractStage.Issued,
            ExtractStatus.Collected => ExtractStage.Collected,
            _ => null,
        };

        if (reached is not { } stage || !rulings.ExtractPostingMoment.HasAccruedBy(stage))
        {
            return (new ProofColumns(Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero), null);
        }

        // The balances the calculator would have read: every other document's effect posted before this
        // extract's own standing accrual. Summed from postings, never stored.
        DateTimeOffset cutoff = postings
            .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract
                && posting.SourceDocumentId == extract.Id
                && !posting.IsReversal
                && posting.Type is not (PostingType.ClientCollection or PostingType.TaxWithheldAtSource)
                && !postings.Any(other => other.ReversesId == posting.Id))
            .Select(posting => posting.CreatedAt)
            .DefaultIfEmpty(DateTimeOffset.MaxValue)
            .Min();

        decimal Before(string ledgerKey) => postings
            .Where(posting => posting.CreatedAt < cutoff
                && !(posting.SourceDocumentType == SourceDocumentType.Extract && posting.SourceDocumentId == extract.Id))
            .Sum(posting => rowById[posting.Id].Effects.Where(effect => effect.LedgerKey == ledgerKey).Sum(effect => effect.Effect.Amount));

        var balances = new ExtractLedgerBalances(
            new Money(Before("ClientAdvance")), new Money(Before("MaterialAdvance")), new Money(Before("Hold")));

        Result<BillingResult> billed = await calculator.CalculateAsync(
            new BillingContext(project, extract.PeriodStart, extract.PeriodEnd, extract.BillingInputFor(balances)),
            cancellationToken);
        if (billed.IsFailure)
        {
            return (null, billed.Error.Code);
        }

        Money Component(BillingComponentKind kind) =>
            Money.Sum(billed.Value.Components.Where(component => component.Kind == kind).Select(component => component.Amount));

        // Cash is the net payable less what the client withholds at source (ExtractPostings.BuildCollection), and
        // only once the client has paid.
        Money collection = stage == ExtractStage.Collected
            ? billed.Value.NetPayable - Component(BillingComponentKind.TaxAtSource)
            : Money.Zero;

        return (new ProofColumns(
            Component(BillingComponentKind.WorkValue) + Component(BillingComponentKind.ChangeOrderValue),
            -Component(BillingComponentKind.Hold),
            -Component(BillingComponentKind.AdvanceRecovery),
            Component(BillingComponentKind.MaterialAdvance) - Component(BillingComponentKind.MaterialAdvanceRecovery),
            collection), null);
    }

    private static ProofColumns Subtract(ProofColumns actual, ProofColumns expected) => new(
        actual.Work - expected.Work,
        actual.Hold - expected.Hold,
        actual.Advance - expected.Advance,
        actual.MaterialAdvance - expected.MaterialAdvance,
        actual.Collection - expected.Collection);

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
