using Kaff.Domain.Contracts;

namespace Kaff.Domain.Rulings;

/// <summary>
/// Q-207-5: what "design quantities seed the execution BOQ" (spec.md section 5.4) means, given a design project has no
/// BOQ (spec.md section 5.3). Registered, not resolved.
/// </summary>
public sealed record DesignQuantitySeed : IRuling
{
    public static readonly DesignQuantitySeed Default = new();

    public RulingNote Note => new(
        "DesignQuantitySeed",
        "Q-207-5: what are the \"design quantities\" that seed the execution BOQ, given a design project has no BOQ?",
        "What the execution project receives from the design project when it is created.",
        "No BOQ line is seeded. The execution BOQ starts empty with its explicit empty state, and the design area is "
        + "carried as a reference on the execution project's GET (designAreaSquareMetres). ASSUMED.",
        "If the client means BOQ lines, a design-quantity list has to exist on the design project first; nothing "
        + "holds one today. Seeding would then be a step in ConvertToExecution that copies values, never references "
        + "(CLAUDE.md, BOQ frozen by copy).",
        RulingStatus.Assumed);
}

/// <summary>
/// Q-207-6: whether "the design total" (spec.md section 5.4) is the design contract value or what the client has paid.
/// Registered, not resolved.
/// </summary>
public sealed record DesignCreditBase : IRuling
{
    public static readonly DesignCreditBase Default = new();

    public RulingNote Note => new(
        "DesignCreditBase",
        "Q-207-6: is \"the design total\" the design contract value, or what the client has paid on the design project?",
        "The amount the 30% design credit is taken of.",
        "The design project's contract value (area x rate), times ContractDefaults.DesignToExecutionCreditRate (30%). "
        + "A design project with no contract value refuses the execution signature with "
        + "errors.adjustment.design_value_missing rather than raising a zero or skipped credit. ASSUMED.",
        "Basing it on what was paid means summing the design project's collection postings at signature in "
        + "Adjustment.RaiseDesignCredit's caller; the rate stays where it is.",
        RulingStatus.Assumed);
}

/// <summary>Q-207-7: whether one design project may convert into more than one execution project. Registered, not resolved.</summary>
public sealed record DesignConversionCount : IRuling
{
    public static readonly DesignConversionCount Default = new();

    public RulingNote Note => new(
        "DesignConversionCount",
        "Q-207-7: may one design project convert into more than one execution project?",
        "How many execution projects may link back to one design project.",
        "One. A second conversion is refused with errors.project.already_converted, checked under a row lock on the "
        + "design project. ASSUMED.",
        "Allowing several also needs a ruling on how the one 30% credit is split between them; today each execution "
        + "signature would raise its own full 30%.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q-207-8: whether the 30% design credit posts at signature by itself or goes through approval. Registered, not
/// resolved.
/// </summary>
public sealed record DesignCreditApproval : IRuling
{
    public static readonly DesignCreditApproval Default = new();

    public RulingNote Note => new(
        "DesignCreditApproval",
        "Q-207-8: does the 30% design credit post at execution signature by itself, or does it go through approval?",
        "When the design credit's posting is written and who may write it.",
        "Raised at signature in the same save as the signature, status Raised, nothing posted. It posts when a "
        + "different user approves it through the existing adjustment approval route (FinancialMovementApprove); "
        + "the signer is refused as its raiser. The posting is the client credit note's pair: client receivable to "
        + "the execution project's contract revenue, PostingType.CreditNote. ASSUMED.",
        "Posting at signature means the signature handler calls Adjustment.Approve itself, which then needs a ruling "
        + "on who the second actor is, because nobody may create and approve the same movement (CLAUDE.md).",
        RulingStatus.Assumed);
}

/// <summary>Q-207-9: which execution types a design project may convert into. Registered, not resolved.</summary>
public sealed record DesignConversionTargets : IRuling
{
    public static readonly DesignConversionTargets Default = new();

    public RulingNote Note => new(
        "DesignConversionTargets",
        "Q-207-9: which execution types may a design project convert into?",
        "The contract types a conversion may create.",
        "Lump Sum or Cost Plus, the user's choice. Design is refused with errors.project.execution_type_not_allowed. "
        + "ASSUMED.",
        "Change Allows below.",
        RulingStatus.Assumed);

    /// <summary>True when a design project may convert into a project of <paramref name="type"/>.</summary>
#pragma warning disable CA1822 // An instance member on purpose: callers reach it through RulingSet, so a test can swap the ruling.
    public bool Allows(ContractType type) => type is ContractType.LumpSum or ContractType.CostPlus;
#pragma warning restore CA1822
}
