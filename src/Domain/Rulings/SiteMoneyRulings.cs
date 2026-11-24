namespace Kaff.Domain.Rulings;

/// <summary>R2-Q7 item 4: a payment the safe cannot cover is refused, not parked. Registered, not resolved.</summary>
public sealed record RefusedPaymentAfterInjection : IRuling
{
    public static readonly RefusedPaymentAfterInjection Default = new();

    public RulingNote Note => new(
        "RefusedPaymentAfterInjection",
        "R2-Q7 item 4 (KAFF-313): when a payment is refused because the safe cannot cover it, is the payment kept pending "
        + "until the Owner injects, or does the user repeat it after the injection? spec.md section 6.1 says only that the "
        + "refusal \"prompts an owner injection\". Who may record the injection is R2-Q7 item 1 and has no default.",
        "What happens to a payment the safe floor refuses.",
        "The database refuses the posting (KAFF_NEGATIVE_BALANCE, 409 treasury.negative_balance) and nothing is stored. "
        + "After an injection the user repeats the payment. No pending payment record exists. ASSUMED.",
        "If the payment is kept pending, a pending-payment record and a release step are added, and the injection triggers "
        + "or offers the release. The refusal itself does not change.",
        RulingStatus.Assumed);
}

/// <summary>R2-Q10: whether the weekly QC report is also a step of the subcontractor extract chain. Registered, not resolved.</summary>
public sealed record QcReportScope : IRuling
{
    public static readonly QcReportScope Default = new();

    public RulingNote Note => new(
        "QcReportScope",
        "R2-Q10 (KAFF-612): is the weekly QC report a step of the client extract only, or does a subcontractor extract "
        + "also need one? spec.md section 7 lists QC in the client chain and not in the subcontractor chain.",
        "Which extract chains carry the QC report step.",
        "The client extract chain only. The subcontractor extract chain (KAFF-510) is not built and has no QC step. ASSUMED.",
        "If the subcontractor chain also needs it, the subcontractor extract gains a QcSubmitted state before the Technical "
        + "Office gate and reuses ExtractQcReportSubmit.",
        RulingStatus.Assumed);
}

/// <summary>R2-Q8: who marks material as stockpiled, which starts the 75% تشوينات. Registered, not resolved.</summary>
public sealed record StockpileMarker : IRuling
{
    public static readonly StockpileMarker Default = new();

    public RulingNote Note => new(
        "StockpileMarker",
        "R2-Q8 (Q-225-1, KAFF-309): who marks material as stockpiled on site, the engineer on the daily log or Accounts "
        + "when the extract is prepared?",
        "Who supplies the value of material stockpiled on site that the تشوينات is issued against.",
        "The supervising site engineer enters it at the weekly QC report (D-235 item 4). The daily log does not mark "
        + "material as stockpiled. ASSUMED.",
        "If the daily log marks it, the QC report reads the logged value instead of asking for it. If Accounts enters it, "
        + "the value moves to the deductions step.",
        RulingStatus.Assumed);
}
