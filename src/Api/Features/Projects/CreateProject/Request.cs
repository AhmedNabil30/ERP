using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.CreateProject;

/// <summary>
/// What opens a project. KAFF-400.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Code</c> member</b>, the same shape as <c>CreateClient.Request</c>: it is generated, never
/// typed or edited. There is no <c>project_code_seq</c> sequence today and this story ships no
/// migration (CLAUDE.md, the story's own hard limit) — <c>Handler.GenerateCode</c> is a named,
/// deliberate simplification rather than the sequential-code mechanism <c>Client</c>/<c>Employee</c>/
/// <c>Subcontractor</c>/<c>Supplier</c> use. See the handler's remarks.
/// </para>
/// <para>
/// <b><c>HoldPercentage</c> is carried as <see cref="Percentage"/> — the fraction, e.g. <c>"0.20"</c>
/// for 20% (decisions.md D-151). A negative value is refused by the type itself before this handler
/// ever sees it.</b> <c>ClientAdvance</c> stays a percent figure, not a fraction —
/// <c>25</c> means 25% — because its own name does not end <c>Rate</c>/<c>Percentage</c>/<c>Markup</c>
/// and D-151's wire sweep (<c>WireUnitTests</c>) does not flag it; it is out of KAFF-400's scope.
/// <c>HoldPercentage</c> defaults to 20% when absent (spec.md §5.1, §15). <c>ClientAdvance</c> is
/// read as the advance <b>rate</b> — <c>Project.AdvanceRate</c> — not an absolute cash amount: spec.md
/// §16 assumption 1 pairs exactly these two figures, "Advance 25%, hold 20% as defaults,
/// owner-adjustable per project", and <c>Project</c> has no column for an absolute advance amount to
/// land on. Defaults to 25 when absent. <b>Flagged for Karim</b>: if the field was meant to carry the
/// cash amount collected at signing instead, that value needs a new column and, more likely, a
/// Treasury posting — out of this story's reach (<c>src/Api/Features/Treasury</c> is off limits here)
/// and out of contract signature's, too, which this story does not build.
/// </para>
/// <para>
/// <b>The remaining Lump Sum rates are not on this endpoint</b> — <c>AdvanceRecoveryRate</c> (25%,
/// assumption 2), <c>MaterialAdvanceRate</c> (75%, spec.md §5.1) and <c>DelayPenaltyEnabled</c> (off,
/// assumption 5) are spec.md's own stated defaults and the brief's request body does not carry them.
/// The handler applies the defaults directly; nothing here overrides them, and Cost Plus and Design
/// terms (supervision rate; area and rate per m²) are contract-signature's later story, not this one.
/// </para>
/// <param name="Name">Required.</param>
/// <param name="ClientId">Must name an existing client.</param>
/// <param name="ContractType">Lump Sum, Cost Plus or Design (spec.md §5). Immutable once created.</param>
/// <param name="ContractValue">
/// Required and positive for Lump Sum. Left null for Cost Plus (spec.md §5.2: no fixed value) and for
/// Design, whose value is computed later from area × rate (contract signature, not this story).
/// </param>
/// <param name="HoldPercentage">
/// Lump Sum only. Carried as <see cref="Percentage"/> — the fraction (D-151). Defaults to 20% when
/// omitted.
/// </param>
/// <param name="ClientAdvance">Lump Sum only. Percent units, read as the advance rate. Defaults to 25.</param>
/// <param name="Status">
/// Optional. <c>null</c> or <see cref="ProjectStatus.Setup"/> leaves the project at its default Setup
/// state. <see cref="ProjectStatus.Active"/> transitions it immediately — a convenience for a project
/// that is already under way when it is entered into Kaff. Any other value is refused: spec.md §13's
/// state machine has no other legal first step, and متعثرة / تم تأجيلها are health tags, never states
/// (D-044 §7), so neither can be an initial status.
/// </param>
public sealed record Request(
    string? Name,
    Guid ClientId,
    ContractType ContractType,
    decimal? ContractValue,
    Percentage? HoldPercentage,
    decimal? ClientAdvance,
    ProjectStatus? Status);
