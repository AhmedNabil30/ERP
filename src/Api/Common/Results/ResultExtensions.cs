using Kaff.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Common.Results;

/// <summary>
/// Turns a domain <see cref="Result"/> into an HTTP response, in one place.
/// </summary>
/// <remarks>
/// <para>
/// The response body is a ProblemDetails carrying the error <c>code</c> and its <c>messageKey</c>.
/// The key is what the Angular application translates. CLAUDE.md: "No hardcoded user-facing strings.
/// Everything through i18n from the first commit" — which means the API must not send prose, in
/// Arabic or in English, for the client to display.
/// </para>
/// <para>
/// The status mapping lives here and only here, so a slice cannot decide that its own conflict is a
/// 400 while every other slice returns 409.
/// </para>
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Extension key carrying the stable machine-readable error code.</summary>
    public const string CodeExtension = "code";

    /// <summary>Extension key carrying the i18n key the client resolves for display.</summary>
    public const string MessageKeyExtension = "messageKey";

    public static IResult ToHttpResult(this Result result, IResult? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? onSuccess ?? Microsoft.AspNetCore.Http.Results.NoContent()
            : Problem(result.Error);
    }

    public static IResult ToHttpResult<TValue>(this Result<TValue> result, Func<TValue, IResult>? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            return Problem(result.Error);
        }

        return onSuccess is null
            ? Microsoft.AspNetCore.Http.Results.Ok(result.Value)
            : onSuccess(result.Value);
    }

    public static IResult Problem(Error error) => Problem(error, extraExtensions: null);

    /// <summary>
    /// Same as <see cref="Problem(Error)"/>, with room for a refusal that has to name something
    /// beyond its code and message key — KAFF-213's <c>errors.master.bab_has_active_items</c> names
    /// the count of active items still filed under the باب, so the operator sees what stands in the
    /// way rather than only that the archive failed.
    /// </summary>
    public static IResult Problem(Error error, IReadOnlyDictionary<string, object?>? extraExtensions)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CodeExtension] = error.Code,
            [MessageKeyExtension] = error.MessageKey,
        };

        if (extraExtensions is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extraExtensions)
            {
                extensions[pair.Key] = pair.Value;
            }
        }

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: StatusFor(error.Type),
            title: error.Code,
            type: $"https://kaff.local/errors/{error.Code}",
            extensions: extensions);
    }

    public static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Locked => StatusCodes.Status423Locked,
        _ => StatusCodes.Status500InternalServerError,
    };
}
