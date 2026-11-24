using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.ListAccruals;

/// <summary>
/// The accruals of one tag, newest first. <c>?open=true</c> keeps only the ones not yet released. KAFF-701 AC1.
/// The company route lists company-tagged accruals only; a project's accruals are read through that project's route and its assignment.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/accruals";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/accruals";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(CompanyRoute, (KaffDbContext database, bool? open, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, open, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ListCompanyAccruals")
            .WithTags("Accounting");

        app.MapGet(ProjectRoute, ([FromRoute] Guid projectId, KaffDbContext database, bool? open, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, open, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("ListProjectAccruals")
            .WithTags("Accounting");
    }
}

public sealed record Row(Guid Id, Guid? ProjectId, Money Amount, DateOnly AccruedOn, string? Description, bool IsOpen, DateOnly? ReleasedOn);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid? projectId, bool? open, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        IQueryable<Accrual> query = database.Accruals.Where(accrual => accrual.ProjectId == projectId);

        if (open == true)
        {
            query = query.Where(accrual => accrual.ReleasedOn == null);
        }

        List<Row> rows = await query
            .OrderByDescending(accrual => accrual.AccruedOn)
            .ThenBy(accrual => accrual.Id)
            .Select(accrual => new Row(
                accrual.Id, accrual.ProjectId, accrual.Amount, accrual.AccruedOn, accrual.Description, accrual.ReleasedOn == null, accrual.ReleasedOn))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
