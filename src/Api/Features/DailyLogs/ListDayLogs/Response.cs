using Kaff.Api.Features.DailyLogs.GetMyLog;

namespace Kaff.Api.Features.DailyLogs.ListDayLogs;

/// <summary>One engineer log of the day, in the same money-free shape as the own-log read, with the back-dated flag.</summary>
public sealed record EngineerLog(Guid EngineerId, string EngineerName, Response Log);
