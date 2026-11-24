using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.RejectAccountingEntry;

/// <summary>
/// <c>POST /api/treasury/accounting-requests/{requestId}/rejection</c>. The Owner returns a waiting accounting request to Finance
/// with a stored reason. Nothing is posted; Finance may ask again. A blank reason is refused (CLAUDE.md).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/accounting-requests/{requestId:guid}/rejection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.AccountingEntryApprove)
            .WithName("RejectAccountingEntry")
            .WithTags("Accounting");
    }
}

/// <param name="Reason">Why the request is returned. Stored on the request and the audit trail.</param>
public sealed record Request(string? Reason);

public sealed record Response(Guid RequestId, AccountingRequestStatus Status, string? RejectionReason);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        [FromRoute] Guid requestId,
        Request body,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        // Same row lock as the approval, so a decision racing a decision cannot both pass.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM accounting_requests WHERE id = {requestId} FOR UPDATE", cancellationToken);

        AccountingRequest? request = await database.AccountingRequests.FirstOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (request is null)
        {
            return ResultExtensions.Problem(AccountingErrors.RequestNotFound);
        }

        Result rejected = request.Reject(currentUser.UserId ?? Guid.Empty, body.Reason, clock.GetUtcNow());
        if (rejected.IsFailure)
        {
            return ResultExtensions.Problem(rejected.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(new Response(request.Id, request.Status, request.RejectionReason));
    }
}
