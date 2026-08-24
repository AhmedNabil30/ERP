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
    /// Records the client's tax registration number.
    /// </summary>
    /// <remarks>
    /// This replaced <c>SetWithholding</c> on 2026-08-21. spec.md §6.7 says "individual clients do
    /// not withhold", so an individual carrying a registration number is asserting the thing §6.7
    /// denies — refused here rather than left to a validator, because the invariant belongs to the
    /// entity (D-040, D-049).
    /// </remarks>
    public Result SetTaxRegistration(string? taxRegistrationNumber)
    {
        string? trimmed = Blank(taxRegistrationNumber);

        if (trimmed is not null && Kind == ClientKind.Individual)
        {
            return Result.Failure(MasterDataErrors.IndividualDoesNotWithhold);
        }

        TaxRegistrationNumber = trimmed;
        return Result.Success();
    }

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
