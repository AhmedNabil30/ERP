using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Contracts.Progress;

namespace Kaff.Domain.Contracts;

/// <summary>
/// Resolves the calculator and progress metric for a contract type.
/// </summary>
/// <remarks>
/// This is dispatch, not a service layer: it forwards nothing and adds no behaviour beyond the
/// lookup. It exists so that a handler writes <c>dispatcher.BillingCalculatorFor(project.ContractType)</c>
/// instead of a <c>switch</c> that would have to be repeated — and eventually diverge — in every
/// feature that bills.
/// </remarks>
public interface IContractTypeDispatcher
{
    Result<IBillingCalculator> BillingCalculatorFor(ContractType contractType);

    Result<IProgressMetric> ProgressMetricFor(ContractType contractType);
}

/// <summary>
/// Builds its lookup from whatever implementations were registered.
/// </summary>
/// <remarks>
/// Constructed from plain collections, so Domain still has no dependency on a DI container.
/// Registration lives in Kaff.Infrastructure. A duplicate registration for the same contract type is
/// a wiring error and throws at startup rather than picking one silently.
/// </remarks>
public sealed class ContractTypeDispatcher : IContractTypeDispatcher
{
    private readonly Dictionary<ContractType, IBillingCalculator> _calculators;
    private readonly Dictionary<ContractType, IProgressMetric> _metrics;

    public ContractTypeDispatcher(IEnumerable<IBillingCalculator> calculators, IEnumerable<IProgressMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(calculators);
        ArgumentNullException.ThrowIfNull(metrics);

        _calculators = new Dictionary<ContractType, IBillingCalculator>();
        foreach (IBillingCalculator calculator in calculators)
        {
            if (!_calculators.TryAdd(calculator.ContractType, calculator))
            {
                throw new InvalidOperationException(
                    $"More than one IBillingCalculator is registered for {calculator.ContractType}.");
            }
        }

        _metrics = new Dictionary<ContractType, IProgressMetric>();
        foreach (IProgressMetric metric in metrics)
        {
            if (!_metrics.TryAdd(metric.ContractType, metric))
            {
                throw new InvalidOperationException(
                    $"More than one IProgressMetric is registered for {metric.ContractType}.");
            }
        }
    }

    public Result<IBillingCalculator> BillingCalculatorFor(ContractType contractType)
        => _calculators.TryGetValue(contractType, out IBillingCalculator? calculator)
            ? Result.Success(calculator)
            : Result.Failure<IBillingCalculator>(BillingErrors.NoCalculatorRegistered);

    public Result<IProgressMetric> ProgressMetricFor(ContractType contractType)
        => _metrics.TryGetValue(contractType, out IProgressMetric? metric)
            ? Result.Success(metric)
            : Result.Failure<IProgressMetric>(BillingErrors.NoProgressMetricRegistered);
}
