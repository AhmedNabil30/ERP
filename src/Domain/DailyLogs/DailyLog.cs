using Kaff.Domain.Boqs;
using Kaff.Domain.Common;

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
    /// The day an entry belongs to: the calendar date in the offset the engineer's device reported. 23:50 at
    /// +02:00 is that day, whenever the server hears of it (KAFF-600 rule 11).
    /// </summary>
    public static DateOnly DayOf(DateTimeOffset occurredAt) => DateOnly.FromDateTime(occurredAt.DateTime);

    public static DailyLog Open(Guid projectId, Guid engineerId, DateTimeOffset occurredAt, DateTimeOffset now) =>
        new(projectId, engineerId, DayOf(occurredAt), now);

    /// <summary>
    /// Records an executed quantity against a BOQ line. The caller applies the entry to
    /// <see cref="BoqLine.QuantityExecuted"/> under a lock, using <c>RulingSet.DailyLogQuantityEntry</c>.
    /// </summary>
    public Result<ExecutedQuantityEntry> RecordQuantity(
        Guid entryId, Boq boq, BoqLine line, decimal quantity, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(boq);
        ArgumentNullException.ThrowIfNull(line);

        Result onDay = OnThisDay(occurredAt);
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

    public Result<DayLabourEntry> RecordDayLabour(Guid entryId, Guid employeeId, decimal hours, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt);
        if (onDay.IsFailure)
        {
            return Result.Failure<DayLabourEntry>(onDay.Error);
        }

        if (hours <= 0m || hours > DayLabourEntry.MaxHours)
        {
            return Result.Failure<DayLabourEntry>(DailyLogErrors.HoursOutOfRange);
        }

        return Result.Success(new DayLabourEntry(entryId, this, employeeId, hours, occurredAt, now));
    }

    public Result<MaterialEntry> RecordMaterial(
        Guid entryId, string? materialType, decimal quantity, string? unit, bool isStockpile, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt);
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

    public Result<PhotoEntry> RecordPhoto(Guid entryId, string? fileReference, string? caption, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt);
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
    public Result<FieldEntry> RecordField(Guid entryId, DailyLogField field, string? text, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt);
        if (onDay.IsFailure)
        {
            return Result.Failure<FieldEntry>(onDay.Error);
        }

        if (!Enum.IsDefined(field))
        {
            return Result.Failure<FieldEntry>(DailyLogErrors.UnknownField);
        }

        if (field is DailyLogField.CheckIn or DailyLogField.CheckOut)
        {
            return Result.Success(new FieldEntry(entryId, this, field, null, occurredAt, now));
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
    /// An engineer's expense, stored as a draft. No posting is created or referenced: Accounts confirms and
    /// posts it (spec.md section 8).
    /// </summary>
    public Result<SiteExpenseDraft> DraftExpense(Guid entryId, string? description, Money amount, DateTimeOffset occurredAt, DateTimeOffset now)
    {
        Result onDay = OnThisDay(occurredAt);
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

    private Result OnThisDay(DateTimeOffset occurredAt) =>
        DayOf(occurredAt) == LogDate ? Result.Success() : Result.Failure(DailyLogErrors.EntryOnAnotherDay);
}
