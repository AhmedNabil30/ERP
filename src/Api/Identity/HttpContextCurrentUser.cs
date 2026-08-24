using System.Security.Claims;
using Kaff.Domain.Identity;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Identity;

/// <summary>
/// Reads the caller's identity from the access token.
/// </summary>
/// <remarks>
/// Every value comes from a validated token claim. Nothing here reads a header, a query parameter or
/// a body field — a caller must never be able to state who they are.
///
/// Token issuance is slice 1. The claim names are fixed in <see cref="KaffClaimTypes"/> so the issuer
/// and this reader cannot drift apart.
/// </remarks>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ReadGuid(KaffClaimTypes.UserId);

    public string DisplayName => Read(KaffClaimTypes.DisplayName) ?? "anonymous";

    public Role? Role => ReadEnum<Role>(KaffClaimTypes.Role);

    public Department? Department => ReadEnum<Department>(KaffClaimTypes.Department);

    public OperationsSubDepartment? OperationsSubDepartment =>
        ReadEnum<OperationsSubDepartment>(KaffClaimTypes.OperationsSubDepartment);

    public Guid? ClientId => ReadGuid(KaffClaimTypes.ClientId);

    public string? SecurityStamp => Read(KaffClaimTypes.SecurityStamp);

    private string? Read(string claimType) => Principal?.FindFirst(claimType)?.Value;

    private Guid? ReadGuid(string claimType) => Guid.TryParse(Read(claimType), out Guid value) ? value : null;

    private TEnum? ReadEnum<TEnum>(string claimType)
        where TEnum : struct, Enum
        => Enum.TryParse(Read(claimType), ignoreCase: false, out TEnum value) ? value : null;
}
