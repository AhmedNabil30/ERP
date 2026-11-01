using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Boqs;

/// <summary>
/// A project's bill of quantities (spec.md §4). One per project. Its total is the Lump Sum contract
/// value, and signing freezes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Frozen by copy, and frozen for real.</b> Every line holds catalogue <i>values</i>, never a key
/// (spec.md §4.4). <see cref="FrozenOn"/> is the marker; the database refuses any insert, update or
/// delete on the lines of a frozen BOQ (<c>003_boq_freeze.sql</c>) except the executed quantity. The
/// marker lives on the header, so freezing a 400-line BOQ is one row changed and one audit record, not
/// 400.
/// </para>
/// <para>
/// Every mutating method here refuses once <see cref="FrozenOn"/> is set, so the user gets a translated
/// refusal; the trigger is what makes the rule true for any other path.
/// </para>
/// </remarks>
public sealed class Boq : Entity
{
    private readonly List<BoqLine> _lines = [];

    private Boq()
    {
    }

    private Boq(Guid projectId, DateTimeOffset createdAt)
        : base(NewId())
    {
        ProjectId = projectId;
        CreatedAt = createdAt;
    }

    public Guid ProjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set once, at contract signature. Null while the BOQ is a draft.</summary>
    public DateOnly? FrozenOn { get; private set; }

    public bool IsFrozen => FrozenOn is not null;

    public IReadOnlyList<BoqLine> Lines => _lines;

    public static Result<Boq> Open(Project project, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!ContractValueSource.AllowsBoq(project.ContractType))
        {
            return Result.Failure<Boq>(BoqErrors.NotForThisContractType);
        }

        if (project.IsSigned)
        {
            return Result.Failure<Boq>(ProjectErrors.AlreadySigned);
        }

