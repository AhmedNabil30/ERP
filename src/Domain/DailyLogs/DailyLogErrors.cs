using Kaff.Domain.Common;

namespace Kaff.Domain.DailyLogs;

/// <summary>Error catalogue for the daily log (spec.md section 8).</summary>
public static class DailyLogErrors
{
    public static readonly Error NotFound =
        Error.NotFound("daily_log.not_found", "errors.daily_log.not_found");

    /// <summary>The project's state, as ruled by <c>RulingSet.DailyLogProjectStates</c>, takes no log.</summary>
    public static readonly Error ProjectStateClosedToLogs =
        Error.Conflict("daily_log.project_state_closed_to_logs", "errors.daily_log.project_state_closed_to_logs");

    /// <summary>The entry's own time falls on another day than the log it was sent to.</summary>
    public static readonly Error EntryOnAnotherDay =
        Error.Conflict("daily_log.entry_on_another_day", "errors.daily_log.entry_on_another_day");

    /// <summary>The client-generated entry id already belongs to an entry of a different log.</summary>
    public static readonly Error EntryIdTaken =
        Error.Conflict("daily_log.entry_id_taken", "errors.daily_log.entry_id_taken");

    /// <summary>Executed quantity reaches a BOQ line only once the BOQ is frozen: before that the line can still be removed.</summary>
    public static readonly Error BoqNotFrozen =
        Error.Conflict("daily_log.boq_not_frozen", "errors.daily_log.boq_not_frozen");

    public static readonly Error BoqLineNotFound =
        Error.NotFound("daily_log.boq_line_not_found", "errors.daily_log.boq_line_not_found");

    public static readonly Error QuantityMustBePositive =
        Error.Validation("daily_log.quantity_must_be_positive", "errors.daily_log.quantity_must_be_positive");

    /// <summary>A correction would take a BOQ line's executed quantity below zero.</summary>
    public static readonly Error ExecutedQuantityBelowZero =
        Error.Conflict("daily_log.executed_quantity_below_zero", "errors.daily_log.executed_quantity_below_zero");

    public static readonly Error HoursOutOfRange =
        Error.Validation("daily_log.hours_out_of_range", "errors.daily_log.hours_out_of_range");

    public static readonly Error WorkerNotFound =
        Error.NotFound("daily_log.worker_not_found", "errors.daily_log.worker_not_found");

    public static readonly Error TextRequired =
        Error.Validation("daily_log.text_required", "errors.daily_log.text_required");

    public static readonly Error TextTooLong =
        Error.Validation("daily_log.text_too_long", "errors.daily_log.text_too_long");

    public static readonly Error FileReferenceRequired =
        Error.Validation("daily_log.file_reference_required", "errors.daily_log.file_reference_required");

    public static readonly Error UnknownField =
        Error.Validation("daily_log.unknown_field", "errors.daily_log.unknown_field");

    public static readonly Error LocationRequired =
        Error.Validation("daily_log.location_required", "errors.daily_log.location_required");

    public static readonly Error CheckInUsesCheckInRoute =
        Error.Validation("daily_log.check_in_uses_check_in_route", "errors.daily_log.check_in_uses_check_in_route");

    public static readonly Error ExpenseMustBePositive =
        Error.Validation("daily_log.expense_must_be_positive", "errors.daily_log.expense_must_be_positive");

    public static readonly Error EntryIdRequired =
        Error.Validation("daily_log.entry_id_required", "errors.daily_log.entry_id_required");

    public static readonly Error OccurredAtRequired =
        Error.Validation("daily_log.occurred_at_required", "errors.daily_log.occurred_at_required");
}
