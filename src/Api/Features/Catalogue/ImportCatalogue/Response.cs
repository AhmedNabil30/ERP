namespace Kaff.Api.Features.Catalogue.ImportCatalogue;

/// <summary>
/// One row the import could not take. KAFF-200 rule 7 — a refused row stops no other row.
/// </summary>
/// <param name="RowNumber">The sheet's own row number, the one Excel shows the user — not an index.</param>
/// <param name="Column">The template column the refusal is about, empty when the domain factory's
/// own refusal does not point at one column in particular.</param>
/// <param name="MessageKey">An i18n key in <c>errors.master.*</c>, per decisions.md D-128 §1.</param>
/// <param name="Value">The offending cell's text, when there is one worth showing back to the operator.</param>
public sealed record RowFailure(int RowNumber, string Column, string MessageKey, string? Value);

/// <summary>
/// KAFF-200 — the outcome of one import. <c>AC-200-A</c>, <c>AC-200-H</c>: every good row became an
/// item; every bad row is named here with why.
/// </summary>
/// <param name="CreatedCount">How many catalogue items this import created.</param>
/// <param name="Failures">Every row this import refused, in the order the sheet carried them.</param>
public sealed record Response(int CreatedCount, IReadOnlyList<RowFailure> Failures);
