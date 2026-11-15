using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.DailyLogs;

/// <summary>
/// One engineer's log for one project on one day (spec.md section 8, KAFF-600). The root holds identity only;
/// every recording is an immutable <see cref="LogEntry"/> that this class validates and creates. Nothing
/// here ever holds a running total: the figures are derived from entries, and the one running figure that
/// exists, <see cref="BoqLine.QuantityExecuted"/>, is maintained by the handler under a row lock.
/// </summary>
public sealed class DailyLog : Entity
{
    public const int MaxTextLength = 2000;

    private DailyLog()
    {
    }

    private DailyLog(Guid projectId, Guid engineerId, DateOnly logDate, DateTimeOffset createdAt)
        : base(NewId())
    {
        ProjectId = projectId;
        EngineerId = engineerId;
        LogDate = logDate;
        CreatedAt = createdAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid EngineerId { get; private set; }

    /// <summary>The day the work happened, taken from the entry's own time and never from the sync time.</summary>
    public DateOnly LogDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The day an entry belongs to: the calendar day of the instant in the site time zone (D-217, D-218). The offset the
    /// device wrote does not matter: the same instant is the same day, whenever the server hears of it (KAFF-600 rule 11).
    /// </summary>
    public static DateOnly DayOf(DateTimeOffset occurredAt, DailyLogEntryRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.SiteDate(occurredAt);
    }

    public static DailyLog Open(Guid projectId, Guid engineerId, DateTimeOffset occurredAt, DateTimeOffset now, DailyLogEntryRule rule) =>
        new(projectId, engineerId, DayOf(occurredAt, rule), now);

    /// <summary>
    /// Records an executed quantity against a BOQ line. The caller applies the entry to
    /// <see cref="BoqLine.QuantityExecuted"/> under a lock, using <c>RulingSet.DailyLogQuantityEntry</c>.
    /// </summary>
    public Result<ExecutedQuantityEntry> RecordQuantity(
        Guid entryId, Boq boq, BoqLine line, decimal quantity, DailyLogEntryRule rule, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(boq);
        ArgumentNullException.ThrowIfNull(line);

        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<ExecutedQuantityEntry>(onDay.Error);
        }

        if (boq.ProjectId != ProjectId || line.BoqId != boq.Id)
        {
            return Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.BoqLineNotFound);
        }

