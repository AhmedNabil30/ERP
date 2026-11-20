using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts;

/// <summary>Everything a step needs from the request, bundled so each endpoint's handler stays one signature.</summary>
internal sealed record StepServices(
    KaffDbContext Database,
    RulingSet Rulings,
    TimeProvider Clock,
    ICurrentUser CurrentUser,
    IAuthorizationService Authorization,
    ClaimsPrincipal User);

/// <summary>One step of the chain: calls an <see cref="Extract"/> method and returns the postings it produced, if any.</summary>
internal delegate Task<Result<IReadOnlyList<Posting>>> ExtractStep(
    Extract extract, Project project, ExtractPostingContext? context, StepServices services, CancellationToken cancellationToken);

/// <summary>
/// The shared spine of every step endpoint: lock the extract row, load, run the aggregate method, and commit the
/// postings and the status change in <b>one</b> save. The permission gate (role and assignment) has already run
/// on the route; nothing here re-decides it. Audit records come from the interceptor on the same save.
/// </summary>
internal static class ExtractSupport
{
    public static DateOnly Today(TimeProvider clock) => SiteTime.Today(clock);

    /// <summary>The extract permissions the caller holds on the route's project, each asked of the real evaluator.</summary>
    public static async Task<ExtractViewer> ViewerAsync(StepServices services) =>
        new(services.CurrentUser.UserId ?? Guid.Empty, await HeldAsync(services.Authorization, services.User));

    /// <summary>Whether the caller may see the money on an extract. The same rule <see cref="ExtractViewer.MaySeeMoney"/> applies.</summary>
    public static async Task<bool> MaySeeMoneyAsync(IAuthorizationService authorization, ClaimsPrincipal user) =>
        new ExtractViewer(Guid.Empty, await HeldAsync(authorization, user)).MaySeeMoney;

    private static async Task<HashSet<Permission>> HeldAsync(IAuthorizationService authorization, ClaimsPrincipal user)
    {
        HashSet<Permission> held = [];
        foreach (Permission permission in ExtractView.Permissions)
        {
            AuthorizationResult result = await authorization.AuthorizeAsync(
                user, resource: null, PermissionRequirement.ToPolicyName(permission, ProjectScope.FromRoute()));
            if (result.Succeeded)
            {
                held.Add(permission);
            }
        }

        return held;
    }

    /// <summary>
    /// Cumulative certified work: the sum, over the frozen BOQ's lines, of the line's rate times its executed
    /// quantity, rounded as the ruled <c>ContractValueSource</c> rounds a line total. Shared by preparation and
    /// by the revision of a rejected extract so the two can never derive it differently.
    /// </summary>
    public static Money CumulativeCertifiedWork(Boq boq, RulingSet rulings)
    {
        ArgumentNullException.ThrowIfNull(boq);
        ArgumentNullException.ThrowIfNull(rulings);

        return Money.Sum(boq.Lines.Select(line => rulings.ContractValueSource.LineTotal(line.SellRate, line.QuantityExecuted)));
    }

    public static IQueryable<Extract> WithChildren(KaffDbContext database) =>
        database.Extracts.Include(extract => extract.GateHistory).Include(extract => extract.Figures).Include(extract => extract.ChangeOrderLines);

    /// <summary>
    /// Runs one step against an extract that belongs to <paramref name="projectId"/>, holding a row lock for the
    /// transaction so two concurrent approvals cannot both pass the state check and post twice.
    /// </summary>
    /// <param name="respond">What a successful step answers. The extract as read, unless the route answers about something else (a cheque).</param>
    public static async Task<IResult> RunAsync(
        Guid projectId,
        Guid id,
        bool needsAccounts,
        StepServices services,
        ExtractStep step,
        CancellationToken cancellationToken,
        Func<IResult>? respond = null)
    {
        KaffDbContext database = services.Database;

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM extracts WHERE id = {id} AND project_id = {projectId} FOR UPDATE", cancellationToken);

        Extract? extract = await WithChildren(database)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (extract is null || project is null)
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        ExtractPostingContext? context = null;
        if (needsAccounts)
        {
            Result<ExtractPostingContext> built = await ContextAsync(database, project, services.Rulings, services.Clock, cancellationToken);
            if (built.IsFailure)
            {
                return ResultExtensions.Problem(built.Error);
            }

            context = built.Value;
            if (extract.RevisesExtractId is not null)
            {
                Money carried = (await ExtractMoneyQueries.CarriedHoldAsync(database, [extract], cancellationToken))
                    .GetValueOrDefault(extract.Id, Money.Zero);
                context = context with { CarriedHold = carried };
            }
        }

        Result<IReadOnlyList<Posting>> stepped = await step(extract, project, context, services, cancellationToken);
        if (stepped.IsFailure)
        {
            return ResultExtensions.Problem(stepped.Error);
        }

        IReadOnlyList<Posting> postings = stepped.Value;
        database.Postings.AddRange(postings);

        // The deferred non-negative-balance guard fires at COMMIT, so both the save and the commit are inside the
        // guard translation. Either refusal rolls the whole transaction back: no posting and no status change.
        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is null)
        {
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (DatabaseGuardTranslation.Translate(exception) is { } violation)
            {
                refused = violation;
            }
        }

