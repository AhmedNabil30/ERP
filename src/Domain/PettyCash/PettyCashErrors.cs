using Kaff.Domain.Common;

namespace Kaff.Domain.PettyCash;

/// <summary>Refusals of the عهدة request (KAFF-609).</summary>
public static class PettyCashErrors
{
    public static readonly Error RequestNotFound =
        Error.NotFound("petty_cash.request_not_found", "errors.petty_cash.request_not_found");

    /// <summary>Only a draft may be submitted or returned.</summary>
    public static readonly Error NotDraft =
        Error.Conflict("petty_cash.not_draft", "errors.petty_cash.not_draft");

    /// <summary>A return always carries a written reason (CLAUDE.md: rejections return to origin with a reason).</summary>
    public static readonly Error ReasonRequired =
        Error.Validation("petty_cash.reason_required", "errors.project.reason_required");

    public static readonly Error AmountInvalid =
        Error.Validation("petty_cash.amount_invalid", "errors.petty_cash.amount_invalid");

    public static readonly Error PurposeRequired =
        Error.Validation("petty_cash.purpose_required", "errors.petty_cash.purpose_required");
}
