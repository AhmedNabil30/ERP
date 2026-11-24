using Kaff.Domain.Common;
using Kaff.Domain.PettyCash;

namespace Kaff.Api.Features.PettyCash;

/// <summary>A عهدة request as read by the supervisor who submits or returns it. It carries the request's own amount, never a project figure.</summary>
public sealed record PettyCashRequestResponse(
    Guid Id,
    Guid ProjectId,
    Guid RequestedByUserId,
    Money Amount,
    string Purpose,
    PettyCashRequestStatus Status,
    DateTimeOffset CreatedAt,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    Guid? ReturnedByUserId,
    DateTimeOffset? ReturnedAt,
    string? ReturnReason)
{
    internal static PettyCashRequestResponse From(PettyCashRequest request) => new(
        request.Id,
        request.ProjectId,
        request.RequestedByUserId,
        request.Amount,
        request.Purpose,
        request.Status,
        request.CreatedAt,
        request.SubmittedByUserId,
        request.SubmittedAt,
        request.ReturnedByUserId,
        request.ReturnedAt,
        request.ReturnReason);
}
