using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.GetProject;

/// <summary>
/// One project's header, for S-052. KAFF-400.
/// </summary>
/// <remarks>
/// <b>No stored balance</b> — CLAUDE.md, spec.md §6.1. <c>ContractValue</c> is the signed term, never a
/// derived figure. <b>Every money-shaped member crosses the wire as a JSON string</b> — decisions.md
/// D-135, through the global converters in <c>KaffJson</c>. <b>No redaction by role</b>: every role
/// that can reach this endpoint at all already holds <c>Permission.ProjectRead</c>, whose catalogue
/// grants are Owner, Finance, Technical Office, Marketing, Site Engineer and Head of Design — HR is
/// deliberately absent from that row (D-044 ruling 2) and never reaches this handler, so there is no
/// caller here for whom a financial field would need to be hidden.
/// </remarks>
/// <param name="Id">The project.</param>
/// <param name="Code">Generated.</param>
/// <param name="Name">As stored.</param>
/// <param name="ClientId">The owning client.</param>
/// <param name="ClientName">Denormalised for the header — S-052 needs it without a second call.</param>
/// <param name="ContractType">Lump Sum, Cost Plus or Design (spec.md §5).</param>
/// <param name="Status">spec.md §13's state machine. Never متعثرة or تم تأجيلها — those are health tags,
/// not states (D-044 §7), and carry no field here yet; KAFF-400 does not build the tag.</param>
/// <param name="ContractValue">
/// The REVISED value: the signed value plus the approved change orders, derived on every read (ruling
/// <c>ChangeOrderDisplayedValue</c>). Null for Cost Plus.
/// </param>
/// <param name="OriginalContractValue">The signed value, before any change order.</param>
/// <param name="ApprovedChangeOrderTotal">The sum of the approved change orders. Zero when there are none.</param>
/// <param name="HoldRate">Lump Sum only.</param>
/// <param name="AdvanceRate">Lump Sum only.</param>
/// <param name="TeamSize">
/// Count of active <c>ProjectAssignment</c> rows. S-052's "team" — the per-member roster (KAFF-115) is
/// its own story and is not built here; see the build notes.
/// </param>
/// <param name="SignedOn">Null until signed.</param>
/// <param name="StartedOn">Null until the project first becomes Active.</param>
/// <param name="HandoverOn">Null until handover.</param>
/// <param name="WarrantyEndsOn">Null until the warranty period is set at handover.</param>
/// <param name="ClosedOn">Null until closed.</param>
/// <param name="LastDailyLogOn">
/// The date of the most recent daily log on the project, derived on every read. Null when none has been written.
/// There is no contract duration and no target completion on <c>Project</c>, so none is returned and none is
/// invented (ruling <c>ProjectTargetCompletion</c>).
/// </param>
/// <param name="Money">
/// Derived from the extracts and the collection postings; never a stored balance. Null for a caller who may not
/// see extract money.
/// </param>
/// <param name="IsArchived">True for an unsigned Setup project whose follow-up period has ended (AC-417-J). Derived on every read.</param>
/// <param name="FollowUpEndsOn">The day the follow-up period ends. Null once the project is signed or has left Setup.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    Guid ClientId,
    string ClientName,
    ContractType ContractType,
    ProjectStatus Status,
    Money? ContractValue,
    Percentage? HoldRate,
    Percentage? AdvanceRate,
    int TeamSize,
    Money? OriginalContractValue,
    Money ApprovedChangeOrderTotal,
    DateOnly? SignedOn,
    DateOnly? StartedOn,
    DateOnly? HandoverOn,
    DateOnly? WarrantyEndsOn,
    DateOnly? ClosedOn,
    DateOnly? LastDailyLogOn,
    ProjectMoney? Money,
    bool IsArchived,
    DateOnly? FollowUpEndsOn);

/// <summary>
/// What the client has been billed, paid and still owes. Only extracts issued to the client count; an extract still
/// in the approval chain is not yet a debt.
/// </summary>
/// <param name="CertifiedToDate">Certified work value of the issued extracts.</param>
/// <param name="BilledToDate">Net payable of the issued extracts.</param>
/// <param name="CollectedToDate">Cash received plus tax withheld at source on them, summed from the postings, net of reversals.</param>
/// <param name="Outstanding">Billed less collected.</param>
public sealed record ProjectMoney(Money CertifiedToDate, Money BilledToDate, Money CollectedToDate, Money Outstanding);
