using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>
/// باب — a trade section. Owned by the Technical Office (spec.md §2).
/// </summary>
/// <remarks>
/// spec.md §2: "~40 trades, tree, carries default markup %." The markup default flows into every new
/// BOQ line for an item in this باب (spec.md §4.2, §4.5) and is overridable per line — concrete 15%,
/// finishes 30% are the examples spec.md gives.
///
/// Held as a <see cref="Percentage"/> rather than a bare decimal so 15% cannot be stored as 15 in one
/// place and 0.15 in another.
/// </remarks>
public sealed class Bab : Entity
{
    public const int MaxCodeLength = 32;
    public const int MaxNameLength = 200;

    private Bab()
    {
    }

    private Bab(Guid id, string code, string nameAr, string nameEn, Guid? parentBabId, Percentage defaultMarkup, int sortOrder)
        : base(id)
    {
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        ParentBabId = parentBabId;
        DefaultMarkup = defaultMarkup;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    /// <summary>Parent node. spec.md §2 describes أبواب as a tree.</summary>
    public Guid? ParentBabId { get; private set; }

    /// <summary>Default line markup for items in this باب (spec.md §4.2).</summary>
    public Percentage DefaultMarkup { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Bab> Create(
        string code,
        string nameAr,
        string nameEn,
        Percentage defaultMarkup,
        Guid? parentBabId = null,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Bab>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            return Result.Failure<Bab>(MasterDataErrors.NameRequired);
        }

        return Result.Success(new Bab(
            NewId(),
            code.Trim().ToUpperInvariant(),
            nameAr.Trim(),
            nameEn.Trim(),
            parentBabId,
            defaultMarkup,
            sortOrder));
    }

    public Result SetParent(Guid? parentBabId)
    {
        if (parentBabId == Id)
        {
            return Result.Failure(MasterDataErrors.BabCannotBeItsOwnParent);
        }

        ParentBabId = parentBabId;
        return Result.Success();
    }

    public void SetDefaultMarkup(Percentage markup) => DefaultMarkup = markup;

    public Result Archive()
    {
        if (!IsActive)
        {
            return Result.Failure(MasterDataErrors.AlreadyArchived);
        }

        IsActive = false;
        return Result.Success();
    }
}
