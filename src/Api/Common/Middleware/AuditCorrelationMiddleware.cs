using Kaff.Infrastructure.Auditing;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Common.Middleware;

/// <summary>
/// Gives every request a correlation id and hands it, with the request path, to the audit context.
/// </summary>
/// <remarks>
/// One approval touches several records — the extract, its postings, the ledger entries. Sharing a
/// correlation id makes those rows one story rather than several coincidences with adjacent
/// timestamps. Without it, reconstructing what a user actually did means comparing clocks.
///
/// An inbound correlation header is honoured so a trail can be followed from the mobile app or the
/// Angular client through the API.
/// </remarks>
public sealed class AuditCorrelationMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public AuditCorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AuditContext auditContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(auditContext);

        Guid correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues header)
            && Guid.TryParse(header.ToString(), out Guid inbound)
                ? inbound
                : Guid.CreateVersion7();

        auditContext.BindToRequest(correlationId, context.Request.Path.Value);
        context.Response.Headers[HeaderName] = correlationId.ToString();

        await _next(context);
    }
}
