using Kaff.Domain.Common;

namespace Kaff.Api.Features.Babs.EditBab;

/// <summary>The edited باب. KAFF-204.</summary>
/// <param name="DefaultMarkup">Carried as <see cref="Percentage"/> — 15% is <c>"0.15"</c> (D-044 ruling 6, D-135, D-151).</param>
public sealed record Response(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    Guid? ParentBabId,
    Percentage DefaultMarkup,
    int SortOrder,
    bool IsActive);
