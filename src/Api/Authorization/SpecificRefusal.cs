using Kaff.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Authorization;

/// <summary>
/// Carries a refusal more specific than the blanket 401/403 from the gate to the response writer.
/// </summary>
/// <remarks>
/// <para>
/// decisions.md D-071 flattens every gate refusal to <c>errors.auth.not_authenticated</c> /
/// <c>errors.auth.forbidden</c> deliberately, and D-080 ruled that telling a caller which axis of
/// role × assignment failed is a disclosure this system does not make. A must-change-password refusal
/// is a different case: it discloses nothing an attacker could not already infer from holding the
/// credential at all, and the shell needs the distinct key to route to the change-password screen
/// rather than treat it as an ordinary refusal (KAFF-103 AC-103-B).
/// </para>
/// <para>
/// <see cref="HttpContext.Items"/> is the transport because the gate decides inside authorization
/// middleware and the key is stamped later, inside <c>IProblemDetailsService</c> — the same reason
/// <c>IAuditContext</c> is a scoped, per-request channel rather than a return value threaded through
/// the pipeline by hand.
/// </para>
/// </remarks>
internal static class SpecificRefusal
{
    private const string ItemKey = "Kaff.SpecificAuthorizationRefusal";

    public static void Set(HttpContext http, Error error)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(error);

        http.Items[ItemKey] = error;
    }

    public static Error? Get(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return http.Items.TryGetValue(ItemKey, out object? value) ? value as Error : null;
    }
}
