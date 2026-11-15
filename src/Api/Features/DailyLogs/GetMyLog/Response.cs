using System.Text.Json.Serialization;
using Kaff.Domain.DailyLogs;

namespace Kaff.Api.Features.DailyLogs.GetMyLog;

/// <summary>
/// One engineer's log for one day. <b>Money-free by construction</b> (KAFF-600 AC-600-J): quantities, hours,
/// counts, photos and materials by type. No member here holds a rate, an amount, a cost or a balance, and the
/// handler projects only these columns, so the response cannot carry one by accident. It is a separate read,
/// not a desk response with fields hidden.
/// </summary>
/// <remarks>
/// <b>Back-dated flag</b> (D-217 item 8). <c>BackDated</c> is present on the log and on every line only for the supervising
/// site engineer and the owner; for anyone else the member is left out of the JSON altogether. A line is back-dated when the
/// day it belongs to is earlier than the day the server received it, in site time.
/// <b>Notes and stoppage reasons</b> are lists: a second one the same day is kept beside the first (D-217 items 12, 16).
/// The weather member is gone (D-217 item 16); stored weather rows are not returned.
/// </remarks>
public sealed record Response(
    Guid Id,
    DateOnly LogDate,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    IReadOnlyList<TextLine> Notes,
    IReadOnlyList<TextLine> StoppageReasons,
    IReadOnlyList<QuantityLine> Quantities,
    IReadOnlyList<DayLabourLine> DayLabour,
    IReadOnlyList<MaterialLine> Materials,
    IReadOnlyList<PhotoLine> Photos,
    IReadOnlyList<ExpenseDraftLine> ExpenseDrafts,
    IReadOnlyList<CheckInLine> CheckIns,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);

/// <summary>
/// One check-in or check-out, with its location and flag (KAFF-613). <c>DeviceTime</c> is when the button was
/// pressed on the phone; <c>ServerTime</c> is when the server received it. Coordinates are degrees, not money.
/// <c>DistanceFromSiteMetres</c> is the whole-metre distance from the project's site reference point, measured by the same
/// <c>GeoDistance</c> that decides the far-from-site flag; null when the project has no reference point.
/// </summary>
public sealed record CheckInLine(
    Guid EntryId,
    DailyLogField Kind,
    DateTimeOffset DeviceTime,
    DateTimeOffset ServerTime,
    double? Latitude,
    double? Longitude,
    double? AccuracyMetres,
    CheckInFlagReason? FlagReason,
    int? DistanceFromSiteMetres);

/// <summary>A note or a stoppage reason. Free text.</summary>
public sealed record TextLine(
    Guid EntryId,
    string Text,
    DateTimeOffset OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);

/// <summary>
/// A saved quantity as one row. <c>Quantity</c> is the corrected figure: the saved quantity plus every signed delta that
/// corrects it (D-218). <c>EntryId</c> is the saved entry, the one a correction names. The correction fields are null until
/// the quantity is corrected, then say who corrected it last and when. <c>PreviousQuantity</c> is the figure in force before
/// that last correction (null when never corrected). <c>CanCorrect</c> says whether this caller may correct
/// the line now: the verdict the correction endpoint would give.
/// </summary>
public sealed record QuantityLine(
    Guid EntryId,
    Guid BoqLineId,
    string? ItemCode,
    string DescriptionAr,
    string Unit,
    decimal Quantity,
    decimal? PreviousQuantity,
    DateTimeOffset OccurredAt,
    bool Corrected,
    Guid? CorrectedByUserId,
    string? CorrectedByName,
    DateTimeOffset? CorrectedAt,
    bool CanCorrect,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);

/// <summary>Crew hours of one man as one row, with the corrected figure, as for <see cref="QuantityLine"/>.</summary>
public sealed record DayLabourLine(
    Guid EntryId,
    Guid EmployeeId,
    string WorkerName,
    string? Trade,
    decimal Hours,
    DateTimeOffset OccurredAt,
    bool Corrected,
    Guid? CorrectedByUserId,
    string? CorrectedByName,
    DateTimeOffset? CorrectedAt,
    bool CanCorrect,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);

public sealed record MaterialLine(
    Guid EntryId,
    string MaterialType,
    decimal Quantity,
    string Unit,
    bool IsStockpile,
    DateTimeOffset OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);

/// <summary>A recorded photo. <c>Url</c> is the route that fetches the file; it is null for a reference that is not a stored id.</summary>
public sealed record PhotoLine(
    Guid EntryId,
    string FileReference,
    string? Caption,
    string? Url,
    bool IsPublished,
    DateTimeOffset OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);

/// <summary>The engineer sees that a draft exists and what it is for. The sum belongs to Accounts.</summary>
public sealed record ExpenseDraftLine(
    Guid EntryId,
    string Description,
    DateTimeOffset OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BackDated);
