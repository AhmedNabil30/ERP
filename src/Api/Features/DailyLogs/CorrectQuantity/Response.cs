namespace Kaff.Api.Features.DailyLogs.CorrectQuantity;

/// <summary>What the saved quantity reads after the correction, and who corrected it when.</summary>
public sealed record Response(Guid CorrectionId, Guid CorrectsEntryId, decimal Quantity, Guid CorrectedByUserId, DateTimeOffset CorrectedAt);
