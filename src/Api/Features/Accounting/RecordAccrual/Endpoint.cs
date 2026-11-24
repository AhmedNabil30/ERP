using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
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

namespace Kaff.Api.Features.Accounting.RecordAccrual;

/// <summary>Finance asks to record an expense owed but not yet invoiced. Nothing posts until the Owner approves. KAFF-701 AC1, AC4.</summary>
/// <remarks>
/// The tag is the route, as for <c>PostMovement</c>: the company route tags the company, the project route tags the project and
/// needs an assignment to it. Never both and never neither (spec.md section 6.10): the expense account must be the company expense
/// account on the first route and a project cost account of that project on the second.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/accruals";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/accruals";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CompanyRoute, (
                Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("RecordCompanyAccrual")
            .WithTags("Accounting");

        app.MapPost(ProjectRoute, (
                [FromRoute] Guid projectId, Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute())
            .WithName("RecordProjectAccrual")
            .WithTags("Accounting");
    }
}

/// <param name="AccruedExpenseAccountId">The AccruedExpense account that carries the liability.</param>
/// <param name="ExpenseAccountId">The expense account that takes the cost.</param>
/// <param name="Amount">Always positive.</param>
/// <param name="AccruedOn">The accounting date. Required.</param>
/// <param name="Description">Optional.</param>
public sealed record Request(Guid AccruedExpenseAccountId, Guid ExpenseAccountId, Money Amount, DateOnly? AccruedOn, string? Description);

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid? projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AccountingEntries.SubmitAsync(
            AccountingRequestKind.Accrual,
            projectId,
            new AccrualPayload(request.AccruedExpenseAccountId, request.ExpenseAccountId, request.Amount.Amount, request.AccruedOn, request.Description),
            database, currentUser, rulings, clock, cancellationToken);
    }
}
