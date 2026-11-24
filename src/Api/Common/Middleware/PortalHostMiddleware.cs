using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Kaff.Api.Common.Middleware;

/// <summary>
/// KAFF-810 AC-810-A, R5-Q1 default (Rulings/PortalRulings.cs PortalDeployment): when the request arrives on the portal host name,
/// only <c>/api/portal/*</c> answers. Every other path is 404, so the staff API is not reachable through the client's address.
/// </summary>
/// <remarks>
/// The host name is <c>Kaff:PortalHost</c>. Unset means the portal has no host of its own yet and the middleware passes every
/// request through, which is what development, CI and the test host want. The portal's static files are not served by this
/// host yet; when they are, their path prefix is added to the allowed set here and nowhere else.
/// </remarks>
public sealed class PortalHostMiddleware
{
    public const string ConfigurationKey = "Kaff:PortalHost";

    private static readonly PathString PortalPrefix = new("/api/portal");

    private readonly RequestDelegate _next;
    private readonly string? _portalHost;

    public PortalHostMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _next = next;
        string? portalHost = configuration[ConfigurationKey];
        _portalHost = string.IsNullOrWhiteSpace(portalHost) ? null : portalHost.Trim();
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool onPortalHost = _portalHost is not null
            && string.Equals(context.Request.Host.Host, _portalHost, StringComparison.OrdinalIgnoreCase);

        if (onPortalHost && !context.Request.Path.StartsWithSegments(PortalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return _next(context);
    }
}
