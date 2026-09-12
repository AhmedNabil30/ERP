using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.OpenEngagement;

/// <summary>
/// Opens an engagement for a worker on the route's project. KAFF-210.
/// </summary>
/// <remarks>
/// <para>
/// <b>The project is not re-checked here.</b> Same reasoning as
/// <c>DayLabour.RegisterFromSite.Handler</c>: <c>Permission.DayLabourSiteManage</c> is project-scoped,
/// so a caller only reaches this handler once the access policy has granted the route's project. The
/// engagement records that project on <see cref="Engagement.ProjectId"/> — decisions.md D-140 point 3's
/// second bullet.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c>
/// record in the same transaction, carrying the project via decisions.md D-148's fallback since
/// <see cref="Engagement"/> names its own <c>ProjectId</c> directly (no fallback needed here, but the
/// column is populated either way).
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(clock);

        if (request.WorkerId is not { } workerId)
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeeNotFound);
        }

        bool workerExists = await database.Employees
            .AnyAsync(employee => employee.Id == workerId && employee.Kind == EmployeeKind.DayLabour, cancellationToken);

        if (!workerExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeeNotFound);
        }

        DateTimeOffset now = clock.GetUtcNow();

        Result<Engagement> opened = Engagement.Open(workerId, projectId, DateOnly.FromDateTime(now.UtcDateTime), now);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        Engagement engagement = opened.Value;

        database.Engagements.Add(engagement);
        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/day-labour/engagements/{engagement.Id}",
            new Response(engagement.Id, engagement.WorkerId, engagement.ProjectId, engagement.OpenedOn));
    }
}
