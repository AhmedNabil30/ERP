using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.ListPrepayments;

/// <summary>The prepayment schedules of one tag, with what has moved to expense and what is left. Both are derived from postings. KAFF-701 AC2.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/prepayments";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/prepayments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(CompanyRoute, (KaffDbContext database, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ListCompanyPrepayments")
            .WithTags("Accounting");

        app.MapGet(ProjectRoute, ([FromRoute] Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("ListProjectPrepayments")
            .WithTags("Accounting");
    }
}

public sealed record Row(Guid Id, Guid? ProjectId, string? Description, Money TotalAmount, int Months, DateOnly FirstMonth, Money Amortised, Money Remaining);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid? projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<PrepaymentSchedule> schedules = await database.PrepaymentSchedules
            .Where(schedule => schedule.ProjectId == projectId)
            .OrderBy(schedule => schedule.FirstMonth)
            .ThenBy(schedule => schedule.Id)
            .ToListAsync(cancellationToken);

        List<Guid> ids = [.. schedules.Select(schedule => schedule.Id)];

        List<Posting> postings = await database.Postings
            .Where(posting => posting.Type == PostingType.PrepaymentAmortisation
                              && posting.SourceDocumentType == SourceDocumentType.CompanyExpense
                              && ids.Contains(posting.SourceDocumentId))
            .ToListAsync(cancellationToken);

        List<Row> rows =
        [
            .. schedules.Select(schedule =>
            {
                Money amortised = ScheduleLedger.Total(ScheduleLedger.Active(postings.Where(posting => posting.SourceDocumentId == schedule.Id)));

                return new Row(
                    schedule.Id, schedule.ProjectId, schedule.Description, schedule.TotalAmount, schedule.Months, schedule.FirstMonth,
                    amortised, schedule.TotalAmount - amortised);
            }),
        ];

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
