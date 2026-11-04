using Kaff.Domain.Common;

namespace Kaff.Domain.ChangeOrders;

/// <summary>Error catalogue for change orders (spec.md section 13).</summary>
public static class ChangeOrderErrors
{
    public static readonly Error NotFound =
        Error.NotFound("change_order.not_found", "errors.change_order.not_found");

    public static readonly Error IllegalTransition =
        Error.Conflict("change_order.illegal_transition", "errors.change_order.illegal_transition");

    public static readonly Error ReasonRequired =
        Error.Validation("change_order.reason_required", "errors.change_order.reason_required");

    public static readonly Error ReasonTooLong =
        Error.Validation("change_order.reason_too_long", "errors.change_order.reason_too_long");

    public static readonly Error TitleRequired =
        Error.Validation("change_order.title_required", "errors.change_order.title_required");

    public static readonly Error LinesRequired =
        Error.Validation("change_order.lines_required", "errors.change_order.lines_required");

    public static readonly Error LineInvalid =
        Error.Validation("change_order.line_invalid", "errors.change_order.line_invalid");

    public static readonly Error LinesNotAllowed =
        Error.Validation("change_order.lines_not_allowed", "errors.change_order.lines_not_allowed");

    public static readonly Error ProjectStateNotAllowed =
        Error.Conflict("change_order.project_state_not_allowed", "errors.change_order.project_state_not_allowed");

    public static readonly Error NumberTaken =
        Error.Conflict("change_order.number_taken", "errors.change_order.number_taken");

    public static readonly Error NotWaitingAtThisGate =
        Error.Conflict("change_order.not_waiting_at_this_gate", "errors.change_order.not_waiting_at_this_gate");
}
