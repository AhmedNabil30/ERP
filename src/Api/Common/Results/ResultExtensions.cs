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

    public static IResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: StatusFor(error.Type),
            title: error.Code,
            type: $"https://kaff.local/errors/{error.Code}",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [CodeExtension] = error.Code,
                [MessageKeyExtension] = error.MessageKey,
            });
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
