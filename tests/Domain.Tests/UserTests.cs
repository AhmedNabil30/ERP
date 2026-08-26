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

    // ---- CreateBootstrapOwner — KAFF-100 ------------------------------------------------------

    /// <summary>
    /// KAFF-100 rule 2 (no department), rule 7/8 (own password, no forced change) and the
    /// database-enforced half of rule 6 (<see cref="User.IsBootstrapOwner"/>) — all applied by the one
    /// factory rather than assembled correctly by every caller.
    /// </summary>
    [Fact]
    public void CreateBootstrapOwner_produces_an_owner_with_no_department_and_no_forced_change()
    {
        Result<User> created = User.CreateBootstrapOwner(
            "karim",
            "Karim",
            PhoneNumber.Create("01000000090").Value,
            "hashed-password",
            Now);

        created.IsSuccess.Should().BeTrue();

        User owner = created.Value;

        owner.Role.Should().Be(Role.Owner, "rule 2 — the account this screen mints is always the Owner");
        owner.Department.Should().BeNull("rule 2 — the Owner is not one of §9's four departments");
        owner.IsBootstrapOwner.Should().BeTrue("rule 6 — the marker the unique index enforces");
        owner.MustChangePassword.Should().BeFalse(
            "rule 7/8 — SetOwnPassword, not SetTemporaryPassword: he typed it himself");
        owner.PasswordHash.Should().Be("hashed-password");
        owner.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// An ordinary <see cref="User.Create"/>d Owner — the KAFF-106 path — carries no flag. Only the
    /// one row the setup screen itself creates does; a second Owner minted later through the normal
    /// path is not mistaken for it, and the unique index does not block that second Owner from
    /// existing.
    /// </summary>
    [Fact]
    public void An_owner_created_through_the_ordinary_path_does_not_carry_the_bootstrap_flag()
    {
        User user = User.Create(
            "ordinary-owner",
            "Ordinary Owner",
            PhoneNumber.Create("01000000091").Value,
            Role.Owner,
            Now).Value;

        user.IsBootstrapOwner.Should().BeFalse(
            "only CreateBootstrapOwner ever sets this, and nothing here called it");
    }

    /// <summary>Rule 3, reapplied through the same <see cref="User.Create"/> path every account uses.</summary>
    [Fact]
    public void CreateBootstrapOwner_refuses_an_empty_full_name()
    {
        Result<User> created = User.CreateBootstrapOwner(
            "karim",
            string.Empty,
            PhoneNumber.Create("01000000092").Value,
            "hashed-password",
            Now);

        created.IsFailure.Should().BeTrue();
        created.Error.Should().Be(IdentityErrors.FullNameRequired);
    }

    // ---- SetOwnPassword — KAFF-103 ------------------------------------------------------------

    /// <summary>
    /// AC-103-H. A subcontractor has nothing to change — StorePasswordHash refuses the role before
    /// either public setter can touch PasswordHash, whichever one is called.
    /// </summary>
    /// <remarks>
    /// Exercised at the entity rather than through <c>POST /api/auth/change-password</c>: a
    /// subcontractor can never hold the session that endpoint requires (spec.md §9 — "record only, no
    /// login" — refuses the credential a session would need in the first place), so this is the one
    /// place the rule is reachable at all.
    /// </remarks>
    [Fact]
    public void SetOwnPassword_refuses_a_subcontractor()
    {
        User subcontractor = User.Create(
            "change-password-sub",
            "Subcontractor Record",
            PhoneNumber.Create("01000000098").Value,
            Role.Subcontractor,
            Now).Value;

        Result changed = subcontractor.SetOwnPassword("hashed-password");

        changed.IsFailure.Should().BeTrue();
        changed.Error.Should().Be(IdentityErrors.SubcontractorCannotLogIn);
        subcontractor.PasswordHash.Should().BeNull("a refused change leaves no credential behind");
    }

    /// <summary>
    /// KAFF-103 rule 4. The method the holder calls on themselves clears the forced-change flag and
    /// rotates the stamp — the same rotation every password write already carries, and the reason a
    /// temporary password stops being usable the moment it is replaced (AC-103-C, AC-103-F).
    /// </summary>
    [Fact]
    public void SetOwnPassword_clears_must_change_password_and_rotates_the_stamp()
    {
        User user = User.Create(
            "change-password-holder",
            "Password Holder",
            PhoneNumber.Create("01000000097").Value,
            Role.SiteEngineer,
            Now,
            Department.Operations,
            OperationsSubDepartment.Technical).Value;

        user.SetTemporaryPassword("temporary-hash").IsSuccess.Should().BeTrue();
        user.MustChangePassword.Should().BeTrue("the Owner set this one, so the holder must replace it");

        string stampAfterTemporary = user.SecurityStamp;

        Result changed = user.SetOwnPassword("chosen-hash");

        changed.IsSuccess.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse("the holder just typed this one themselves");
        user.PasswordHash.Should().Be("chosen-hash");
        user.SecurityStamp.Should().NotBe(
            stampAfterTemporary,
            "rule 4 — the change must end every session the temporary credential could still open");
    }

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
}
