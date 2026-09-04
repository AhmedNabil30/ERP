using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.CreateClient;

/// <summary>
/// What Marketing sends to register a client.
/// </summary>
/// <remarks>
/// <b>There is deliberately no <c>Code</c> member, and its absence is <c>AC-119-B</c>'s second half.</b>
/// The criterion asks for a supplied code to be <i>"ignored or refused — under no circumstances
/// stored"</i>, which is two behaviours no single test can assert. It is settled structurally
/// instead: a <c>code</c> in the body binds to nothing, and no code path in this slice can reach a
/// value the type does not carry. Karim, spec.md §2's amendment: codes are generated, "manual entry
/// and later editing both forbidden". <c>Client.Code</c> has a private setter and no mutator
/// [Verified: 2026-09-04 @ <c>src/Domain/MasterData/Client.cs</c> -&gt; <c>Code</c>], so the same
/// holds after creation. See decisions.md D-107 §4.
/// </remarks>
/// <param name="Name">Arabic, normally. Required — <c>Client.Create</c> refuses a blank one.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it into the deduplication key.</param>
/// <param name="Kind">Individual or Corporate (spec.md §6.7, KAFF-119 rule 8). Sent as the member name.</param>
/// <param name="AlternatePhone">Optional. Not matched on — only the primary phone deduplicates.</param>
/// <param name="Email">Optional.</param>
/// <param name="Address">Optional.</param>
/// <param name="TaxRegistrationNumber">
/// Optional, and only for a corporate client. It identifies the legal entity; it is <b>not</b> a
/// withholding rate, which moved to the contract on 2026-08-21 (D-049 ruling 9, KAFF-416). An
/// individual carrying one is refused by <c>Client.SetTaxRegistration</c> — spec.md §6.7,
/// "individual clients do not withhold" — which is <c>AC-119-K</c>.
/// </param>
/// <param name="AcknowledgedDuplicatePhone">
/// <b>"I was shown who already holds this number and I am proceeding anyway."</b> A boolean and not
/// an id: a single id cannot express the several matches D-049 ruling 8 makes normal, and the audit
/// link is server-derived from the match this handler re-runs rather than from anything the caller
/// claims. <b>A named simplification:</b> a new client can appear on that number between the check
/// and the save, so the acknowledgement is about the number, and the trail records what was actually
/// there at save time. See decisions.md D-107 §2.
/// </param>
public sealed record Request(
    string? Name,
    string? Phone,
    ClientKind Kind,
    string? AlternatePhone,
    string? Email,
    string? Address,
    string? TaxRegistrationNumber,
    bool AcknowledgedDuplicatePhone);
