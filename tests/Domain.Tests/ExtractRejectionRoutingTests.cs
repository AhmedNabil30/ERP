using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;

namespace Kaff.Domain.Tests;

/// <summary>
/// D-242 item 2 (amends spec.md section 7): a rejection goes to whoever can fix its reason. Wrong quantities return the
/// extract to the site engineer at preparation; wrong material values return it to the supervisor at the QC report.
/// </summary>
public sealed class ExtractRejectionRoutingTests
{
    private readonly ExtractHarness _h = new();

    private Extract AtTechnicalOffice()
    {
        Extract extract = _h.Prepare(ExtractHarness.NewUser());
        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        return extract;
    }

    [Fact]
    public void Wrong_quantities_return_the_extract_to_the_site_engineer_and_the_qc_report_waits_for_his_revision()
    {
        Extract extract = AtTechnicalOffice();

        extract.Reject(ExtractHarness.NewUser(), "short by 20 m3", ExtractRejectionKind.Quantities, _h.Context(), [], ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();

        extract.Status.Should().Be(ExtractStatus.Draft);
        ExtractGateRecord rejection = extract.GateHistory[^1];
        rejection.RejectionKind.Should().Be(ExtractRejectionKind.Quantities);
        rejection.Reason.Should().Be("short by 20 m3");
        extract.IsRevisable.Should().BeTrue();
        ExtractWaiting.For(extract).Permission.Should().Be(Permission.ExtractPrepare, "the site engineer fixes the quantities");

        extract.SubmitQcReport(ExtractHarness.NewUser(), new Money(100_000m), Money.Zero, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.AwaitingEvidenceRevision);
        extract.Status.Should().Be(ExtractStatus.Draft);

        extract.ReviseEvidence(ExtractHarness.NewUser(), new Money(280_000m), new Money(100_000m), Money.Zero, ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        ExtractWaiting.For(extract).Permission.Should().Be(Permission.ExtractQcReportSubmit);
        extract.SubmitQcReport(ExtractHarness.NewUser(), new Money(100_000m), Money.Zero, ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.Status.Should().Be(ExtractStatus.QcSubmitted);
    }

    [Fact]
    public void Wrong_material_values_return_the_extract_to_the_supervisor_at_the_qc_report_and_not_to_the_site_engineer()
    {
        Extract extract = AtTechnicalOffice();

        extract.Reject(ExtractHarness.NewUser(), "on-site value overstated", ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();

        extract.Status.Should().Be(ExtractStatus.Draft);
        extract.GateHistory[^1].RejectionKind.Should().Be(ExtractRejectionKind.MaterialValues);
        extract.IsRevisable.Should().BeFalse("the quantities were not questioned");
        extract.ReviseEvidence(ExtractHarness.NewUser(), new Money(300_000m), new Money(50_000m), Money.Zero, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.RevisionNotAllowed);
        ExtractWaiting.For(extract).Permission.Should().Be(Permission.ExtractQcReportSubmit, "the supervisor enters the values again");

        extract.SubmitQcReport(ExtractHarness.NewUser(), new Money(50_000m), Money.Zero, ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.MaterialValueOnSite.Should().Be(new Money(50_000m));
    }

    [Fact]
    public void A_rejection_without_a_kind_is_refused_and_moves_nothing()
    {
        Extract extract = AtTechnicalOffice();
        int records = extract.GateHistory.Count;

        extract.Reject(ExtractHarness.NewUser(), "no", null, _h.Context(), [], ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.RejectionKindRequired);
        extract.Status.Should().Be(ExtractStatus.QcSubmitted);
        extract.GateHistory.Should().HaveCount(records);
    }
}
