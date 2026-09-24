using Kaff.Api.Common.Results;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Treasury.ListPostings;

/// <summary>
/// Reads one account's statement, oldest first, with a running balance summed fresh on every call.
/// KAFF-306.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule about the figures goes through <see cref="PostingStatementQueries"/>, and nothing
/// here reproduces one.</b> This handler's job is loading the account, filtering and ordering the
/// postings, and slicing the page — the running balance itself, and keeping a reversed original
/// visible beside its reversal, both live in the Domain method (AC-306-B, AC-306-C).
/// </para>
/// <para>
/// <b>Loads the whole filtered set into memory before paging.</b> A page's running balance has to
/// start from the account's real history, not from wherever the cursor happens to land, so the
/// statement is built once over everything the filters match and then sliced.
/// <c>ponytail:</c> fine at Kaff's per-account posting volume; if one account's history ever
/// outgrows memory, move the running sum to a SQL window function — that needs a migration, which
/// this read-only story cannot add.
/// </para>
/// <para>
/// <b>The route's project, if any, must own the account being queried.</b> Same reasoning as
/// <c>ReversePosting.Handler</c>: a caller with <c>TreasuryReadProject</c> on project A must not be
/// able to read project B's account by naming its id through project A's route — the permission
/// check alone does not catch this, because it authorizes the route's project, not the account the
/// query string names. An account outside the route's scope is refused exactly like one that does
/// not exist, so the two cases stay indistinguishable from the outside.
/// </para>
/// </remarks>
internal static class Handler
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static async Task<IResult> HandleAsync(
        Guid? projectId,
        Guid accountId,
        DateOnly? from,
        DateOnly? to,
        PostingType? type,
        string? cursor,
        int pageSize,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Account? account = await database.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);

        if (account is null || (projectId is not null && account.ProjectId != projectId))
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        int limit = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        IQueryable<Posting> query = database.Postings
            .Where(posting => posting.FromAccountId == accountId || posting.ToAccountId == accountId);

        if (from is not null)
        {
            query = query.Where(posting => posting.PostingDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(posting => posting.PostingDate <= to);
        }

        if (type is not null)
        {
            query = query.Where(posting => posting.Type == type);
        }

        List<Posting> ordered = await query
            .OrderBy(posting => posting.PostingDate)
            .ThenBy(posting => posting.Id)
            .ToListAsync(cancellationToken);

        IReadOnlyList<PostingStatementRow> statement =
            PostingStatementQueries.BuildStatement(ordered, accountId, account.NormalBalance);

        int start = 0;

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out Guid afterId))
        {
            for (int i = 0; i < statement.Count; i++)
            {
                if (statement[i].Id == afterId)
                {
                    start = i + 1;
                    break;
                }
            }
        }

        List<PostingRow> page =
        [
            .. statement.Skip(start).Take(limit).Select(row => new PostingRow(
                row.Id,
                row.PostingDate,
                row.Type,
                row.Inflow,
                row.Outflow,
                row.RunningBalance,
                row.SourceDocument.Type,
                row.SourceDocument.Id,
                row.SourceDocument.Reference,
                row.ReversesId,
                row.IsReversal)),
        ];

        string? nextCursor = start + page.Count < statement.Count ? page[^1].Id.ToString() : null;

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(page, nextCursor));
    }
}
