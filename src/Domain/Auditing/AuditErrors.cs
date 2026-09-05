using Kaff.Domain.Common;

namespace Kaff.Domain.Auditing;

/// <summary>Error catalogue for the audit trail. KAFF-117.</summary>
public static class AuditErrors
{
    /// <summary>
    /// The requested window ends before it starts.
    /// </summary>
    /// <remarks>
    /// <b>Refused rather than answered with nothing.</b> An inverted range matches no row, so
    /// defaulting it would render as an empty trail — and an empty trail is what the Owner sees when
    /// nothing happened. The two must not look alike on the one screen whose purpose is to settle a
    /// disagreement about whether something happened. Same reasoning as
    /// <c>ClientListFilterParsing</c>'s unknown status: absent is a default, wrong is a mistake, and
    /// they must not produce the same list.
    /// </remarks>
    public static readonly Error DateRangeInverted =
        Error.Validation("audit.date_range_inverted", "errors.audit.date_range_inverted");
}
