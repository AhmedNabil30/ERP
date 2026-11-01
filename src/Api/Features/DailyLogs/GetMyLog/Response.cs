namespace Kaff.Api.Features.DailyLogs.GetMyLog;

/// <summary>
/// The engineer's log for one day. <b>Money-free by construction</b> (KAFF-600 AC-600-J): quantities, hours,
/// counts, photos and materials by type. No member here holds a rate, an amount, a cost or a balance, and the
/// handler projects only these columns, so the response cannot carry one by accident. It is a separate read,
/// not a desk response with fields hidden.
/// </summary>
public sealed record Response(
    Guid Id,
    DateOnly LogDate,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    string? Weather,
    string? Note,
    string? StoppageReason,
    IReadOnlyList<QuantityLine> Quantities,
    IReadOnlyList<DayLabourLine> DayLabour,
    IReadOnlyList<MaterialLine> Materials,
    IReadOnlyList<PhotoLine> Photos,
    IReadOnlyList<ExpenseDraftLine> ExpenseDrafts);

public sealed record QuantityLine(
    Guid EntryId, Guid BoqLineId, string? ItemCode, string DescriptionAr, string Unit, decimal Quantity, DateTimeOffset OccurredAt);

public sealed record DayLabourLine(Guid EntryId, Guid EmployeeId, string WorkerName, decimal Hours, DateTimeOffset OccurredAt);

public sealed record MaterialLine(
    Guid EntryId, string MaterialType, decimal Quantity, string Unit, bool IsStockpile, DateTimeOffset OccurredAt);

public sealed record PhotoLine(Guid EntryId, string FileReference, string? Caption, bool IsPublished, DateTimeOffset OccurredAt);

/// <summary>The engineer sees that a draft exists and what it is for. The sum is Accounts' to see.</summary>
public sealed record ExpenseDraftLine(Guid EntryId, string Description, DateTimeOffset OccurredAt);
