namespace Kaff.Domain.Common;

/// <summary>Error catalogue for refusals that belong to the wire itself, not to any one feature.</summary>
public static class ApiErrors
{
    /// <summary>
    /// D-151 §6b — a request body that fails to deserialise (a negative <c>Percentage</c>, a malformed
    /// decimal string) surfaces as a bare <c>JsonException</c> → <c>BadHttpRequestException</c> → a
    /// <c>400</c> with no <c>messageKey</c>, because <c>CustomizeProblemDetails</c> otherwise stamps a
    /// key only for <c>401</c>, <c>403</c> and a <c>SpecificRefusal</c>. This is that gap closed for
    /// every malformed body on every route, not only the one that found it.
    /// </summary>
    public static readonly Error MalformedBody =
        Error.Validation("wire.malformed_body", "errors.wire.malformed_body");
}