        if (refused is not null)
        {
            await transaction.DisposeAsync();
            return await RefusalAsync(database, refused, postings, cancellationToken);
        }

        if (respond is not null)
        {
            return respond();
        }

        return Microsoft.AspNetCore.Http.Results.Ok(await ViewAsync(services, extract, project, cancellationToken));
    }

    /// <summary>The extract as this caller reads it: the shared build behind every route that answers with the extract.</summary>
    public static async Task<ExtractResponse> ViewAsync(
        StepServices services, Extract extract, Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        ExtractViewer viewer = await ViewerAsync(services);
        KaffDbContext database = services.Database;

        Guid[] changeOrderIds = [.. extract.ChangeOrderLines.Select(line => line.ChangeOrderId)];
        Dictionary<Guid, (int Number, string Title)> titles = (await database.ChangeOrders.AsNoTracking()
                .Where(order => changeOrderIds.Contains(order.Id))
                .Select(order => new { order.Id, order.Number, order.Title })
                .ToListAsync(cancellationToken))
            .ToDictionary(order => order.Id, order => (order.Number, order.Title));

        // D-241 item 2: the link both ways, so the original names its revision and the revision its original.
        List<ExtractLink> links = await database.Extracts.AsNoTracking()
            .Where(candidate => candidate.ProjectId == extract.ProjectId
                && (candidate.Id == extract.RevisesExtractId || candidate.RevisesExtractId == extract.Id))
            .Select(candidate => new ExtractLink(candidate.Id, candidate.Number, candidate.Status))
            .ToListAsync(cancellationToken);

        Guid[] actorIds = [.. extract.GateHistory.Select(record => record.ActorUserId).Distinct()];
        Dictionary<Guid, string> names = await database.Users.AsNoTracking()
            .Where(candidate => actorIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, candidate => candidate.FullName, cancellationToken);

        // D-246: the credit notes that stand against the extract. A rejected or reversed note no longer does.
        List<Adjustment> notes = await database.Adjustments.AsNoTracking()
            .Where(adjustment => adjustment.SourceDocumentType == SourceDocumentType.Extract
                && adjustment.SourceDocumentId == extract.Id
                && (adjustment.Status == AdjustmentStatus.Raised || adjustment.Status == AdjustmentStatus.Approved))
            .OrderBy(adjustment => adjustment.Number)
            .ToListAsync(cancellationToken);

        IReadOnlyList<StaffMember> staff = await ExtractWaitingOnView.StaffAsync(database, project.Id, cancellationToken);
        Adjustment? raised = notes.Find(adjustment => adjustment.Status == AdjustmentStatus.Raised);

        // The same waiting line the adjustment's own read and the extract list give.
        WaitingLineResponse? noteWaiting = raised is null ? null : WaitingLine.ForCreditNote(raised, extract, staff);

        ExtractCollectionState? collection = await ExtractCollections.StateAsync(database, extract, cancellationToken);
        ExtractResponse built = ExtractView.Build(
            extract, project, services.Rulings, viewer,
            ExtractWaitingOnView.Build(extract, staff),
            collection,
            titles);

        // D-260 item 6: the resolutions the dispute route would accept now, so the screen offers only those.
        List<DisputeResolutionKind> resolutions = [];
        if (built.Actions.ResolveDispute)
        {
            resolutions.Add(DisputeResolutionKind.CreditNote);
            bool revisionPosts = PostingProjectStateLegality
                .Validate(services.Rulings.PostingProjectStates, project.Status, PostingType.RevenueRecognition, isReversal: true)
                .IsSuccess;
            if (revisionPosts && extract.EnsureMayCancelForRevision(
                    await LaterExtractExistsAsync(database, extract, cancellationToken), collection?.BlocksRevision ?? false).IsSuccess)
            {
                resolutions.Add(DisputeResolutionKind.RevisedExtract);
            }
        }

        // AC-500-E: the NCR the extract's held remainder waits on, while it is open.
        Ncr? openNcr = await database.Ncrs.AsNoTracking()
            .FirstOrDefaultAsync(ncr => ncr.ExtractId == extract.Id && ncr.Status == NcrStatus.Open, cancellationToken);

        bool seesMoney = viewer.MaySeeMoney;
        return built with
        {
            OpenNcr = openNcr is null
                ? null
                : Kaff.Api.Features.Ncrs.NcrResponse.From(
                    openNcr, extract, seesMoney, viewer.Held.Contains(Kaff.Api.Features.Ncrs.NcrSupport.ClosePermission)),
            GateHistory = [.. built.GateHistory.Select(record => record with { ActorName = names.GetValueOrDefault(record.ActorUserId) })],
            Revises = links.Find(link => link.Id == extract.RevisesExtractId),
            RevisedBy = links.Find(link => link.Id != extract.RevisesExtractId),
            WithholdingCategory = seesMoney ? project.WithholdingCategory : null,
            CreditNotes = [.. notes.Select(note => new ExtractCreditNote(note.Id, note.Number, note.Status, seesMoney ? note.Amount : null))],
            CreditNoteWaitingOn = noteWaiting,
            DisputeResolutions = resolutions,
        };
    }

    /// <summary>Whether a standing extract numbered after <paramref name="extract"/> exists: a revised extract is then refused.</summary>
    public static Task<bool> LaterExtractExistsAsync(KaffDbContext database, Extract extract, CancellationToken cancellationToken) =>
        database.Extracts.AnyAsync(
            candidate => candidate.ProjectId == extract.ProjectId
                && candidate.Number > extract.Number
                && candidate.Status != ExtractStatus.Void,
            cancellationToken);

    /// <summary>
    /// AC-500-K. The refusal names the reason and the balance of every account the postings touched <i>as it then
    /// stands</i>, read after the rollback, so nothing the failed attempt did can show in it.
    /// </summary>
    private static async Task<IResult> RefusalAsync(
        KaffDbContext database, Error refused, IReadOnlyList<Posting> postings, CancellationToken cancellationToken)
    {
        Guid[] touched = [.. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct()];
        database.ChangeTracker.Clear();

        var balances = await database.AccountBalances
            .AsNoTracking()
            .Where(balance => touched.Contains(balance.AccountId))
            .Select(balance => new { balance.AccountId, balance.AccountCode, Balance = balance.SignedBalance.Amount })
            .ToListAsync(cancellationToken);

        return ResultExtensions.Problem(
            refused,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["balances"] = balances.Select(balance => new
                {
                    accountId = balance.AccountId,
                    accountCode = balance.AccountCode,
                    balance = balance.Balance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                }).ToList(),
            });
    }

    /// <summary>
    /// The accounts an extract posts across. The four ledgers are the project's own; the safe is the company's.
    /// A Lump Sum project is not opened with a revenue account (<c>ProjectAccountSetFactory</c> lists none), so it
    /// is opened here the first time an extract needs it, through <see cref="ProjectAccountSetFactory.Open"/>.
    /// </summary>
    public static async Task<Result<ExtractPostingContext>> ContextAsync(
        KaffDbContext database, Project project, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
    {
        List<Account> own = await database.Accounts
            .Where(account => account.ProjectId == project.Id && account.IsActive)
            .ToListAsync(cancellationToken);

        Account? Pick(AccountType type) => own.FirstOrDefault(account => account.Type == type);

        Account? safe = await database.Accounts
            .Where(account => account.Type == AccountType.Safe && account.IsActive)
            .OrderBy(account => account.Code)
            .FirstOrDefaultAsync(cancellationToken);
        Account? tax = await database.Accounts
            .Where(account => account.Type == AccountType.TaxWithheldAtSource && account.IsActive)
            .OrderBy(account => account.Code)
            .FirstOrDefaultAsync(cancellationToken);

        Account? receivable = Pick(AccountType.ClientReceivable);
        Account? hold = Pick(AccountType.Hold);
        Account? clientAdvance = Pick(AccountType.ClientAdvance);
        Account? materialAdvance = Pick(AccountType.MaterialAdvance);

        if (safe is null || receivable is null || hold is null || clientAdvance is null || materialAdvance is null)
        {
            return Result.Failure<ExtractPostingContext>(ExtractErrors.AccountsMissing);
        }

        Account? revenue = Pick(AccountType.ContractRevenue);
        if (revenue is null)
        {
            Result<Account> opened = ProjectAccountSetFactory.Open(project, AccountType.ContractRevenue, Today(clock));
            if (opened.IsFailure)
            {
                return Result.Failure<ExtractPostingContext>(opened.Error);
            }

            revenue = opened.Value;
            database.Accounts.Add(revenue);
        }

        return new ExtractPostingContext(
            project,
            new ExtractAccounts(revenue, receivable, hold, clientAdvance, materialAdvance, safe, tax),
            rulings,
            Today(clock));
    }

    /// <summary>
    /// The ledger balances the calculator reads, summed from the postings by the <c>account_balances</c> view.
    /// There is no stored balance to read.
    /// </summary>
    public static ExtractLedgerBalances Balances(KaffDbContext database, Guid projectId)
    {
        LedgerBalances ledgers = BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId);
        return new ExtractLedgerBalances(ledgers.ClientAdvance, ledgers.MaterialAdvance, ledgers.Hold);
    }

    /// <summary>
    /// The accrual postings the extract wrote that are still standing: not reversals, and not already reversed by
    /// an earlier rejection. These are the legs <see cref="Extract.Reject"/> mirrors.
    /// </summary>
    public static async Task<IReadOnlyList<ExtractPostedLeg>> StandingAccrualLegsAsync(
        KaffDbContext database, Guid extractId, CancellationToken cancellationToken)
    {
        List<Posting> postings = await database.Postings
            .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract
                && posting.SourceDocumentId == extractId
                && posting.ReversesId == null
                && posting.Type != PostingType.ClientCollection
                && posting.Type != PostingType.TaxWithheldAtSource
                && !database.Postings.Any(other => other.ReversesId == posting.Id))
            .ToListAsync(cancellationToken);

        Guid[] accountIds = [.. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct()];
        Dictionary<Guid, Account> accounts = await database.Accounts
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        return [.. postings.Select(posting => new ExtractPostedLeg(posting, accounts[posting.FromAccountId], accounts[posting.ToAccountId]))];
    }

    /// <summary>
    /// The collection postings (cash and withholding) that are still standing: not reversals, and not already
    /// reversed. These are the legs <see cref="Extract.ReverseCollection"/> mirrors.
    /// </summary>
    public static async Task<IReadOnlyList<ExtractPostedLeg>> StandingCollectionLegsAsync(
        KaffDbContext database, Guid extractId, CancellationToken cancellationToken)
    {
        List<Posting> postings = await database.Postings
            .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract
                && posting.SourceDocumentId == extractId
                && posting.ReversesId == null
                && (posting.Type == PostingType.ClientCollection || posting.Type == PostingType.TaxWithheldAtSource)
                && !database.Postings.Any(other => other.ReversesId == posting.Id))
            .ToListAsync(cancellationToken);

        Guid[] accountIds = [.. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct()];
        Dictionary<Guid, Account> accounts = await database.Accounts
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        return [.. postings.Select(posting => new ExtractPostedLeg(posting, accounts[posting.FromAccountId], accounts[posting.ToAccountId]))];
    }

    public static Result<IReadOnlyList<Posting>> NoPostings() => Result.Success<IReadOnlyList<Posting>>([]);

    public static Result<IReadOnlyList<Posting>> NoPostingsOr(Result result) =>
        result.IsFailure ? Result.Failure<IReadOnlyList<Posting>>(result.Error) : NoPostings();

    public static Result<IReadOnlyList<Posting>> Of(Result<PostingSet?> result) =>
        result.IsFailure
            ? Result.Failure<IReadOnlyList<Posting>>(result.Error)
            : Result.Success<IReadOnlyList<Posting>>(result.Value is null ? [] : [.. result.Value.Postings]);
}
