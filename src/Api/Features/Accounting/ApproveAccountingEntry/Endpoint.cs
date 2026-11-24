using System.Globalization;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.ApproveAccountingEntry;

/// <summary>
/// <c>POST /api/treasury/accounting-requests/{requestId}/approval</c>. The Owner approves a waiting accounting request and the
/// postings are written, built from what Finance asked. The Owner is company-wide, so one route serves company and project tags.
/// The user who asked can never approve (separation of duties). The request row is locked for the decision, so two decisions cannot
/// both pass. The postings carry the requester as their creator; the approver is on the request and the audit trail.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/accounting-requests/{requestId:guid}/approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.AccountingEntryApprove)
            .WithName("ApproveAccountingEntry")
            .WithTags("Accounting");
    }
}

/// <param name="AccrualId">Set when the approval created an accrual.</param>
public sealed record Response(Guid RequestId, AccountingRequestStatus Status, IReadOnlyList<Guid> PostingIds, Guid? AccrualId);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        [FromRoute] Guid requestId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(audit);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM accounting_requests WHERE id = {requestId} FOR UPDATE", cancellationToken);

        AccountingRequest? request = await database.AccountingRequests.FirstOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (request is null)
        {
            return ResultExtensions.Problem(AccountingErrors.RequestNotFound);
        }

        Guid actor = currentUser.UserId ?? Guid.Empty;
        DateTimeOffset now = clock.GetUtcNow();

        Result gate = request.EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return ResultExtensions.Problem(gate.Error);
        }

        Result<IReadOnlyList<object>> built = await AccountingEntries.BuildAsync(
            request.Kind, request.ProjectId, request.Payload, request.RequestedByUserId, database, rulings, now, track: true, cancellationToken);
        if (built.IsFailure)
        {
            return ResultExtensions.Problem(built.Error);
        }

        // No blind approval (D-267): the Owner saw the total at ask time. A row without a stored total, or one whose rebuilt total moved, is asked again.
        if (request.Amount is null || request.Amount.Value != AccountingEntries.Total(built.Value))
        {
            return ResultExtensions.Problem(AccountingErrors.RequestAmountChanged);
        }

        request.Approve(actor, now);

        foreach (object entity in built.Value)
        {
            database.Add(entity);
        }

        audit.SetReason(string.Create(
            CultureInfo.InvariantCulture, $"{request.Kind} approved, asked by {request.RequestedByUserId}"));

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

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(new Response(
                request.Id,
                request.Status,
                [.. built.Value.OfType<Posting>().Select(posting => posting.Id)],
                built.Value.OfType<Accrual>().FirstOrDefault()?.Id));
    }
}
