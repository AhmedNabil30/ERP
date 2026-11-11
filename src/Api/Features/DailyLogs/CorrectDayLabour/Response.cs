namespace Kaff.Api.Features.DailyLogs.CorrectDayLabour;

/// <summary>What the saved entry reads after the correction, and who corrected it when.</summary>
public sealed record Response(Guid CorrectionId, Guid CorrectsEntryId, decimal Hours, Guid CorrectedByUserId, DateTimeOffset CorrectedAt);
