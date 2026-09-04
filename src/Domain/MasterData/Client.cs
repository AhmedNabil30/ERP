using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.MasterData;

/// <summary>Whether the client is a person or a company. Drives withholding (spec.md §6.7).</summary>
public enum ClientKind
{
    Individual = 1,
    Corporate = 2,
}

/// <summary>
/// A client. Owned by Marketing (spec.md §2), project-independent.
/// </summary>
/// <remarks>
/// <para>
/// <b>The phone is a soft deduplication key, not a unique one.</b> spec.md §2 says "deduplicated by
/// phone" and §3 says "never create a duplicate client", and until 2026-08-21 that was a unique index
/// which refused the save outright. Karim ruled the other way: "Allow duplicates, but show a soft
/// warning … Do not block the save," because "a corporate client and its CEO might be registered as
/// two separate entities sharing the same contact number." The index remains, non-unique, so the
/// lookup that produces the warning stays fast. See decisions.md D-049.
/// </para>
/// <para>
/// <see cref="PhoneNormalised"/> is the key rather than the entered text, so +20 10 …, 0020 10 … and
/// 010 … all match as they should — which matters more now, not less: a matcher that misses is a
/// warning nobody sees.
/// </para>
/// <para>
/// <b>Withholding is not here.</b> The rate lives on the project, because spec.md §6.7 sets it by
/// what is supplied and §5.4 lets one client hold a design contract and an execution contract at
/// once. See <c>Project.WithholdingCategory</c> and decisions.md D-049.
/// </para>
/// </remarks>
public sealed class Client : Entity
{
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 32;

    private Client()
    {
    }

    private Client(
        Guid id,
        string code,
        string name,
        PhoneNumber phone,
        ClientKind kind,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        Kind = kind;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string PhoneEntered { get; private set; } = null!;

    /// <summary>Digits-only national form. The deduplication key of spec.md §2.</summary>
    public string PhoneNormalised { get; private set; } = null!;

    public string? AlternatePhone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public ClientKind Kind { get; private set; }

    /// <summary>
    /// The company's tax registration number. Identity, not rate.
    /// </summary>
    /// <remarks>
    /// This stayed when the withholding <i>category</i> moved to the project on 2026-08-21: a tax
    /// registration number identifies the legal entity and does not change per contract, whereas the
    /// rate does. Only a corporate client has one — an individual with a registration number is
    /// making the same claim the rate used to, by another field.
    /// </remarks>
    public string? TaxRegistrationNumber { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public PhoneNumber Phone => PhoneNumber.FromStorage(PhoneEntered, PhoneNormalised);

    /// <summary>
    /// Creates a client.
    /// </summary>
    /// <param name="code">
    /// The generated code. Karim, 2026-08-21: "The system must auto-generate sequential codes
    /// (e.g. C-10001). Manual entry or editing of the code is strictly forbidden." The generator is
    /// the caller's — slice 1 — and there is deliberately no setter, so a code cannot be changed
    /// after creation.
    /// </param>
    public static Result<Client> Create(
        string code,
        string name,
        PhoneNumber phone,
        ClientKind kind,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Client>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure<Client>(MasterDataErrors.NameRequired);
        }

        return Result.Success(new Client(
            NewId(),
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            phone,
            kind,
            createdAt));
    }

    public void SetContactDetails(string? alternatePhone, string? email, string? address, string? notes)
    {
        AlternatePhone = Blank(alternatePhone);
        Email = Blank(email)?.ToLowerInvariant();
        Address = Blank(address);
        Notes = Blank(notes);
    }

    /// <summary>
    /// Corrects the client's name. KAFF-121 rule 2.
    /// </summary>
    /// <remarks>
    /// The same two conditions <see cref="Create"/> applies, because a name that could not have been
    /// registered must not be reachable by editing into it — a second, laxer path to the same column
    /// is how an invariant stops being one. spec.md §2 requires a client file to hold "full history",
    /// and until this existed a mistyped name was permanent (KAFF-121 finding F-09).
    /// </remarks>
    public Result Rename(string? name)
    {
        string? trimmed = Blank(name);

        if (trimmed is null || trimmed.Length > MaxNameLength)
        {
            return Result.Failure(MasterDataErrors.NameRequired);
        }

        Name = trimmed;
        return Result.Success();
    }

    /// <summary>
    /// Replaces the primary phone. KAFF-121 rule 3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No <c>Result</c>, because <see cref="PhoneNumber"/> cannot be constructed invalid — the
    /// caller has already been through <see cref="PhoneNumber.Create"/> and there is nothing left for
    /// this to refuse.
    /// </para>
    /// <para>
    /// <b>And nothing here refuses a duplicate.</b> spec.md §2, amended: a repeated number warns and
    /// does not block. The warning is the caller's, because naming the client that already holds the
    /// number needs a query and this type has no database — KAFF-119 rule 4, D-049 ruling 8. Putting
    /// a refusal here would contradict the ruling and would do it in the one place no handler could
    /// override.
    /// </para>
    /// </remarks>
    public void SetPrimaryPhone(PhoneNumber phone)
    {
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
    }

    /// <summary>
    /// Sets the kind and the tax registration number together, because spec.md §6.7 constrains the
    /// <b>pair</b> and not either member on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why one method and not two setters.</b> §6.7's rule is "an individual does not withhold",
    /// which is a statement about a client's kind and its registration number at the same instant.
    /// Two independent setters have to be called in some order, and <i>either</i> order is wrong for
    /// a legal request: kind-then-number refuses promoting an individual to a company that has a
    /// registration number, and number-then-kind admits an individual carrying one for as long as it
    /// takes to run the second line. Validating the pair has no order to get wrong.
    /// </para>
    /// <para>
    /// This is KAFF-121 rule 6, and rule 6 says the guard lives with the setter rather than in a
    /// validator — a validator is a second copy of the rule that every other caller of the entity
    /// bypasses (KAFF-120 rule 5, D-040, D-049).
    /// </para>
    /// </remarks>
    public Result SetClassification(ClientKind kind, string? taxRegistrationNumber)
    {
        string? trimmed = Blank(taxRegistrationNumber);

        if (trimmed is not null && kind == ClientKind.Individual)
        {
            return Result.Failure(MasterDataErrors.IndividualDoesNotWithhold);
        }

        Kind = kind;
        TaxRegistrationNumber = trimmed;
        return Result.Success();
    }

    /// <summary>
    /// Records the client's tax registration number, leaving the kind as it is.
    /// </summary>
    /// <remarks>
    /// This replaced <c>SetWithholding</c> on 2026-08-21. spec.md §6.7 says "individual clients do
    /// not withhold", so an individual carrying a registration number is asserting the thing §6.7
    /// denies — refused here rather than left to a validator, because the invariant belongs to the
    /// entity (D-040, D-049). It is <see cref="SetClassification"/> with the kind unchanged, and it
    /// delegates rather than repeating the check: two copies of one rule is the thing that drifts.
    /// </remarks>
    public Result SetTaxRegistration(string? taxRegistrationNumber) =>
        SetClassification(Kind, taxRegistrationNumber);

    public Result Archive()
    {
        if (!IsActive)
        {
            return Result.Failure(MasterDataErrors.AlreadyArchived);
        }

        IsActive = false;
        return Result.Success();
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