        return Result.Success(new Boq(project.Id, createdAt));
    }

    /// <summary>
    /// Adds a line by copying the catalogue item's values. The markup defaults from the باب unless
    /// overridden (spec.md §4.2, §4.5).
    /// </summary>
    public Result<BoqLine> AddCatalogueLine(
        CatalogueItem item,
        Bab bab,
        decimal? quantityEstimated,
        decimal? quantitySurveyed,
        Percentage? conditionsRate = null,
        Percentage? markupOverride = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bab);

        if (item.Status == CatalogueItemStatus.Archived)
        {
            return Result.Failure<BoqLine>(BoqErrors.CatalogueItemArchived);
        }

        return Append(
            item.Code, item.DescriptionAr, item.DescriptionEn, item.Unit, bab,
            item.CostPrice, item.BaseSellRate, quantityEstimated, quantitySurveyed,
            conditionsRate, markupOverride, isCustom: false);
    }

    /// <summary>
    /// Adds a custom line (spec.md §4.5): BOQ only, flagged <c>PendingCatalogueReview</c>, and it never
    /// writes the catalogue.
    /// </summary>
    public Result<BoqLine> AddCustomLine(
        string? itemCode,
        string descriptionAr,
        string? descriptionEn,
        string unit,
        Bab bab,
        Money costPrice,
        Money baseSellRate,
        decimal? quantityEstimated,
        decimal? quantitySurveyed,
        Percentage? conditionsRate = null,
        Percentage? markupOverride = null)
    {
        ArgumentNullException.ThrowIfNull(bab);

        if (string.IsNullOrWhiteSpace(descriptionAr) || descriptionAr.Length > BoqLine.MaxDescriptionLength)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.DescriptionRequired);
        }

        if (string.IsNullOrWhiteSpace(unit) || unit.Length > BoqLine.MaxUnitLength)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.UnitRequired);
        }

        if (itemCode is { Length: > BoqLine.MaxCodeLength })
        {
            return Result.Failure<BoqLine>(MasterDataErrors.CodeRequired);
        }

        if (costPrice.IsNegative)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.CostPriceMustNotBeNegative);
        }

        if (baseSellRate.IsNegative)
        {
            return Result.Failure<BoqLine>(MasterDataErrors.SellRateMustNotBeNegative);
        }

        return Append(
            string.IsNullOrWhiteSpace(itemCode) ? null : itemCode.Trim().ToUpperInvariant(),
            descriptionAr.Trim(),
            string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim(),
            unit.Trim(),
            bab, costPrice, baseSellRate, quantityEstimated, quantitySurveyed,
            conditionsRate, markupOverride, isCustom: true);
    }

    public Result SetQuantities(Guid lineId, decimal? estimated, decimal? surveyed)
    {
        Result open = EnsureOpen();
        if (open.IsFailure)
        {
            return open;
        }

        if (estimated < 0m || surveyed < 0m)
        {
            return Result.Failure(BoqErrors.QuantityMustNotBeNegative);
        }

        BoqLine? line = _lines.FirstOrDefault(candidate => candidate.Id == lineId);
        if (line is null)
        {
            return Result.Failure(BoqErrors.LineNotFound);
        }

        line.SetQuantities(estimated, surveyed);
        return Result.Success();
    }

    public Result SetPricing(Guid lineId, Percentage conditionsRate, Percentage lineMarkup)
    {
        Result open = EnsureOpen();
        if (open.IsFailure)
        {
            return open;
        }

        BoqLine? line = _lines.FirstOrDefault(candidate => candidate.Id == lineId);
        if (line is null)
        {
            return Result.Failure(BoqErrors.LineNotFound);
        }

        line.SetPricing(conditionsRate, lineMarkup);
        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        Result open = EnsureOpen();
        if (open.IsFailure)
        {
            return open;
        }

        BoqLine? line = _lines.FirstOrDefault(candidate => candidate.Id == lineId);
        if (line is null)
        {
            return Result.Failure(BoqErrors.LineNotFound);
        }

        _lines.Remove(line);

        for (int index = 0; index < _lines.Count; index++)
        {
            _lines[index].MoveTo(index + 1);
        }

        return Result.Success();
    }

    /// <summary>
    /// The sum of the line totals, each priced on the ruled quantity basis and rounded as the ruling says
    /// (<see cref="ContractValueSource"/>). Refuses an empty BOQ and any line that lacks the quantity.
    /// </summary>
    public Result<Money> Total(ContractValueSource rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (_lines.Count == 0)
        {
            return Result.Failure<Money>(BoqErrors.Empty);
        }

        Money total = Money.Zero;

        foreach (BoqLine line in _lines)
        {
            decimal? quantity = line.QuantityFor(rule.QuantityBasis);

            if (quantity is null)
            {
                return Result.Failure<Money>(BoqErrors.LineQuantityMissing);
            }

            total += rule.LineTotal(line.SellRate, quantity.Value);
        }

        return Result.Success(total);
    }

    /// <summary>Freezes the BOQ at contract signature. One way: there is no thaw.</summary>
    public Result Freeze(DateOnly on)
    {
        if (IsFrozen)
        {
            return Result.Failure(BoqErrors.AlreadyFrozen);
        }

        FrozenOn = on;
        return Result.Success();
    }

    private Result EnsureOpen() => IsFrozen ? Result.Failure(BoqErrors.Frozen) : Result.Success();

    private Result<BoqLine> Append(
        string? itemCode,
        string descriptionAr,
        string? descriptionEn,
        string unit,
        Bab bab,
        Money costPrice,
        Money baseSellRate,
        decimal? quantityEstimated,
        decimal? quantitySurveyed,
        Percentage? conditionsRate,
        Percentage? markupOverride,
        bool isCustom)
    {
        Result open = EnsureOpen();
        if (open.IsFailure)
        {
            return Result.Failure<BoqLine>(open.Error);
        }

        if (quantityEstimated < 0m || quantitySurveyed < 0m)
        {
            return Result.Failure<BoqLine>(BoqErrors.QuantityMustNotBeNegative);
        }

        var line = new BoqLine(
            Id,
            ProjectId,
            _lines.Count + 1,
            itemCode,
            descriptionAr,
            descriptionEn,
            unit,
            bab.Code,
            bab.NameAr,
            bab.NameEn,
            costPrice,
            baseSellRate,
            conditionsRate ?? Percentage.Zero,
            markupOverride ?? bab.DefaultMarkup,
            quantityEstimated,
            quantitySurveyed,
            isCustom);

        _lines.Add(line);
        return Result.Success(line);
    }
}
