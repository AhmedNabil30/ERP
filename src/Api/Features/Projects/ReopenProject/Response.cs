namespace Kaff.Api.Features.Projects.ReopenProject;

/// <summary>The project after reopening: when the new follow-up period started and the day it ends.</summary>
public sealed record Response(Guid ProjectId, DateTimeOffset FollowUpStartedOn, DateOnly? FollowUpEndsOn);
