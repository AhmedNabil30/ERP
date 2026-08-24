using System.Security.Claims;
using System.Text.Encodings.Web;
using Kaff.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// Signs the caller in from request headers, so tests need no token issuer.
/// </summary>
/// <remarks>
/// This replaces authentication only. Authorization runs exactly as it does in production: the same
/// policy provider, the same handler, the same <see cref="Domain.Authorization.PermissionEvaluator"/>,
/// and the same database-backed assignment lookup. A test that passes here has passed the real gate.
/// </remarks>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";
    public const string DepartmentHeader = "X-Test-Department";
    public const string SubDepartmentHeader = "X-Test-SubDepartment";
    public const string ClientIdHeader = "X-Test-ClientId";

    /// <summary>
    /// The security stamp the caller's "token" was issued against.
    /// </summary>
    /// <remarks>
    /// Deliberately has no default. <see cref="IPermissionSubjectReader"/> refuses a request whose
    /// stamp is missing or stale, and giving the test double a stamp that always matches would
    /// disable the global sign-out of decisions.md D-051 for the entire suite — a harness that
    /// reports safety the product does not have. See decisions.md D-053.
    /// </remarks>
    public const string SecurityStampHeader = "X-Test-SecurityStamp";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out Microsoft.Extensions.Primitives.StringValues userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims =
        [
            new(KaffClaimTypes.UserId, userId.ToString()),
            new(KaffClaimTypes.DisplayName, $"test-user-{userId}"),
        ];

        AddIfPresent(claims, KaffClaimTypes.Role, RoleHeader);
        AddIfPresent(claims, KaffClaimTypes.Department, DepartmentHeader);
        AddIfPresent(claims, KaffClaimTypes.OperationsSubDepartment, SubDepartmentHeader);
        AddIfPresent(claims, KaffClaimTypes.ClientId, ClientIdHeader);
        AddIfPresent(claims, KaffClaimTypes.SecurityStamp, SecurityStampHeader);

        var identity = new ClaimsIdentity(claims, SchemeName, KaffClaimTypes.DisplayName, KaffClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    private void AddIfPresent(List<Claim> claims, string claimType, string headerName)
    {
        if (Request.Headers.TryGetValue(headerName, out Microsoft.Extensions.Primitives.StringValues value)
            && !string.IsNullOrWhiteSpace(value.ToString()))
        {
            claims.Add(new Claim(claimType, value.ToString()));
        }
    }
}
