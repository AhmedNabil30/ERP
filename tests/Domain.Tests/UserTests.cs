using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-112 rule 8 (state transition) and rule 9a (stamp rotation) — <c>User.Reactivate</c>, at the
/// entity level rather than through the API, because both are invariants of the method itself and
/// must hold regardless of what a handler calls beside it.
/// </summary>
public sealed class UserTests
{
    [Fact]
    public void Reactivate_refuses_an_already_active_user()
    {
        User user = MakeActiveUser();

        Result reactivated = user.Reactivate();

        reactivated.IsFailure.Should().BeTrue();
        reactivated.Error.Should().Be(IdentityErrors.UserAlreadyActive);
        user.IsActive.Should().BeTrue("a refused reactivation changes nothing about an already-active account");
    }

    /// <summary>
    /// KAFF-112 rule 8 — clears <see cref="User.DeactivatedAt"/> and sets <see cref="User.IsActive"/>.
    /// </summary>
    [Fact]
    public void Reactivate_clears_deactivated_at_and_sets_is_active()
    {
        User user = MakeActiveUser();
        user.Deactivate(Now).IsSuccess.Should().BeTrue();

        Result reactivated = user.Reactivate();

        reactivated.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.DeactivatedAt.Should().BeNull();
    }

    /// <summary>
    /// KAFF-112 rule 9a, decisions.md D-051 (N5) — "the one path that should rotate and does not".
    /// </summary>
    /// <remarks>
    /// Calls <b>only</b> <see cref="User.Deactivate"/> and <see cref="User.Reactivate"/> — nothing
    /// touches the credential — so this fails specifically if <c>Reactivate</c> stops rotating the
    /// stamp itself, even though <c>ReactivateUser</c>'s handler also rotates it indirectly by calling
    /// <c>ClearPassword</c> on every request. The two are independent invariants and this test is only
    /// honest about the one it names.
    /// </remarks>
    [Fact]
    public void Reactivate_rotates_the_security_stamp_on_its_own()
    {
        User user = MakeActiveUser();
        user.Deactivate(Now).IsSuccess.Should().BeTrue();

        string stampAfterDeactivation = user.SecurityStamp;

        user.Reactivate().IsSuccess.Should().BeTrue();

        user.SecurityStamp.Should().NotBe(
            stampAfterDeactivation,
            "a token minted before this reactivation must not authenticate against the account after it");
    }

    private static User MakeActiveUser()
        => User.Create(
            "reactivate-domain-test",
            "Domain Test User",
            PhoneNumber.Create("01000000099").Value,
            Role.SiteEngineer,
            Now,
            Department.Operations,
            OperationsSubDepartment.Technical).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
}
