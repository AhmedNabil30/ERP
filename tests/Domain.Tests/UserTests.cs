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

    // ---- ChangeRole — KAFF-109, decisions.md D-051 (Q27) -------------------------------------------

    /// <summary>
    /// KAFF-109 rule 11: department compatibility is re-applied against the new role, exactly as
    /// <c>Create</c> applies it. An HR user in the wrong department cannot be created; the same user
    /// cannot be turned into one either, without moving the department first (<c>AC-109-G</c>).
    /// </summary>
    [Fact]
    public void ChangeRole_reapplies_the_hr_department_rule()
    {
        User user = User.Create(
            "change-role-marketing",
            "Marketing User",
            PhoneNumber.Create("01000000010").Value,
            Role.MarketingSales,
            Now,
            Department.Marketing).Value;

        Result changed = user.ChangeRole(Role.Hr);

        changed.IsFailure.Should().BeTrue();
        changed.Error.Should().Be(IdentityErrors.HrRoleRequiresHrDepartment);
        user.Role.Should().Be(Role.MarketingSales, "a refused change is not a change");
    }

    /// <summary>KAFF-109 rule 11 — the no-department rule for external roles, reapplied.</summary>
    [Fact]
    public void ChangeRole_reapplies_the_external_role_department_rule()
    {
        User user = User.Create(
            "change-role-finance",
            "Finance User",
            PhoneNumber.Create("01000000011").Value,
            Role.Finance,
            Now,
            Department.Finance).Value;

        Result changed = user.ChangeRole(Role.Client);

        changed.IsFailure.Should().BeTrue(
            "Role.Client cannot hold a department, and this account still carries Department.Finance");
        changed.Error.Should().Be(IdentityErrors.ExternalRoleCannotHoldDepartment);
        user.Role.Should().Be(Role.Finance);
    }

    /// <summary>KAFF-109 rule 11 — the client-id rule for <see cref="Role.Client"/>, reapplied.</summary>
    /// <remarks>
    /// <c>Role.Owner</c> and no department, so <c>ValidateDepartment</c> passes and the client-id
    /// check is the one this test actually exercises — a departmented user would fail on the
    /// no-department rule first, which is <see cref="ChangeRole_reapplies_the_external_role_department_rule"/>'s job.
    /// </remarks>
    [Fact]
    public void ChangeRole_refuses_role_client_with_no_client_id()
    {
        User user = User.Create(
            "change-role-owner",
            "Owner User",
            PhoneNumber.Create("01000000013").Value,
            Role.Owner,
            Now).Value;

        Result changed = user.ChangeRole(Role.Client);

        changed.IsFailure.Should().BeTrue();
        changed.Error.Should().Be(IdentityErrors.ClientUserRequiresClient);
        user.Role.Should().Be(Role.Owner);
    }

    /// <summary>A compatible change succeeds and sets the new role.</summary>
    [Fact]
    public void ChangeRole_succeeds_when_the_new_role_fits_the_existing_department()
    {
        User user = User.Create(
            "change-role-tech",
            "Technical Office User",
            PhoneNumber.Create("01000000012").Value,
            Role.SiteEngineer,
            Now,
            Department.Operations,
            OperationsSubDepartment.Technical).Value;

        Result changed = user.ChangeRole(Role.TechnicalOffice);

        changed.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(Role.TechnicalOffice);
    }

    /// <summary>
    /// KAFF-109 rule 8 — a request naming the role already held succeeds without altering anything.
    /// Not special-cased: re-validating state that was already valid cannot fail.
    /// </summary>
    [Fact]
    public void ChangeRole_to_the_role_already_held_succeeds_and_changes_nothing()
    {
        User user = MakeActiveUser();

        Result changed = user.ChangeRole(Role.SiteEngineer);

        changed.IsSuccess.Should().BeTrue();
        user.Role.Should().Be(Role.SiteEngineer);
    }

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
}
