using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.ConvertToExecution;

/// <summary>
/// Opens the execution project of a design project (spec.md section 5.4, KAFF-415). The new project, its account set
/// and its link back to the design commit in one save. The design project is read, locked and never modified.
/// </summary>
/// <remarks>
/// The design row is locked for the transaction so two concurrent conversions cannot both find no execution project
/// (Q-207-7). No audit record is hand-written: the project and its accounts are tracked entities, so the interceptor
/// writes a <c>Created</c> record for each in the same save.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        if (request.ContractType is not { } executionType || !Enum.IsDefined(executionType))
        {
            return ResultExtensions.Problem(ProjectErrors.ExecutionTypeNotAllowed);
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        Project? design = await database.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (design is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        bool alreadyConverted = await database.Projects.AnyAsync(
            candidate => candidate.LinkedProjectId == projectId && candidate.LinkType == ProjectLinkType.DesignToExecution,
            cancellationToken);

        Result<OpenedProject> opened = ProjectOpening.ConvertToExecution(
            design, alreadyConverted, request.Name, executionType, rulings, clock.GetUtcNow());

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        Project execution = opened.Value.Project;
        database.Projects.Add(execution);
        database.Accounts.AddRange(opened.Value.Accounts);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{execution.Id}",
            new Response(
                execution.Id,
                execution.Code,
                execution.Name,
                execution.ClientId,
                execution.ContractType,
                execution.Status,
                projectId,
                execution.CreatedAt));
    }
}
