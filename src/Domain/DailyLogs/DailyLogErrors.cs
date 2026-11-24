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

    /// <summary>The material unit is not a key of <see cref="MaterialUnits.Keys"/>.</summary>
    public static readonly Error UnitNotAllowed =
        Error.Validation("daily_log.unit_not_allowed", "errors.daily_log.unit_not_allowed");

    public static readonly Error FileReferenceRequired =
        Error.Validation("daily_log.file_reference_required", "errors.daily_log.file_reference_required");

    /// <summary>The photo reference is not one this project holds: not an id, or an id with no stored file in this project.</summary>
    public static readonly Error FileReferenceInvalid =
        Error.Validation("daily_log.file_reference_invalid", "errors.daily_log.file_reference_invalid");

    /// <summary>The photo does not read cleanly to its end, so its metadata cannot be removed with certainty.</summary>
    public static readonly Error PhotoUnreadable =
        Error.Validation("daily_log.photo_unreadable", "errors.daily_log.photo_unreadable");

    /// <summary>The uploaded bytes are not a JPEG, PNG or WebP, whatever the client called them.</summary>
    public static readonly Error PhotoNotAnImage =
        Error.Validation("daily_log.photo_not_an_image", "errors.daily_log.photo_not_an_image");

    public static readonly Error PhotoTooLarge =
        Error.TooLarge("daily_log.photo_too_large", "errors.daily_log.photo_too_large");

    public static readonly Error PhotoNotFound =
        Error.NotFound("daily_log.photo_not_found", "errors.daily_log.photo_not_found");

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

    /// <summary>No entry is saved before the engineer has checked in today on this project (D-217 item 9).</summary>
    public static readonly Error CheckInRequired =
        Error.Conflict("daily_log.check_in_required", "errors.daily_log.check_in_required");

    /// <summary>A check-in belongs to today. A past day gets no check-in of its own (D-217 item 10).</summary>
    public static readonly Error CheckInOnlyToday =
        Error.Conflict("daily_log.check_in_only_today", "errors.daily_log.check_in_only_today");

    /// <summary>The entry is dated in the future or more days back than the window allows (D-217 item 7).</summary>
    public static readonly Error EntryOutsideWindow =
        Error.Conflict("daily_log.entry_outside_window", "errors.daily_log.entry_outside_window");

    /// <summary>The weather entry kind is retired (D-217 item 16). Weather is written as the stoppage reason.</summary>
    public static readonly Error WeatherRetired =
        Error.Validation("daily_log.weather_retired", "errors.daily_log.weather_retired");

    public static readonly Error EntryNotFound =
        Error.NotFound("daily_log.entry_not_found", "errors.daily_log.entry_not_found");

    /// <summary>The caller may not correct this entry on this day (D-217 item 3, D-218 items 14 and 16).</summary>
    public static readonly Error CorrectionNotAllowed =
        Error.Forbidden("daily_log.correction_not_allowed", "errors.daily_log.correction_not_allowed");

    /// <summary>The day of the entry is outside the open days, so it can no longer be corrected.</summary>
    public static readonly Error CorrectionWindowClosed =
        Error.Conflict("daily_log.correction_window_closed", "errors.daily_log.correction_window_closed");

    public static readonly Error NothingToCorrect =
        Error.Validation("daily_log.nothing_to_correct", "errors.daily_log.nothing_to_correct");

    /// <summary>A correction names the saved entry it corrects, never another correction.</summary>
    public static readonly Error CannotCorrectACorrection =
        Error.Validation("daily_log.cannot_correct_a_correction", "errors.daily_log.cannot_correct_a_correction");
}
