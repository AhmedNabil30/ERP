using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.CreatePrepayment;

/// <summary>
/// Finance schedules the release of an advance payment to expense, month by month. KAFF-701 AC2, AC4.
/// The payment itself is an existing posting into the Prepayment account; the schedule names it. The schedule writes no posting.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/prepayments";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/prepayments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CompanyRoute, (Request request, KaffDbContext database, ICurrentUser currentUser, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("CreateCompanyPrepayment")
            .WithTags("Accounting");

        app.MapPost(ProjectRoute, ([FromRoute] Guid projectId, Request request, KaffDbContext database, ICurrentUser currentUser, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute())
            .WithName("CreateProjectPrepayment")
            .WithTags("Accounting");
    }
}

/// <param name="PrepaymentAccountId">The Prepayment account that holds the advance payment.</param>
/// <param name="ExpenseAccountId">The expense account each month moves to.</param>
/// <param name="FundingPostingId">The posting that paid the amount into the prepayment account.</param>
/// <param name="TotalAmount">Must equal the funding posting.</param>
/// <param name="Months">1 to 120.</param>
/// <param name="FirstMonth">Any date in the first month charged.</param>
/// <param name="Description">Optional.</param>
public sealed record Request(
    Guid PrepaymentAccountId, Guid ExpenseAccountId, Guid FundingPostingId, Money TotalAmount, int Months, DateOnly? FirstMonth, string? Description);

public sealed record Response(Guid Id, Guid? ProjectId, Money TotalAmount, int Months, DateOnly FirstMonth);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid? projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (request.FirstMonth is not { } firstMonth || firstMonth < TypedPostingDate.Earliest)
        {
            return ResultExtensions.Problem(AccountingErrors.MonthInvalid);
        }

        Account? prepayment = await database.Accounts.FirstOrDefaultAsync(account => account.Id == request.PrepaymentAccountId, cancellationToken);
        Account? expense = await database.Accounts.FirstOrDefaultAsync(account => account.Id == request.ExpenseAccountId, cancellationToken);
        Posting? funding = await database.Postings.FirstOrDefaultAsync(posting => posting.Id == request.FundingPostingId, cancellationToken);

        if (prepayment is null || expense is null)
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        // A funding posting that has been reversed no longer stands, so it cannot be scheduled.
        if (funding is null || await database.Postings.AnyAsync(posting => posting.ReversesId == funding.Id, cancellationToken))
        {
            return ResultExtensions.Problem(AccountingErrors.PrepaymentFundingMismatch);
        }

        Result<PrepaymentSchedule> created = PrepaymentSchedule.Create(
            projectId, prepayment, expense, funding, request.TotalAmount, request.Months, firstMonth, request.Description,
            currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        database.PrepaymentSchedules.Add(created.Value);

        Error? guard = await GuardedSave.SaveAsync(database, cancellationToken);
        if (guard is not null)
        {
            return ResultExtensions.Problem(guard);
        }

        PrepaymentSchedule schedule = created.Value;

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/treasury/prepayments/{schedule.Id}",
            new Response(schedule.Id, schedule.ProjectId, schedule.TotalAmount, schedule.Months, schedule.FirstMonth));
    }
}
