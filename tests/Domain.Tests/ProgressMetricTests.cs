using System.Reflection;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Contracts.Progress;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>
/// spec.md section 5: progress by contract type. Lump Sum is a percentage that follows the work; Cost Plus is money
/// and never a percentage (section 5.2); Design fails rather than invent a number (section 5.3).
/// </summary>
public sealed class ProgressMetricTests
{
    private readonly RulingSet _rulings = RulingSet.Default;
    private readonly Project _project;
    private readonly Boq _boq;
    private readonly BoqLine _concrete;
    private readonly BoqLine _paint;

    public ProgressMetricTests()
    {
        Bab concreteBab = Bab.Create("CON", "أعمال خرسانة", "Concrete Works", Percentage.Zero).Value;
        Bab paintBab = Bab.Create("PNT", "أعمال دهانات", "Paint Works", Percentage.Zero).Value;
        CatalogueItem concreteItem = CatalogueItem.Create(
            "CON-1", "خرسانة", "م٣", concreteBab.Id, new Money(500m), new Money(1_000m), "Concrete").Value;
        CatalogueItem paintItem = CatalogueItem.Create(
            "PNT-1", "دهان", "م٢", paintBab.Id, new Money(50m), new Money(100m), "Paint").Value;

        _project = Project.Create("PRJ-P", "P", TestAccounts.ClientId, ContractType.LumpSum, TestAccounts.Now).Value;
        _project.SetLumpSumTerms(
            ContractDefaults.AdvanceRate, ContractDefaults.HoldRate, ContractDefaults.AdvanceRecoveryRate,
            ContractDefaults.MaterialAdvanceRate, delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();

        _boq = Boq.Open(_project, TestAccounts.Now).Value;

        // 10 x 1,000 = 10,000 and 100 x 100 = 10,000, no conditions and no markup, so 20,000 in all.
        _concrete = _boq.AddCatalogueLine(concreteItem, concreteBab, 10m, 10m).Value;
        _paint = _boq.AddCatalogueLine(paintItem, paintBab, 100m, 100m).Value;
        _project.Sign(new DateOnly(2026, 1, 1), _boq.Total(_rulings.ContractValueSource).Value).IsSuccess.Should().BeTrue();
        _boq.Freeze(new DateOnly(2026, 1, 1)).IsSuccess.Should().BeTrue();
    }

    private async Task<ProgressReading> MeasureAsync()
    {
        LumpSumProgressMetric metric = new();
        Result<ProgressInput> input = metric.InputFrom(_project, new ProgressEvidence(_boq, Money.Zero, _rulings));
        input.IsSuccess.Should().BeTrue();
        Result<ProgressReading> reading = await metric.MeasureAsync(
            new ProgressContext(_project, new DateOnly(2026, 3, 1), input.Value), CancellationToken.None);
        reading.IsSuccess.Should().BeTrue();
        return reading.Value;
    }

    [Fact]
    public async Task Lump_sum_completion_is_executed_value_over_contract_value_and_moves_with_the_executed_quantity()
    {
        (await MeasureAsync()).Completion.Should().Be(Percentage.Zero, "nothing has been executed, and that zero is a measurement");

        _concrete.ApplyExecutedChange(5m).IsSuccess.Should().BeTrue();   // 5,000 of 20,000
        (await MeasureAsync()).Completion.Should().Be(Percentage.FromPercent(25m));

        _paint.ApplyExecutedChange(100m).IsSuccess.Should().BeTrue();    // plus 10,000
        ProgressReading reading = await MeasureAsync();
        reading.Completion.Should().Be(Percentage.FromPercent(75m));
        reading.ValueToDate.Should().Be(new Money(15_000m));
        reading.Kind.Should().Be(ProgressKind.Percentage);
    }

    [Fact]
    public async Task Lump_sum_reports_a_segment_per_bab_and_marks_the_started_unfinished_one_active()
    {
        _concrete.ApplyExecutedChange(5m).IsSuccess.Should().BeTrue();   // CON half done
        _paint.ApplyExecutedChange(100m).IsSuccess.Should().BeTrue();    // PNT done

        ProgressReading reading = await MeasureAsync();

        reading.Segments.Select(segment => segment.LabelKey).Should().Equal("CON", "PNT");
        ProgressSegment concrete = reading.Segments[0];
        ProgressSegment paint = reading.Segments[1];
        concrete.Completion.Should().Be(Percentage.FromPercent(50m));
        concrete.Value.Should().Be(new Money(5_000m));
        BabActivity.IsActive(concrete).Should().BeTrue("work has begun and it is not finished");
        BabActivity.IsActive(paint).Should().BeFalse("it is complete");
    }

    [Fact]
    public async Task A_bab_with_no_executed_work_is_not_active()
    {
        ProgressReading reading = await MeasureAsync();

        reading.Segments.Should().OnlyContain(segment => !BabActivity.IsActive(segment));
    }

    [Fact]
    public void Lump_sum_without_a_boq_fails_rather_than_report_a_zero()
    {
        Result<ProgressInput> input = new LumpSumProgressMetric().InputFrom(_project, new ProgressEvidence(null, Money.Zero, _rulings));

        input.IsFailure.Should().BeTrue();
        input.Error.Should().Be(BoqErrors.Required);
    }

    [Fact]
    public async Task Cost_plus_reports_money_and_no_percentage()
    {
        Project costPlus = Project.Create("PRJ-C", "C", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;
        costPlus.SetCostPlusTerms(Percentage.FromPercent(10m)).IsSuccess.Should().BeTrue();
        CostPlusProgressMetric metric = new();

        Result<ProgressInput> input = metric.InputFrom(costPlus, new ProgressEvidence(null, new Money(200_000m), _rulings));
        input.IsSuccess.Should().BeTrue();
        Result<ProgressReading> reading = await metric.MeasureAsync(
            new ProgressContext(costPlus, new DateOnly(2026, 3, 1), input.Value), CancellationToken.None);

        reading.IsSuccess.Should().BeTrue();
        reading.Value.Kind.Should().Be(ProgressKind.MonetaryOnly);
        reading.Value.Completion.Should().BeNull("spec.md section 5.2: no percentage progress bar");
        reading.Value.Segments.Should().BeEmpty();
        reading.Value.CostToDate.Should().Be(new Money(200_000m));
        reading.Value.SupervisionToDate.Should().Be(new Money(20_000m));
    }

    [Fact]
    public void The_monetary_only_factory_has_no_parameter_that_could_carry_a_percentage()
    {
        MethodInfo factory = typeof(ProgressReading).GetMethod(nameof(ProgressReading.MonetaryOnly))!;

        factory.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().OnlyContain(type => type == typeof(Money), "a Cost Plus reading takes money and nothing else");
    }

    [Fact]
    public async Task Design_progress_fails_because_nothing_stores_stage_completion()
    {
        Project design = Project.Create("PRJ-D", "D", TestAccounts.ClientId, ContractType.Design, TestAccounts.Now).Value;
        DesignProgressMetric metric = new();

        metric.InputFrom(design, new ProgressEvidence(null, Money.Zero, _rulings)).Error
            .Should().Be(BillingErrors.CalculatorNotImplemented);

        Result<ProgressReading> reading = await metric.MeasureAsync(
            new ProgressContext(design, new DateOnly(2026, 3, 1), new DesignProgressInput(new Dictionary<DesignStage, Percentage>())),
            CancellationToken.None);
        reading.IsFailure.Should().BeTrue();
        reading.Error.Should().Be(BillingErrors.CalculatorNotImplemented);
    }

    [Fact]
    public void The_dispatcher_hands_each_contract_type_its_own_metric()
    {
        ContractTypeDispatcher dispatcher = new([], [new LumpSumProgressMetric(), new CostPlusProgressMetric(), new DesignProgressMetric()]);

        foreach (ContractType type in Enum.GetValues<ContractType>())
        {
            dispatcher.ProgressMetricFor(type).Value.ContractType.Should().Be(type);
        }
    }
}
