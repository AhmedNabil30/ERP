namespace Kaff.Domain.Rulings;

/// <summary>
/// Who submits the weekly QC report that an extract must pass before the Technical Office quantity
/// gate (spec.md §7, and the <c>QC</c> state of spec.md §14's extract machine).
/// </summary>
/// <remarks>
/// <para>
/// spec.md §7 names the step and names no actor for it. Three readings are defensible and they are not
/// equivalent, because the chain's integrity depends on which one is taken:
/// </para>
/// <list type="number">
///   <item>
///     <b>The supervising engineer</b> — the running default. spec.md §7's own sentence pairs the
///     Technical Office's assembly of the document with "the supervising engineer confirms field
///     quantities", and spec.md §9 makes the supervisor the actor who submits what juniors draft.
///   </item>
///   <item>
///     <b>The Technical Office</b> — spec.md §9 gives Operations / Technical "quantities, BOQ, extract
///     gate". Rejected as a default because the Technical Office holds
///     <c>Permission.QuantityGateApprove</c>: it would be gating its own report, which is the shape
///     spec.md §9's "nobody creates and approves the same movement" exists to prevent.
///   </item>
///   <item>
///     <b>Operations / Administrative</b> — spec.md §9 gives it "reports, photos, tasks". Rejected as a
///     default because the reports that section means are the site reports and KPI reports of
///     spec.md §11, and because the only grant shape available for it is a bare department, which
///     matches any role placed in that sub-department (finding F-04, decisions.md D-052).
///   </item>
/// </list>
/// <para>
/// This entry carries no value to read. The holder is enforced in <c>PermissionCatalogue</c>, which is
/// static data, so a second copy of the answer here would be a second place to change and the two would
/// drift. The note exists so the assumption is listed with every other unruled rule rather than only
/// visible to someone reading the catalogue.
/// </para>
/// </remarks>
public sealed record ExtractQcReportAuthor : IRuling
{
    public static readonly ExtractQcReportAuthor Default = new();

    public RulingNote Note => new(
        "ExtractQcReportAuthor",
        "Who submits an extract's weekly QC report (spec.md §7 names the step and no actor; raised by "
        + "KAFF-500 question 5, ruled as an assumption in decisions.md D-189 §3)",
        "The holder of Permission.ExtractQcReportSubmit — the actor who moves an extract from Draft to QC.",
        "The supervising engineer on the project, and nobody else. ASSUMED. The Technical Office is "
        + "deliberately excluded because it holds the quantity gate that the report feeds.",
        "Change the grants on the ExtractQcReportSubmit row in PermissionCatalogue, and remove the "
        + "permission from the pinned Unresolved set in CatalogueCompletenessTests. No handler names a "
        + "role, so nothing else moves.",
        RulingStatus.Assumed);
}
