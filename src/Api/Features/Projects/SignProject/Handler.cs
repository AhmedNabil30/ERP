using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.SignProject;

/// <summary>
/// Signs the contract. The BOQ total becomes the contract value, the BOQ is frozen and the date is
/// stamped — all three in the one <c>SaveChangesAsync</c>, so a project is never signed with a BOQ that
/// can still change, nor a BOQ frozen under a project that is not signed.
/// </summary>
/// <remarks>
/// Which source each contract type takes its value from, which quantity prices a line, and how a line
/// rounds are rulings (<see cref="ContractValueSource"/>), not code here. No audit record is written by
/// hand: the project and the BOQ header are tracked entities, so the interceptor writes one <c>Modified</c>
/// record for each. Freezing touches no line row, so a 400-line BOQ writes two records, not 402.
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

        Project? project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Boq? boq = await database.Boqs
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        DateOnly signedOn = request.SignedOn ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        Money? boqTotal = null;

        if (ContractValueSource.RequiresBoq(project.ContractType))
        {
            if (boq is null)
            {
                return ResultExtensions.Problem(BoqErrors.Required);
            }

            Result<Money> total = boq.Total(rulings.ContractValueSource);

            if (total.IsFailure)
            {
                return ResultExtensions.Problem(total.Error);
            }

            boqTotal = total.Value;
        }

        Result signed = project.Sign(signedOn, boqTotal);

        if (signed.IsFailure)
        {
            return ResultExtensions.Problem(signed.Error);
        }

        if (boq is not null)
        {
            Result frozen = boq.Freeze(signedOn);

            if (frozen.IsFailure)
            {
                return ResultExtensions.Problem(frozen.Error);
            }
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(project.Id, signedOn, project.ContractValue, boq?.FrozenOn));
    }
}
