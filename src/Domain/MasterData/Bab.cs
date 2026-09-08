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

    /// <summary>
    /// Re-parents this باب, refusing any move that would make it its own ancestor at any depth.
    /// </summary>
    /// <param name="parentBabId">The new parent, or <c>null</c> to make this باب a root.</param>
    /// <param name="parentByBabId">
    /// Every باب's parent pointer — the one projection the walk needs, read by the caller in a single
    /// query. <b>The tree is what a cycle is a property of, not the node</b>: an entity cannot see its
    /// siblings, so a check confined to <c>this</c> catches depth one and nothing beyond it. That was
    /// the guard here until 2026-09-08, and <c>A.SetParent(B)</c> then <c>B.SetParent(A)</c> passed it
    /// completely, leaving neither باب reachable from a root (spec.md §2: the أبواب are a tree).
    /// </param>
    /// <remarks>
    /// The walk is bounded by the size of the tree rather than by reaching a root, because rows
    /// written before this guard existed may already hold a cycle this باب is not part of. A chain
    /// longer than the tree has one, and the move is refused rather than walked forever.
    /// </remarks>
    public Result SetParent(Guid? parentBabId, IReadOnlyDictionary<Guid, Guid?> parentByBabId)
    {
        ArgumentNullException.ThrowIfNull(parentByBabId);

        Guid? ancestor = parentBabId;

        for (int step = 0; step <= parentByBabId.Count; step++)
        {
            if (ancestor is null)
            {
                ParentBabId = parentBabId;
                return Result.Success();
            }

            if (ancestor == Id)
            {
                return Result.Failure(MasterDataErrors.BabCannotBeItsOwnParent);
            }

            ancestor = parentByBabId.GetValueOrDefault(ancestor.Value);
        }

        return Result.Failure(MasterDataErrors.BabCannotBeItsOwnParent);
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
