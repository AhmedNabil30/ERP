using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Contracts.Progress;

/// <summary>How progress is expressed for a contract type.</summary>
public enum ProgressKind
{
    /// <summary>A single percentage of contract value. Lump Sum (spec.md §5.1).</summary>
    Percentage = 1,

    /// <summary>
    /// Money only. Cost Plus — spec.md §5.2: "Progress metric is cost-to-date plus supervision —
    /// no percentage progress bar."
    /// </summary>
    MonetaryOnly = 2,

    /// <summary>Weighted stages. Design (spec.md §5.3).</summary>
    StageWeighted = 3,
}

/// <summary>One reported part of overall progress — a باب section, or a design stage.</summary>
public sealed record ProgressSegment(string LabelKey, Percentage? Completion, Money? Value);

/// <summary>
/// A progress reading.
/// </summary>
/// <remarks>
/// The constructor is private and the factories are the only way in, so that spec.md §5.2 —
/// "no percentage progress bar" for Cost Plus — is enforced by the type rather than by a reviewer
/// noticing. <see cref="MonetaryOnly"/> cannot carry a percentage; there is no argument for one.
/// </remarks>
public sealed record ProgressReading
{
    private ProgressReading(
        ProgressKind kind,
        Percentage? completion,
        Money? valueToDate,
        Money? costToDate,
        Money? supervisionToDate,
        IReadOnlyList<ProgressSegment> segments)
    {
        Kind = kind;
        Completion = completion;
        ValueToDate = valueToDate;
        CostToDate = costToDate;
        SupervisionToDate = supervisionToDate;
        Segments = segments;
    }

    public ProgressKind Kind { get; }

    /// <summary>Null whenever <see cref="Kind"/> is <see cref="ProgressKind.MonetaryOnly"/>.</summary>
    public Percentage? Completion { get; }

    public Money? ValueToDate { get; }

    public Money? CostToDate { get; }

    public Money? SupervisionToDate { get; }

    public IReadOnlyList<ProgressSegment> Segments { get; }

    /// <summary>Lump Sum: certified value against contract value (spec.md §5.1).</summary>
    public static ProgressReading FromPercentage(
        Percentage completion,
        Money valueToDate,
        IReadOnlyList<ProgressSegment>? segments = null)
        => new(ProgressKind.Percentage, completion, valueToDate, null, null, segments ?? []);

    /// <summary>
    /// Cost Plus: cost-to-date plus supervision, and nothing that could be rendered as a bar
    /// (spec.md §5.2).
    /// </summary>
    public static ProgressReading MonetaryOnly(Money costToDate, Money supervisionToDate)
        => new(ProgressKind.MonetaryOnly, null, null, costToDate, supervisionToDate, []);

    /// <summary>Design: the five fixed-weight stages (spec.md §5.3).</summary>
    public static ProgressReading StageWeighted(
        Percentage completion,
        IReadOnlyList<ProgressSegment> stages,
        Money? valueToDate = null)
        => new(ProgressKind.StageWeighted, completion, valueToDate, null, null, stages);
}

/// <summary>Type-specific evidence for a progress reading.</summary>
public abstract record ProgressInput(ContractType ContractType);

/// <summary>spec.md §5.1 — certified work against contract value.</summary>
public sealed record LumpSumProgressInput(Money CertifiedToDate, IReadOnlyList<ProgressSegment> BabSegments)
    : ProgressInput(ContractType.LumpSum);

/// <summary>spec.md §5.2 — cost-to-date plus supervision. Kaff engineers' hours are internal cost, never billed.</summary>
public sealed record CostPlusProgressInput(Money BillableCostToDate, Money SupervisionToDate)
    : ProgressInput(ContractType.CostPlus);

/// <summary>spec.md §5.3 — completion per fixed-weight stage.</summary>
public sealed record DesignProgressInput(IReadOnlyDictionary<DesignStage, Percentage> StageCompletion)
    : ProgressInput(ContractType.Design);

/// <summary>Everything a progress reading is given.</summary>
public sealed record ProgressContext(Project Project, DateOnly AsOf, ProgressInput Input);

/// <summary>
/// Reports how far a project has got, in the form appropriate to its contract type.
/// </summary>
/// <remarks>
/// The second of the two seams a contract type may differ through (CLAUDE.md).
/// </remarks>
public interface IProgressMetric
{
    ContractType ContractType { get; }

    Task<Result<ProgressReading>> MeasureAsync(ProgressContext context, CancellationToken cancellationToken);
}

/// <summary>Base that performs the type check and the cast.</summary>
public abstract class ProgressMetric<TInput> : IProgressMetric
    where TInput : ProgressInput
{
    public abstract ContractType ContractType { get; }

    public Task<Result<ProgressReading>> MeasureAsync(ProgressContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Project.ContractType != ContractType)
        {
            return Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.ContractTypeMismatch));
        }

        if (context.Input is not TInput typedInput)
        {
            return Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.ProgressInputMismatch));
        }

        return MeasureCoreAsync(context, typedInput, cancellationToken);
    }

    protected abstract Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        TInput input,
        CancellationToken cancellationToken);
}
