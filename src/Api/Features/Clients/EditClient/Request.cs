using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.EditClient;

/// <summary>
/// What Marketing sends to correct a client's file. KAFF-121.
/// </summary>
/// <remarks>
/// <para>
/// <b>PUT, not PATCH — the body states the whole editable record and replaces it.</b> The precedent
/// is <c>MoveUserDepartment</c>, and the reason here is <see cref="Kind"/>: spec.md §6.7 constrains
/// the kind and the tax registration number as a pair, so a request that could omit one of them
/// would be asking the server to guess which half of the pair the operator meant to keep.
/// </para>
/// <para>
/// <b>There is no <c>Code</c> member, and its absence is <c>AC-121-E</c>.</b> Karim, spec.md §2's
/// amendment: codes are generated, "manual entry and later editing both forbidden". <c>Client.Code</c>
/// has a private setter and no mutator [Verified: 2026-09-04 @
/// <c>src/Domain/MasterData/Client.cs</c> -&gt; <c>Code</c>], so a code in the body binds to nothing
/// and no path in this slice could store one. Settled the same structural way <c>CreateClient</c>
/// settled it (decisions.md D-107 §4) — and it has to be settled twice, because "the code is not
/// editable" is a claim about the <i>edit</i> path that the create path cannot make.
/// </para>
/// <para>
/// <b>There is no <c>IsActive</c> member either.</b> Archiving is its own act with its own audit
/// meaning and its own story (KAFF-123); KAFF-121 rule 9 — "editing does not archive, and archiving
/// is not an edit".
/// </para>
/// </remarks>
/// <param name="Name">Required, and correctable — KAFF-121 rule 2. Until this story a mistyped name was permanent.</param>
/// <param name="Phone">The primary phone, entered form. Changing it re-runs the duplicate check (rule 4).</param>
/// <param name="Kind">Individual or Corporate. Constrained with <paramref name="TaxRegistrationNumber"/> as a pair.</param>
/// <param name="AlternatePhone">Optional. Not matched on — only the primary phone deduplicates.</param>
/// <param name="Email">Optional.</param>
/// <param name="Address">Optional.</param>
/// <param name="Notes">
/// <b>Internal.</b> spec.md §12: the client MUST NEVER see internal notes. That is enforced by the
/// permission on this route and by the response type, which carries no notes member — not by asking
/// the caller nicely (KAFF-121 rule 8, <c>AC-121-H</c>).
/// </param>
/// <param name="TaxRegistrationNumber">
/// Optional, and only for a corporate client. An individual carrying one is refused by
/// <c>Client.SetClassification</c> — spec.md §6.7 — which is <c>AC-121-F</c>. Sending the existing
/// number alongside <c>Kind: Individual</c> is exactly the case that criterion describes.
/// </param>
/// <param name="AcknowledgedDuplicatePhone">
/// <b>"I was shown who already holds this number and I am proceeding anyway."</b> Same contract as
/// registration (decisions.md D-107 §2) and re-matched server-side. <b>The client being edited is
/// never its own duplicate</b> — see <c>PhoneMatches.FindAsync</c>'s <c>excluding</c> parameter —
/// so an edit that leaves the phone alone never asks this question.
/// </param>
public sealed record Request(
    string? Name,
    string? Phone,
    ClientKind Kind,
    string? AlternatePhone,
    string? Email,
    string? Address,
    string? Notes,
    string? TaxRegistrationNumber,
    bool AcknowledgedDuplicatePhone);