        if (!boq.IsFrozen)
        {
            return Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.BoqNotFrozen);
        }

        if (quantity <= 0m)
        {
            return Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.QuantityMustBePositive);
        }

        return Result.Success(new ExecutedQuantityEntry(entryId, this, line.Id, quantity, occurredAt, now));
    }

    /// <summary>
    /// Records crew hours. <paramref name="hoursAlreadyRecorded"/> is what this man's entries in this log already count for,
    /// corrections included: one man works at most 16 hours a day (D-217 item 1).
    /// </summary>
    public Result<DayLabourEntry> RecordDayLabour(
        Guid entryId, Guid employeeId, decimal hours, decimal hoursAlreadyRecorded, DailyLogEntryRule rule, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rule);

        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<DayLabourEntry>(onDay.Error);
        }

        if (!rule.HoursAllowed(hours) || hoursAlreadyRecorded + hours > rule.MaxCrewHours)
        {
            return Result.Failure<DayLabourEntry>(DailyLogErrors.HoursOutOfRange);
        }

        return Result.Success(new DayLabourEntry(entryId, this, employeeId, hours, occurredAt, now));
    }

    /// <summary>
    /// Corrects a saved quantity (D-218). Nothing is updated: the result is a new signed delta that names the entry it
    /// corrects, on the same day and log as that entry. <paramref name="currentFigure"/> is what the entry counts for now.
    /// </summary>
    public Result<ExecutedQuantityEntry> CorrectQuantity(
        Guid entryId, ExecutedQuantityEntry original, decimal currentFigure, decimal newFigure, Guid correctedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (original.DailyLogId != Id)
        {
            return Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.EntryNotFound);
        }

        if (original.CorrectsEntryId is not null)
        {
            return Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.CannotCorrectACorrection);
        }

        if (newFigure <= 0m)
        {
            return Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.QuantityMustBePositive);
        }

        decimal delta = newFigure - currentFigure;

        return delta == 0m
            ? Result.Failure<ExecutedQuantityEntry>(DailyLogErrors.NothingToCorrect)
            : Result.Success(new ExecutedQuantityEntry(
                entryId, this, original.BoqLineId, delta, original.OccurredAt, now, original.Id, correctedBy));
    }

    /// <summary>
    /// Corrects saved crew hours (D-218, KAFF-615 AC-11): a new signed delta that names the entry it corrects.
    /// <paramref name="hoursOfOtherEntries"/> is what this man's other entries in the log count for.
    /// </summary>
    public Result<DayLabourEntry> CorrectDayLabour(
        Guid entryId,
        DayLabourEntry original,
        decimal currentFigure,
        decimal newFigure,
        decimal hoursOfOtherEntries,
        DailyLogEntryRule rule,
        Guid correctedBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(rule);

        if (original.DailyLogId != Id)
        {
            return Result.Failure<DayLabourEntry>(DailyLogErrors.EntryNotFound);
        }

        if (original.CorrectsEntryId is not null)
        {
            return Result.Failure<DayLabourEntry>(DailyLogErrors.CannotCorrectACorrection);
        }

        if (!rule.HoursAllowed(newFigure) || hoursOfOtherEntries + newFigure > rule.MaxCrewHours)
        {
            return Result.Failure<DayLabourEntry>(DailyLogErrors.HoursOutOfRange);
        }

        decimal delta = newFigure - currentFigure;

        return delta == 0m
            ? Result.Failure<DayLabourEntry>(DailyLogErrors.NothingToCorrect)
            : Result.Success(new DayLabourEntry(
                entryId, this, original.EmployeeId, delta, original.OccurredAt, now, original.Id, correctedBy));
    }

    public Result<MaterialEntry> RecordMaterial(
        Guid entryId, string? materialType, decimal quantity, string? unit, bool isStockpile, DailyLogEntryRule rule, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<MaterialEntry>(onDay.Error);
        }

        if (string.IsNullOrWhiteSpace(materialType) || string.IsNullOrWhiteSpace(unit))
        {
            return Result.Failure<MaterialEntry>(DailyLogErrors.TextRequired);
        }

        if (materialType.Length > MaterialEntry.MaxLength || unit.Length > MaterialEntry.MaxLength)
        {
            return Result.Failure<MaterialEntry>(DailyLogErrors.TextTooLong);
        }

        if (quantity <= 0m)
        {
            return Result.Failure<MaterialEntry>(DailyLogErrors.QuantityMustBePositive);
        }

        return Result.Success(new MaterialEntry(entryId, this, materialType.Trim(), quantity, unit.Trim(), isStockpile, occurredAt, now));
    }

    public Result<PhotoEntry> RecordPhoto(Guid entryId, string? fileReference, string? caption, DailyLogEntryRule rule, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<PhotoEntry>(onDay.Error);
        }

        if (string.IsNullOrWhiteSpace(fileReference))
        {
            return Result.Failure<PhotoEntry>(DailyLogErrors.FileReferenceRequired);
        }

        if (fileReference.Length > MaxTextLength || caption?.Length > MaxTextLength)
        {
            return Result.Failure<PhotoEntry>(DailyLogErrors.TextTooLong);
        }

        return Result.Success(new PhotoEntry(entryId, this, fileReference.Trim(), string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(), occurredAt, now));
    }

    /// <summary>
    /// Records a single-valued field. Check-in and check-out are the entry's own time and take no text; the
    /// others require text.
    /// </summary>
    public Result<FieldEntry> RecordField(Guid entryId, DailyLogField field, string? text, DailyLogEntryRule rule, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<FieldEntry>(onDay.Error);
        }

        if (!Enum.IsDefined(field))
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.UnknownField);
        }

        if (field == DailyLogField.Weather)
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.WeatherRetired);
        }

        if (field is DailyLogField.CheckIn or DailyLogField.CheckOut)
        {
            // This path carries no location, and check-in needs one (Q-207-4, D-208). Callers use RecordCheckIn.
            return Result.Failure<FieldEntry>(DailyLogErrors.CheckInUsesCheckInRoute);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.TextRequired);
        }

        if (text.Length > MaxTextLength)
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.TextTooLong);
        }

        return Result.Success(new FieldEntry(entryId, this, field, text.Trim(), occurredAt, now));
    }

    /// <summary>
    /// Records a check-in or check-out with the phone's location (KAFF-613). A missing or unusable location is
    /// refused with <see cref="DailyLogErrors.LocationRequired"/> and nothing is recorded (Q-207-4, D-208). A location
    /// farther from <paramref name="reference"/> than the rule allows is recorded and flagged
    /// <see cref="CheckInFlagReason.FarFromReference"/> (Q-207-3, D-208). <paramref name="occurredAt"/> is the device time and
    /// <paramref name="now"/> the server time; neither replaces the other.
    /// </summary>
    public Result<FieldEntry> RecordCheckIn(
        Guid entryId,
        DailyLogField kind,
        double? latitude,
        double? longitude,
        double? accuracyMetres,
        (double Latitude, double Longitude)? reference,
        CheckInDistanceRule distanceRule,
        DailyLogEntryRule rule,
        DateTimeOffset occurredAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(distanceRule);

        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<FieldEntry>(onDay.Error);
        }

        if (kind is not (DailyLogField.CheckIn or DailyLogField.CheckOut))
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.UnknownField);
        }

        bool located = latitude is { } lat && longitude is { } lng
            && double.IsFinite(lat) && double.IsFinite(lng) && GeoDistance.IsValid(lat, lng);

        if (!located)
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.LocationRequired);
        }

        double? accuracy = accuracyMetres is { } a && double.IsFinite(a) && a >= 0d ? a : null;

        CheckInFlagReason? flag = reference is { } point
            && distanceRule.IsFar(GeoDistance.Metres(latitude!.Value, longitude!.Value, point.Latitude, point.Longitude))
                ? CheckInFlagReason.FarFromReference
                : null;

        return Result.Success(new FieldEntry(
            entryId, this, kind, null, occurredAt, now, latitude, longitude, accuracy, flag));
    }

    /// <summary>
    /// An engineer's expense, stored as a draft. No posting is created or referenced: Accounts confirms and
    /// posts it (spec.md section 8).
    /// </summary>
    public Result<SiteExpenseDraft> DraftExpense(Guid entryId, string? description, Money amount, DailyLogEntryRule rule, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt, rule);
        if (onDay.IsFailure)
        {
            return Result.Failure<SiteExpenseDraft>(onDay.Error);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<SiteExpenseDraft>(DailyLogErrors.TextRequired);
        }

        if (description.Length > MaxTextLength)
        {
            return Result.Failure<SiteExpenseDraft>(DailyLogErrors.TextTooLong);
        }

        if (!amount.IsPositive)
        {
            return Result.Failure<SiteExpenseDraft>(DailyLogErrors.ExpenseMustBePositive);
        }

        return Result.Success(new SiteExpenseDraft(entryId, this, description.Trim(), amount, occurredAt, now));
    }

    private Result OnThisDay(DateTimeOffset occurredAt, DailyLogEntryRule rule) =>
        DayOf(occurredAt, rule) == LogDate ? Result.Success() : Result.Failure(DailyLogErrors.EntryOnAnotherDay);
}
