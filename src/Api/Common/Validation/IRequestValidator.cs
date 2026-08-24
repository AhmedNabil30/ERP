using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Common.Validation;

/// <summary>
/// Validates one request type. The <c>Validator.cs</c> of a slice.
/// </summary>
/// <remarks>
/// <para>
/// Hand-rolled rather than taken from a package. DataAnnotations emits English sentences as error
/// messages, which CLAUDE.md forbids reaching a user; FluentValidation would be a dependency doing
/// what these forty lines do, and CLAUDE.md forbids adding one for that. Every failure here is an
/// <see cref="Error"/> with an i18n key, exactly like every domain failure, so the client has one
/// way to render both. See decisions.md D-020.
/// </para>
/// <para>
/// Validation is shape-checking: required fields, lengths, ranges. Business rules stay in Domain,
/// where the entity can enforce them regardless of which endpoint reached it.
/// </para>
/// </remarks>
public interface IRequestValidator<in TRequest>
{
    ValueTask<Result> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Runs the registered validator for a request before the handler sees it.
/// </summary>
/// <remarks>
/// Applied per endpoint with <c>.AddEndpointFilter&lt;ValidationFilter&lt;TRequest&gt;&gt;()</c>.
/// Explicit rather than automatic, because a filter that silently applies to everything is one whose
/// absence nobody notices.
///
/// The validator is resolved from the request's scope rather than injected: the filter instance is
/// built once when the route is mapped, so a constructor-injected scoped validator would be captured
/// for the lifetime of the application.
/// </remarks>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        IRequestValidator<TRequest>? validator =
            context.HttpContext.RequestServices.GetService<IRequestValidator<TRequest>>();

        if (validator is null)
        {
            return await next(context);
        }

        TRequest? request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        Result result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return result.IsFailure
            ? ResultExtensions.Problem(result.Error)
            : await next(context);
    }
}
