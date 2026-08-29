using Kaff.Domain.Auditing;
using Kaff.Domain.Common;

namespace Kaff.Domain.Identity;

/// <summary>
/// A person who can authenticate, or a portal identity for a client.
/// </summary>
/// <remarks>
/// <para>
/// This is not ASP.NET Core Identity. See decisions.md D-011: Identity's store abstraction is a
/// repository over the data layer, which CLAUDE.md forbids, and its role/claim tables duplicate the
/// role × assignment model spec.md §9 requires.
/// </para>
/// <para>
/// Credentials are modelled but not exercised here. Slice 0 builds the authorization mechanism;
/// slice 1 owns registration, login and token issuance.
/// </para>
/// </remarks>
public sealed class User : Entity
{
    /// <summary>
    /// spec.md §9 amendment, decisions.md D-049 ruling 3: "at least 8 characters, no forced
    /// complexity."
    /// </summary>
    /// <remarks>
    /// Lives here rather than in the one feature that reads it today, because three do: creation
    /// (KAFF-106), the forced first-sign-in change (KAFF-103) and a reset. Karim's number, in one
    /// place, so a second feature cannot quietly pick a different one. The domain stores a hash and
    /// never sees the plaintext, so nothing here can enforce it — the endpoint that receives the
    /// plaintext does.
    /// </remarks>
    public const int MinimumPasswordLength = 8;

    private User()
    {
    }

    private User(
        Guid id,
        string userName,
        string fullName,
        PhoneNumber phone,
        Role role,
        Department? department,
        OperationsSubDepartment? operationsSubDepartment,
        Guid? clientId,
        Guid? employeeId,
        string? email,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserName = userName;
        FullName = fullName;
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        Role = role;
        Department = department;
        OperationsSubDepartment = operationsSubDepartment;
        ClientId = clientId;
        EmployeeId = employeeId;
        Email = email;
        CreatedAt = createdAt;
        IsActive = true;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
    }

