using Kaff.Api.Common.Results;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.RaiseAdjustment;

/// <summary>
/// Raises a client credit note. Every type but the credit note is refused before anything is read or written. Nothing is
/// posted: the posting is written when the Owner approves.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type is not { } type)
        {
            return ResultExtensions.Problem(AdjustmentErrors.TypeRequired);
        }

        if (type != AdjustmentType.ClientCreditNote)
        {
            return ResultExtensions.Problem(Adjustment.RefusalFor(type));
        }

        if (request.SourceExtractId is not { } extractId)
        {
            return ResultExtensions.Problem(AdjustmentErrors.SourceDocumentRequired);
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        Extract? extract = await database.Extracts
            .FirstOrDefaultAsync(candidate => candidate.Id == extractId && candidate.ProjectId == projectId, cancellationToken);
        if (project is null || extract is null)
        {
            return ResultExtensions.Problem(extract is null ? ExtractErrors.NotFound : ProjectErrors.NotFound);
        }

        Result<Adjustment> raised = await AdjustmentSupport.RaiseCreditNoteAsync(
            database, project, extract, request.TargetAccountId, request.Amount, request.Reason, rulings,
            currentUser.UserId ?? Guid.Empty, clock.GetUtcNow(), cancellationToken);
        if (raised.IsFailure)
        {
            return ResultExtensions.Problem(raised.Error);
        }

        Error? refused = await AdjustmentSupport.SaveNewAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/adjustments/{raised.Value.Id}", AdjustmentResponse.From(raised.Value));
    }
}
