using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Closure;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CollectRequest = Kaff.Api.Features.Extracts.CollectExtract.Request;

namespace Kaff.Api.Features.Closure.CollectReleasedHold;

/// <summary>
/// Where the released hold stands against what the client has paid on it (D-244 item 2), derived on every read.
/// </summary>
/// <param name="Expected">The hold released at handover, summed from the release posting. Zero before the release.</param>
/// <param name="Received">Cash the client paid on it, net of reversals.</param>
/// <param name="Withheld">Tax the client withheld at source on it.</param>
/// <param name="InCheques">Cheques received or deposited and not yet cleared.</param>
/// <param name="Remaining">What may still be recorded: expected less received, withheld and cheques in hand.</param>
public sealed record Response(Guid ProjectId, Money Expected, Money Received, Money Withheld, Money InCheques, Money Remaining);

/// <summary>The released hold's position, read from postings and cheques. Never stored.</summary>
internal sealed record ReleasedHoldState(CollectionPosition Position, Money Received, Money Withheld)
{
    public Response ToResponse(Guid projectId) =>
        new(projectId, Position.Expected, Received, Withheld, Position.InCheques, Position.Remaining);
}

/// <summary>
/// D-244 item 2: Finance records a payment of the released hold, by cash, transfer or cheque, under the project row lock.
/// The rules and postings are <see cref="ReleasedHoldPayments"/>'s, which are the extract's own. The record, the
/// cheque and the postings commit in one save, or nothing does.
/// </summary>
internal static class Handler
{
    private static readonly PostingType[] CollectionTypes = [PostingType.ClientCollection, PostingType.TaxWithheldAtSource];

    public static async Task<IResult> ReadAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        bool exists = await database.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId, cancellationToken);
        return exists
            ? Microsoft.AspNetCore.Http.Results.Ok((await StateAsync(database, projectId, cancellationToken)).ToResponse(projectId))
            : ResultExtensions.Problem(ProjectErrors.NotFound);
    }

    public static async Task<IResult> CollectAsync(
        Guid projectId,
        CollectRequest request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await RunAsync(
            projectId,
            database,
            rulings,
            clock,
            (project, accounts, state, actor, now, _) => Task.FromResult(Record(database, rulings, request, project, accounts, state, actor, now)),
            currentUser,
            respond: null,
            cancellationToken);
    }

    private static Result<IReadOnlyList<Posting>> Record(
        KaffDbContext database,
        RulingSet rulings,
        CollectRequest request,
        Project project,
        Kaff.Domain.Contracts.Billing.ExtractAccounts accounts,
        ReleasedHoldState state,
        Guid actor,
        DateTimeOffset now)
    {
        Money cash = request.CashReceived ?? Money.Zero;
        Money withheld = request.WithheldAtSource ?? Money.Zero;

        IReadOnlyList<Posting> postings = [];
        if (request.Method == CollectionMethod.Cheque)
        {
            Result accepted = ReleasedHoldPayments.AcceptCheque(project, rulings, state.Position, cash, withheld);
            if (accepted.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Posting>>(accepted.Error);
            }
        }
        else
        {
            Result<PostingSet> collected = ReleasedHoldPayments.Collect(
                project, rulings, accounts, state.Position, cash, withheld, SiteTime.DateOf(now), actor, now);
            if (collected.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Posting>>(collected.Error);
            }

            postings = [.. collected.Value.Postings];
        }

        Result<CollectionRecord> recorded = CollectionRecord.Record(
            project.Id, null, request.Method, request.Date, request.Reference, cash, withheld, actor, now);
        if (recorded.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Posting>>(recorded.Error);
        }

        database.CollectionRecords.Add(recorded.Value);
        if (recorded.Value.Method == CollectionMethod.Cheque)
        {
            database.Cheques.Add(Cheque.Receive(recorded.Value, actor, now));
        }

        return Result.Success(postings);
    }

    /// <summary>
    /// The shared spine of every released-hold move (a payment, a cheque clearance or bounce): lock the project row, read
    /// the position, run the move, and commit its postings and records in one save. The permission gate has already run
    /// on the route. A guard refusal at save or commit rolls everything back.
    /// </summary>
    public static async Task<IResult> RunAsync(
        Guid projectId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        Func<Project, Kaff.Domain.Contracts.Billing.ExtractAccounts, ReleasedHoldState, Guid, DateTimeOffset, CancellationToken, Task<Result<IReadOnlyList<Posting>>>> move,
        ICurrentUser currentUser,
        Func<IResult>? respond,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(currentUser);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Result<Kaff.Domain.Extracts.ExtractPostingContext> context =
            await ExtractSupport.ContextAsync(database, project, rulings, clock, cancellationToken);
        if (context.IsFailure)
        {
            return ResultExtensions.Problem(context.Error);
        }

        ReleasedHoldState state = await StateAsync(database, projectId, cancellationToken);
        Result<IReadOnlyList<Posting>> moved = await move(
            project, context.Value.Accounts, state, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow(), cancellationToken);
        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        database.Postings.AddRange(moved.Value);

        // The deferred guards fire at COMMIT, so both the save and the commit are inside the translation.
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
            return ResultExtensions.Problem(refused);
        }

        if (respond is not null)
        {
            return respond();
        }

        database.ChangeTracker.Clear();
        return Microsoft.AspNetCore.Http.Results.Ok((await StateAsync(database, projectId, cancellationToken)).ToResponse(projectId));
    }

    /// <summary>
    /// Expected is the standing hold release; collected the collection postings against the handover; in cheques the
    /// handover's cheques still in hand (a collection record with no extract). Summed in memory: EF cannot sum Money.
    /// </summary>
    public static async Task<ReleasedHoldState> StateAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        var legs = await database.Postings
            .AsNoTracking()
            .Where(posting => posting.ProjectId == projectId
                && posting.SourceDocumentType == SourceDocumentType.ProjectHandover
                && posting.SourceDocumentId == projectId
                && (posting.Type == PostingType.HoldRelease || CollectionTypes.Contains(posting.Type)))
            .Select(posting => new { posting.Type, posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        Money Sum(PostingType type) =>
            Money.Sum(legs.Where(leg => leg.Type == type).Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount));

        List<Money> inHand =
        [
            .. (await (
                    from cheque in database.Cheques.AsNoTracking()
                    join record in database.CollectionRecords.AsNoTracking() on cheque.CollectionId equals record.Id
                    where record.ProjectId == projectId && record.ExtractId == null
                    select new { cheque.State, record.CashReceived, record.WithheldAtSource })
                .ToListAsync(cancellationToken))
                .Where(row => Cheque.IsInHand(row.State))
                .Select(row => row.CashReceived + row.WithheldAtSource),
        ];

        Money received = Sum(PostingType.ClientCollection);
        Money withheld = Sum(PostingType.TaxWithheldAtSource);
        return new ReleasedHoldState(
            new CollectionPosition(Sum(PostingType.HoldRelease), received + withheld, Money.Sum(inHand)), received, withheld);
    }
}