    /// <summary>Login identifier. Unique, case-insensitive.</summary>
    public string UserName { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string? Email { get; private set; }

    public string PhoneEntered { get; private set; } = null!;

    /// <summary>Digits-only national form. Deduplication and lookup use this.</summary>
    public string PhoneNormalised { get; private set; } = null!;

    public Role Role { get; private set; }

    public Department? Department { get; private set; }

    public OperationsSubDepartment? OperationsSubDepartment { get; private set; }

    /// <summary>Set for <see cref="Identity.Role.Client"/> users. Scopes the portal to one client (spec.md §12).</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>Links a staff login to the single HR record for that person (spec.md §2, §10).</summary>
    public Guid? EmployeeId { get; private set; }

    /// <summary>
    /// Null means the account cannot authenticate with a password. Subcontractor records are
    /// always null — spec.md §9 says "record only, no login".
    /// </summary>
    [AuditRedacted]
    public string? PasswordHash { get; private set; }

    [AuditRedacted]
    public string SecurityStamp { get; private set; } = null!;

    /// <summary>
    /// True when the stored credential was set by somebody else and must be replaced on first
    /// sign-in. spec.md §9 amendment (Karim, 2026-08-21, decisions.md D-049 ruling 4).
    /// </summary>
    /// <remarks>
    /// Non-repudiation, not hygiene: until the user replaces it, two people know the credential that
    /// acts as that account, and the audit trail cannot tell them apart. Set by
    /// <see cref="SetTemporaryPassword"/> and cleared by <see cref="SetOwnPassword"/> — there is no
    /// boolean parameter, because a parameter defaulting to "not forced" would make the dangerous
    /// case the quiet one. The first Owner's exemption (Nabil, decisions.md D-052 §3) is expressed by
    /// the setup screen calling <see cref="SetOwnPassword"/>: he types it himself, so nobody else has
    /// ever known it.
    /// </remarks>
    public bool MustChangePassword { get; private set; }

    /// <summary>
    /// Consecutive failed sign-ins since the last successful one. spec.md §9 amendment (D-049
    /// ruling 3): "an account locks for 15 minutes after 5 consecutive failed attempts."
    /// </summary>
    /// <remarks>
    /// Per account, because that is what the amendment says. 🟡 Whether the lockout should key on
    /// account-and-address instead is an open question (decisions.md D-049) — per account alone means
    /// anyone who knows a username can lock a site engineer out indefinitely, fifteen minutes at a
    /// time. Nothing is built for the second reading until it is ruled on.
    /// </remarks>
    public int FailedSignInAttempts { get; private set; }

    /// <summary>End of the current lockout window, or null when the account is not locked.</summary>
    public DateTimeOffset? LockedOutUntil { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// True for exactly the one row the setup screen creates. KAFF-100.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the mechanism behind KAFF-100 rule 6 — "the check and the insert are one atomic
    /// operation, enforced by the database" — and it is a marker, not a business rule about who may
    /// hold the flag: nothing stops the Owner minting a second <see cref="Identity.Role.Owner"/>
    /// account later through the ordinary <see cref="Create"/> path (KAFF-106), and that account
    /// carries <see langword="false"/> here. Only <see cref="CreateBootstrapOwner"/> ever sets it
    /// true, and it is set once, at construction — there is no setter that flips it later.
    /// </para>
    /// <para>
    /// <c>ux_users_bootstrap_owner_once</c>, a unique index filtered to <c>is_bootstrap_owner =
    /// true</c>, is what makes two concurrent bootstrap requests produce one Owner and one refusal
    /// (<c>AC-100-C</c>) — CLAUDE.md's safe-balance precedent, applied here: "enforced by a database
    /// constraint, not application code." The friendly <c>Users.AnyAsync()</c> check in the handler is
    /// the courtesy path that avoids hashing a password for nothing; this index is the one that
    /// actually holds when two requests arrive in the same instant, because a unique index rejects
    /// the loser's <c>INSERT</c> regardless of what either request's transaction already believed.
    /// </para>
    /// </remarks>
    public bool IsBootstrapOwner { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public PhoneNumber Phone => PhoneNumber.FromStorage(PhoneEntered, PhoneNormalised);

    public static Result<User> Create(
        string userName,
        string fullName,
        PhoneNumber phone,
        Role role,
        DateTimeOffset createdAt,
        Department? department = null,
        OperationsSubDepartment? operationsSubDepartment = null,
        Guid? clientId = null,
        Guid? employeeId = null,
        string? email = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Failure<User>(IdentityErrors.UserNameRequired);
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<User>(IdentityErrors.FullNameRequired);
        }

        Result departmentCheck = ValidateDepartment(role, department, operationsSubDepartment);
        if (departmentCheck.IsFailure)
        {
            return Result.Failure<User>(departmentCheck.Error);
        }

        if (role == Role.Client && clientId is null)
        {
            return Result.Failure<User>(IdentityErrors.ClientUserRequiresClient);
        }

        if (role != Role.Client && clientId is not null)
        {
            return Result.Failure<User>(IdentityErrors.NonClientUserCannotCarryClient);
        }

        var user = new User(
            NewId(),
            userName.Trim().ToLowerInvariant(),
            fullName.Trim(),
            phone,
            role,
            department,
            operationsSubDepartment,
            clientId,
            employeeId,
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            createdAt);

        return Result.Success(user);
    }

    /// <summary>
    /// Creates the one Owner the setup screen mints. KAFF-100.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every invariant KAFF-100 states about this specific row is applied here, once, rather than left
    /// for the handler to assemble correctly: <see cref="Identity.Role.Owner"/>, no department (rule
    /// 2 — the Owner is not one of §9's four departments), <see cref="IsBootstrapOwner"/> set true (the
    /// database-enforced half of rule 6), and the password set through <see cref="SetOwnPassword"/>
    /// rather than <see cref="SetTemporaryPassword"/> (rule 7/8 — he types it himself, so
    /// <see cref="MustChangePassword"/> stays false and there is no boolean parameter for a caller to
    /// get wrong).
    /// </para>
    /// <para>
    /// Reuses <see cref="Create"/> rather than duplicating its checks — the username and full-name
    /// requirements (rule 3) are the same rule for every account, not a second copy that could drift.
    /// </para>
    /// </remarks>
    public static Result<User> CreateBootstrapOwner(
        string userName,
        string fullName,
        PhoneNumber phone,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        Result<User> created = Create(userName, fullName, phone, Role.Owner, createdAt);
        if (created.IsFailure)
        {
            return created;
        }

        User owner = created.Value;
        owner.IsBootstrapOwner = true;

        Result passwordSet = owner.SetOwnPassword(passwordHash);
        if (passwordSet.IsFailure)
        {
            return Result.Failure<User>(passwordSet.Error);
        }

        return Result.Success(owner);
    }

    /// <summary>
    /// Sets a credential the account holder chose themselves. No forced change follows.
    /// </summary>
    /// <remarks>
    /// The first Owner's setup screen, the first-sign-in change, and a self-service reset all land
    /// here: in each the holder types the password and nobody else has ever known it, so the
    /// non-repudiation D-049 ruling 4 protects is not at risk. Slice 1 owns hashing; the domain only
    /// stores the result.
    /// </remarks>
    public Result SetOwnPassword(string passwordHash)
    {
        Result set = StorePasswordHash(passwordHash);
        if (set.IsFailure)
        {
            return set;
        }

        MustChangePassword = false;
        return Result.Success();
    }

    /// <summary>
    /// Sets a credential on behalf of somebody else. The holder MUST replace it on first sign-in.
    /// </summary>
    /// <remarks>
    /// spec.md §9 amendment, D-049 ruling 4 — onboarding is a temporary password set by the Owner.
    /// This is the default shape for any credential an administrator issues; the exemption is the
    /// separate, explicitly named <see cref="SetOwnPassword"/>.
    /// </remarks>
    public Result SetTemporaryPassword(string passwordHash)
    {
        Result set = StorePasswordHash(passwordHash);
        if (set.IsFailure)
        {
            return set;
        }

        MustChangePassword = true;
        return Result.Success();
    }

    /// <summary>
    /// Removes the stored credential, leaving an account that cannot authenticate with a password.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stamp rotates, by the same argument as a changed credential (D-049 ruling 3, D-053): a
    /// credential that no longer exists must not leave live sessions behind it.
    /// </para>
    /// <para>
    /// <b>Called by <c>ReactivateUser</c>'s handler, unconditionally, on every reactivation</b> —
    /// KAFF-112 rule 3, built under the readiness waiver of decisions.md D-062 §1 (Q50 stays open).
    /// Q50 asked whether the returning employee arrives with no credential at all or is given one in
    /// the same request; the story's reading is both, in that order — this method clears what was
    /// there, and the handler calls <see cref="SetTemporaryPassword"/> afterwards only when the Owner
    /// supplied one. <see cref="Reactivate"/> does not depend on this call for its own stamp rotation
    /// (D-051 N5) — the two are independent invariants that happen to run in the same transaction.
    /// </para>
    /// </remarks>
    public void ClearPassword()
    {
        PasswordHash = null;

        // There is no credential left to force a change of; whichever setter runs next decides again.
        MustChangePassword = false;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
    }

    /// <summary>True while the account is inside a lockout window (spec.md §9 amendment, D-049).</summary>
    public bool IsLockedOut(DateTimeOffset asOf) => LockedOutUntil > asOf;

    /// <summary>
    /// Records one failed sign-in, locking the account once the run reaches
    /// <paramref name="maxFailedAttempts"/>.
    /// </summary>
    /// <remarks>
    /// The two numbers are Karim's (5 and 15 minutes, spec.md §9 amendment) and are passed in rather
    /// than written here, so they live in configuration next to the other operational number he set —
    /// see <c>LockoutOptions</c> and decisions.md D-049. The counter restarts when the lock is
    /// applied: it exists to serve one lock, and carrying it over would mean a single further attempt
    /// after the window expired re-locked the account.
    /// </remarks>
    public void RecordFailedSignIn(DateTimeOffset occurredAt, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        FailedSignInAttempts++;

        if (FailedSignInAttempts < maxFailedAttempts)
        {
            return;
        }

        FailedSignInAttempts = 0;
        LockedOutUntil = occurredAt + lockoutDuration;
    }

    /// <summary>Clears the failure run and any lockout. "Consecutive" is what makes this necessary.</summary>
    public void RecordSuccessfulSignIn()
    {
        FailedSignInAttempts = 0;
        LockedOutUntil = null;
    }

    private Result StorePasswordHash(string passwordHash)
    {
        if (Role == Role.Subcontractor)
        {
            // spec.md §9: subcontractors are a record, not a login.
            return Result.Failure(IdentityErrors.SubcontractorCannotLogIn);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure(IdentityErrors.PasswordHashRequired);
        }

        PasswordHash = passwordHash;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
        return Result.Success();
    }

    public Result Deactivate(DateTimeOffset occurredAt)
    {
        if (!IsActive)
        {
            return Result.Failure(IdentityErrors.UserAlreadyInactive);
        }

        IsActive = false;
        DeactivatedAt = occurredAt;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
        return Result.Success();
    }

    /// <summary>
    /// KAFF-112 rule 8. Brings the account back with no credential and no session that survives it.
    /// </summary>
    /// <remarks>
    /// <b>Rotates <see cref="SecurityStamp"/>.</b> Every other method that changes what this account
    /// can do already does — <see cref="Deactivate"/>, <see cref="ClearPassword"/>, and
    /// <see cref="StorePasswordHash"/> behind both password setters — and until now this was "the one
    /// path that should rotate and does not" (decisions.md D-051 N5, KAFF-112 rule 9a). The rotation
    /// does not depend on the caller also clearing or reissuing the credential in the same request:
    /// this method's own invariant is that reactivating an account cannot leave a pre-existing token
    /// able to authenticate against it, independent of whatever a handler does next.
    /// </remarks>
    public Result Reactivate()
    {
        if (IsActive)
        {
            return Result.Failure(IdentityErrors.UserAlreadyActive);
        }

        IsActive = true;
        DeactivatedAt = null;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
        return Result.Success();
    }

    public Result MoveToDepartment(Department? department, OperationsSubDepartment? subDepartment)
    {
        Result check = ValidateDepartment(Role, department, subDepartment);
        if (check.IsFailure)
        {
            return check;
        }

        Department = department;
        OperationsSubDepartment = subDepartment;
        return Result.Success();
    }

    /// <summary>
    /// Changes the role the account holds. KAFF-109.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reapplies exactly the invariants <see cref="Create"/> applies at creation — department
    /// compatibility through <see cref="ValidateDepartment"/>, the client-id rule for
    /// <see cref="Identity.Role.Client"/>, and the no-department rule for external roles — against
    /// the <b>new</b> role and the account's existing <see cref="Department"/>,
    /// <see cref="OperationsSubDepartment"/> and <see cref="ClientId"/> (KAFF-109 rule 11). None of
    /// those three fields moves here: KAFF-108 owns the department move and this method does not
    /// repeat it.
    /// </para>
    /// <para>
    /// <b>Revoking project assignments is not this method's job.</b> A <see cref="User"/> cannot reach
    /// its own <see cref="ProjectAssignment"/> rows, so the revocation is handler work — the same
    /// shape KAFF-111 already established inside KAFF-110's handler (decisions.md D-051 Q27, D-074
    /// §2). This method only decides whether the role transition itself is legal.
    /// </para>
    /// <para>
    /// KAFF-109 rule 8: a request naming the role already held is not a change. It is not special-cased
    /// here — re-validating state that was already valid cannot fail, so this call simply succeeds
    /// without altering anything observable, and the handler is what compares before and after to
    /// decide whether there is anything left to revoke.
    /// </para>
    /// </remarks>
    public Result ChangeRole(Role role)
    {
        Result departmentCheck = ValidateDepartment(role, Department, OperationsSubDepartment);
        if (departmentCheck.IsFailure)
        {
            return departmentCheck;
        }

        if (role == Role.Client && ClientId is null)
        {
            return Result.Failure(IdentityErrors.ClientUserRequiresClient);
        }

        if (role != Role.Client && ClientId is not null)
        {
            return Result.Failure(IdentityErrors.NonClientUserCannotCarryClient);
        }

        // spec.md §9, "Subcontractor — record only, no login", the transition half of it. The entity
        // already refuses a subcontractor a credential on the way in (StorePasswordHash) and the
        // database refuses the pair outright — ck_users_subcontractor_cannot_log_in, "role <>
        // 'Subcontractor' OR password_hash IS NULL". Nothing re-applied it on the way *round*, so a
        // departmentless staff account holding a hash — every Role.Owner, including the one the setup
        // screen mints — passed every check here and violated the constraint at SaveChangesAsync,
        // arriving at the caller as an untranslatable 500 (qa/slice-1/verification-2026-08-26.md,
        // V-26-A). This is that same constraint, stated where a Result can carry it.
        //
        // 🟡 It REFUSES rather than clearing the credential, and which of the two Kaff wants is a
        // business question nobody has answered — see decisions.md D-088. Refusing is the reversible
        // half: a ruling can relax it to "convert and clear" later, and nothing was destroyed in the
        // meantime. Clearing a credential the Owner did not ask to clear cannot be undone.
        if (role == Role.Subcontractor && PasswordHash is not null)
        {
            return Result.Failure(IdentityErrors.SubcontractorCannotLogIn);
        }

        Role = role;
        return Result.Success();
    }

    /// <summary>
    /// Every rule binding a role to the fields that qualify it. The one place both <see cref="Create"/>
    /// and <see cref="ChangeRole"/> route through, which is why the role-is-a-role check lives here.
    /// </summary>
    /// <remarks>
    /// The name says "department" and it now checks the role's existence too. Kept rather than renamed:
    /// four historical records — <c>meetings/2026-08-21-sprint-1-refinement.md</c>,
    /// <c>qa/slice-1/verification-2026-08-23.md</c>, <c>proposals/story-review-2026-08-22.md</c> and
    /// <c>stories/slice-1-foundation/KAFF-106-owner-creates-a-user.md</c> — cite this identifier under
    /// SM-31, and a rename breaks four citations in documents that must not be edited by whoever
    /// renames it. The identifier is the contract; this summary is where the widening is stated.
    /// </remarks>
    private static Result ValidateDepartment(
        Role role,
        Department? department,
        OperationsSubDepartment? subDepartment)
    {
        // V-27-C. Asked first, because every rule below is written against the nine roles that exist
        // and says nothing about a value outside them — which is how (Role)99 walked past all of them.
        // An enum is not a closed set at run time: the cast is legal, the column is text (D-002), and
        // JsonStringEnumConverter takes integers, so the wire could name a role that is not one.
        // Refused here rather than in the ChangeUserRole slice because CreateUser reaches the same
        // fields by the same route, and a guard in one endpoint leaves the sibling open.
        if (!Enum.IsDefined(role))
        {
            return Result.Failure(IdentityErrors.UnknownRole);
        }

        // Permission grants may be written against a department alone — HR owns the people records
        // (spec.md §2) and Operations / Administrative confirms site expenses (§8) — and such a grant
        // matches any role carrying that department. A portal client or a subcontractor with a
        // department would therefore inherit company-wide permissions that skip the project check and
        // the client check entirely, breaking spec.md §12. Neither role is Kaff staff, so neither has
        // a department. See decisions.md D-035.
        if (role is Role.Client or Role.Subcontractor && department is not null)
        {
            return Result.Failure(IdentityErrors.ExternalRoleCannotHoldDepartment);
        }

        // The same hole, from the other direction. Karim's ruling of 2026-08-20 makes HR strictly
        // administrative — "cannot see project costs, margins, or the safe" — but the catalogue
        // cannot enforce that alone, because grants written against a department match any role
        // carrying it. An HR user placed in Operations / Administrative would inherit
        // SiteExpenseConfirm and PhotoPublish; one placed in Finance would inherit nothing today,
        // but only because every Finance grant happens to be written against the role. Binding the
        // HR role to the HR department closes both, now and for grants not yet written.
        // See decisions.md D-044.
        if (role == Role.Hr && department != Identity.Department.Hr)
        {
            return Result.Failure(IdentityErrors.HrRoleRequiresHrDepartment);
        }

        // spec.md §9: only Operations subdivides.
        if (department == Identity.Department.Operations && subDepartment is null)
        {
            return Result.Failure(IdentityErrors.OperationsRequiresSubDepartment);
        }

        if (department != Identity.Department.Operations && subDepartment is not null)
        {
            return Result.Failure(IdentityErrors.SubDepartmentOnlyForOperations);
        }

        return Result.Success();
    }
}
