namespace Kaff.Api.Features.DayLabour.OpenEngagement;

/// <summary>
/// The allow-list decisions.md D-140 point 5 names, applied to an engagement: no money member.
/// </summary>
public sealed record Response(Guid Id, Guid WorkerId, Guid ProjectId, DateOnly OpenedOn);
