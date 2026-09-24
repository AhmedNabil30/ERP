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
/// <param name="ContractValue">Lump Sum's signed value, or Design's once set. Null for Cost Plus.</param>
/// <param name="HoldRate">Lump Sum only.</param>
/// <param name="AdvanceRate">Lump Sum only.</param>
/// <param name="TeamSize">
/// Count of active <c>ProjectAssignment</c> rows. S-052's "team" — the per-member roster (KAFF-115) is
/// its own story and is not built here; see the build notes.
/// </param>
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
    int TeamSize);
