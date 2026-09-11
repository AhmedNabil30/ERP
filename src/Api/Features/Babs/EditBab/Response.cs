namespace Kaff.Api.Features.Babs.EditBab;

/// <summary>The edited باب. KAFF-204.</summary>
/// <param name="DefaultMarkup">The rate as a fraction — 15% is <c>0.15</c> (D-044 ruling 6, D-135).</param>
public sealed record Response(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    Guid? ParentBabId,
    decimal DefaultMarkup,
    int SortOrder,
    bool IsActive);
