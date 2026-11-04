namespace Kaff.Domain.ChangeOrders;

/// <summary>
/// Where a change order sits in the spec.md section 13 machine. A priced (Lump Sum) change order runs
/// <c>Draft, TechnicalReview, ClientApproval, OwnerApproval, Approved</c>; a rejection ends it at
/// <see cref="Rejected"/> and <see cref="Withdrawn"/> is the other terminal state. Cost Plus and Design change
/// orders are documentary and unpriced: <c>Draft, Logged</c>.
/// </summary>
public enum ChangeOrderStatus
{
    Draft = 1,
    TechnicalReview = 2,
    ClientApproval = 3,
    OwnerApproval = 4,

    /// <summary>Terminal. The contract value now includes this change order.</summary>
    Approved = 5,

    /// <summary>Terminal. A gate refused it with a stored reason.</summary>
    Rejected = 6,

    /// <summary>Terminal. Taken back before approval.</summary>
    Withdrawn = 7,

    /// <summary>Terminal. A Cost Plus or Design change order, recorded and unpriced. Moves no money.</summary>
    Logged = 8,
}

/// <summary>The steps that leave a record in a change order's stored gate history.</summary>
public enum ChangeOrderGate
{
    Created = 1,
    Submitted = 2,
    TechnicalReview = 3,
    ClientApproval = 4,
    Owner = 5,
    Withdrawn = 6,
    Logged = 7,
}

public enum ChangeOrderGateOutcome
{
    Passed = 1,

    /// <summary>The change order ended at Rejected. The record carries a non-blank reason.</summary>
    Rejected = 2,
}
