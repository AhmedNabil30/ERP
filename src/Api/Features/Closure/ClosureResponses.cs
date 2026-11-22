using Kaff.Domain.Closure;

namespace Kaff.Api.Features.Closure;

/// <summary>A snag as read. Money-free: a snag carries no cost.</summary>
public sealed record SnagResponse(
    Guid Id,
    int Number,
    string Description,
    string Location,
    SnagOwnerKind OwnerKind,
    Guid? SubcontractorId,
    SnagPriority Priority,
    SnagStatus Status,
    bool BlocksHandover,
    SnagResolutionKind? ResolutionKind,
    string? ResolutionReason,
    DateTimeOffset RaisedAt,
    DateTimeOffset? FixedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ResolvedAt)
{
    internal static SnagResponse From(Snag snag) => new(
        snag.Id,
        snag.Number,
        snag.Description,
        snag.Location,
        snag.OwnerKind,
        snag.SubcontractorId,
        snag.Priority,
        snag.Status,
        snag.BlocksHandover,
        snag.ResolutionKind,
        snag.ResolutionReason,
        snag.RaisedAt,
        snag.FixedAt,
        snag.VerifiedAt,
        snag.ResolvedAt);
}

/// <summary>A warranty callback as read.</summary>
public sealed record WarrantyCallbackResponse(
    Guid Id, int Number, string Description, DateOnly RaisedOn, bool IsResolved, DateTimeOffset? ResolvedAt)
{
    internal static WarrantyCallbackResponse From(WarrantyCallback callback) =>
        new(callback.Id, callback.Number, callback.Description, callback.RaisedOn, callback.IsResolved, callback.ResolvedAt);
}

/// <summary>A hold release request as read (AC-802-L). It carries no amount: the release moves the hold read at approval.</summary>
public sealed record HoldReleaseRequestResponse(
    Guid Id,
    Guid ProjectId,
    HoldReleaseRequestStatus Status,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    string? RejectionReason)
{
    internal static HoldReleaseRequestResponse From(HoldReleaseRequest request) => new(
        request.Id,
        request.ProjectId,
        request.Status,
        request.RequestedByUserId,
        request.RequestedAt,
        request.DecidedByUserId,
        request.DecidedAt,
        request.RejectionReason);
}
