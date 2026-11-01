namespace Kaff.Api.Features.DailyLogs.OpenDailyLog;

/// <param name="OccurredAt">Any time on the day to open, with the device's offset. The day is its calendar date.</param>
public sealed record Request(DateTimeOffset? OccurredAt);

public sealed record Response(Guid Id, DateOnly LogDate);
