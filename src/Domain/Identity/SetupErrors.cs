using Kaff.Domain.Common;

namespace Kaff.Domain.Identity;

/// <summary>
/// Errors raised by the one-time bootstrap screen. KAFF-100.
/// </summary>
public static class SetupErrors
{
    /// <summary>
    /// <c>AC-100-B</c>. The users table already holds a row — of any role, active or not — so the
    /// screen is refused, for good. Rule 4/5: this refusal has no flag behind it, only the emptiness
    /// of the table; rule 6: the database's <c>ux_users_bootstrap_owner_once</c> index is what makes
    /// this the outcome of a race as well as of a plain second call.
    /// </summary>
    public static readonly Error AlreadyCompleted =
        Error.Conflict("setup.already_completed", "errors.setup.already_completed");
}
