using Kaff.Domain.Common;

namespace Kaff.Domain.Boqs;

/// <summary>
/// A prepared base BOQ (D-217 items 19 and 20, D-218 item 13): a name and lines of catalogue item and
/// quantity, nothing else. It holds no rates, no hold percentage and no advance. A base is not a
/// contract, so a line names its catalogue item by key; the freeze-by-copy rule (spec.md §4.4) applies
/// to the project BOQ the base is copied into, where the rates are read from the catalogue of the day.
/// </summary>
public sealed class BoqBase : Entity
{
    public const int MaxNameLength = 200;

    private readonly List<BoqBaseLine> _lines = [];

    private BoqBase()
    {
    }

    private BoqBase(string name, DateTimeOffset createdAt)
        : base(NewId())
    {
        Name = name;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set when the Technical Office archives the base. An archived base is hidden and read-only.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyList<BoqBaseLine> Lines => _lines;

    public static Result<BoqBase> Create(string? name, DateTimeOffset createdAt)
    {
        Result<string> valid = ValidName(name);

        return valid.IsFailure
            ? Result.Failure<BoqBase>(valid.Error)
            : Result.Success(new BoqBase(valid.Value, createdAt));
    }

    public Result Rename(string? name)
    {
        Result open = EnsureOpen();
        if (open.IsFailure)
        {
            return open;
        }

        Result<string> valid = ValidName(name);
        if (valid.IsFailure)
        {
            return Result.Failure(valid.Error);
        }

        Name = valid.Value;
        return Result.Success();
    }

    /// <summary>Replaces every line in one step: the editor sends the whole list it shows.</summary>
    public Result<IReadOnlyList<BoqBaseLine>> ReplaceLines(IEnumerable<(Guid CatalogueItemId, decimal Quantity)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Result open = EnsureOpen();
        if (open.IsFailure)
        {
            return Result.Failure<IReadOnlyList<BoqBaseLine>>(open.Error);
        }

        var fresh = new List<BoqBaseLine>();
        foreach ((Guid catalogueItemId, decimal quantity) in items)
        {
            if (quantity < 0m)
            {
                return Result.Failure<IReadOnlyList<BoqBaseLine>>(BoqErrors.QuantityMustNotBeNegative);
            }

            fresh.Add(new BoqBaseLine(Id, fresh.Count + 1, catalogueItemId, quantity));
        }

        _lines.Clear();
        _lines.AddRange(fresh);
        return Result.Success<IReadOnlyList<BoqBaseLine>>(fresh);
    }

    public Result Archive(DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(BoqErrors.BaseArchived);
        }

        ArchivedAt = at;
        return Result.Success();
    }

    private static Result<string> ValidName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength
            ? Result.Failure<string>(BoqErrors.BaseNameRequired)
            : Result.Success(name.Trim());

    private Result EnsureOpen() => IsArchived ? Result.Failure(BoqErrors.BaseArchived) : Result.Success();
}
