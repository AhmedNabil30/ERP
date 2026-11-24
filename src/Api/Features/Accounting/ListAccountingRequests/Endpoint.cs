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

namespace Kaff.Api.Features.Accounting.ListAccountingRequests;

/// <summary>
/// The accounting requests of one tag, newest first, with what each asks for and its outcome. <c>?status=Requested</c> keeps the ones
/// still waiting for the Owner. The company route lists company-tagged requests; a project's requests are read through that
/// project's route and its assignment.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/accounting-requests";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/accounting-requests";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(CompanyRoute, (KaffDbContext database, AccountingRequestStatus? status, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, status, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ListCompanyAccountingRequests")
            .WithTags("Accounting");

        app.MapGet(ProjectRoute, ([FromRoute] Guid projectId, KaffDbContext database, AccountingRequestStatus? status, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, status, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("ListProjectAccountingRequests")
            .WithTags("Accounting");
    }
}

public sealed record Row(
    Guid Id,
    AccountingRequestKind Kind,
    Guid? ProjectId,
    AccountingRequestStatus Status,
    string Payload,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    string? RejectionReason,
    Money? Amount,
    int? LineCount,
    string AskedByName,
    string? DecidedByName);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid? projectId, AccountingRequestStatus? status, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        IQueryable<AccountingRequest> query = database.AccountingRequests.Where(request => request.ProjectId == projectId);

        if (status is not null)
        {
            query = query.Where(request => request.Status == status);
        }

        List<Row> rows = await query
            .OrderByDescending(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .Select(request => new Row(
                request.Id, request.Kind, request.ProjectId, request.Status, request.Payload, request.RequestedByUserId,
                request.RequestedAt, request.DecidedByUserId, request.DecidedAt, request.RejectionReason, request.Amount, request.LineCount,
                database.Users.Where(person => person.Id == request.RequestedByUserId).Select(person => person.FullName).FirstOrDefault() ?? string.Empty,
                database.Users.Where(person => person.Id == request.DecidedByUserId).Select(person => person.FullName).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
