using System.Diagnostics.CodeAnalysis;

namespace Kaff.Domain.Common;

/// <summary>
/// Classification of a domain error. The Api maps this to an HTTP status code in one place.
/// </summary>
public enum ErrorType
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>The request is malformed or fails a field-level rule. 400.</summary>
    Validation = 1,

    /// <summary>The referenced record does not exist. 404.</summary>
    NotFound = 2,

    /// <summary>A business rule forbids the operation in the current state. 409.</summary>
    Conflict = 3,

    /// <summary>The caller is authenticated but not permitted. 403.</summary>
    Forbidden = 4,

    /// <summary>The caller is not authenticated. 401.</summary>
    Unauthenticated = 5,
}

/// <summary>
/// A domain error. <paramref name="Code"/> is a stable machine identifier and
/// <paramref name="MessageKey"/> is an i18n key — never a user-facing sentence.
/// CLAUDE.md forbids hardcoded user-facing strings anywhere in the system.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "CA1716 protects cross-language consumers — 'Error' is a Visual Basic keyword. " +
                    "This solution is C# only and has no other-language consumers. CLAUDE.md names " +
                    "this concept 'domain errors are Result<T>', and renaming it to DomainError to " +
                    "satisfy a VB interop rule would put a synonym into a codebase whose vocabulary " +
                    "is deliberately fixed.")]
public sealed record Error(string Code, string MessageKey, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string messageKey) => new(code, messageKey, ErrorType.Validation);

    public static Error NotFound(string code, string messageKey) => new(code, messageKey, ErrorType.NotFound);

    public static Error Conflict(string code, string messageKey) => new(code, messageKey, ErrorType.Conflict);

    public static Error Forbidden(string code, string messageKey) => new(code, messageKey, ErrorType.Forbidden);

    public static Error Unauthenticated(string code, string messageKey) => new(code, messageKey, ErrorType.Unauthenticated);
}
