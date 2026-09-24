using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.CreateProject;

/// <summary>
/// The project that was just opened, as the screen that created it needs to see it. KAFF-400.
/// </summary>
/// <remarks>
/// <b>No stored balance, ever</b> — CLAUDE.md, spec.md §6.1. Every money-shaped member here is a
/// contract <i>term</i> the entity carries, not a derived figure. <b>Every money-shaped member crosses
/// the wire as a JSON string</b>, both directions — decisions.md D-135 — through the global
/// <c>Money</c>/<c>Percentage</c> converters registered in <c>KaffJson</c>; nothing here does that by
/// hand.
/// </remarks>
/// <param name="Id">The new project.</param>
/// <param name="Code">Generated. Never supplied and never editable.</param>
/// <param name="Name">As stored — trimmed.</param>
/// <param name="ClientId">The client this project belongs to.</param>
/// <param name="ContractType">Immutable from here (spec.md §5.4).</param>
/// <param name="Status">Always Setup, unless the request asked to skip to Active.</param>
/// <param name="ContractValue">Lump Sum only, at this stage. Null for Cost Plus and Design.</param>
/// <param name="HoldRate">Lump Sum only. Defaults to 20% (spec.md §5.1, §15).</param>
/// <param name="AdvanceRate">Lump Sum only. Defaults to 25% (spec.md §16 assumption 1).</param>
/// <param name="CreatedAt">When the project row was written.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    Guid ClientId,
    ContractType ContractType,
    ProjectStatus Status,
    Money? ContractValue,
    Percentage? HoldRate,
    Percentage? AdvanceRate,
    DateTimeOffset CreatedAt);
