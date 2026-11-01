namespace Kaff.Domain.Rulings;

/// <summary>Whether the site engineer types an amount when he drafts a site expense.</summary>
public enum SiteExpenseDraftAmountMode
{
    /// <summary>The engineer types the figure; Accounts confirms and posts it. spec.md section 8, rule 9.</summary>
    EngineerTypesAmount = 1,

    /// <summary>The engineer describes the expense only; Accounts supplies the figure. KAFF-600 AC-600-J.</summary>
    AccountsSuppliesAmount = 2,
}

/// <summary>
/// Does the engineer type an amount on an expense draft? spec.md section 8 and KAFF-600 collide: rule 9 says an
/// engineer's expense entry is a draft that Accounts confirms and posts, and a draft of an expense carries a
/// figure; AC-600-J says the engineer's screen shows no rate, amount, cost or balance anywhere. Both cannot be
/// satisfied as written. Registered, not resolved.
/// </summary>
/// <remarks>
/// Running assumption, not a ruling: <c>DraftExpense</c> takes a <c>Money Amount</c>, so the engineer types a
/// figure. The engineer's own read (<c>GET .../daily-logs/mine</c>) does not echo it back, so the amount does
/// not reach his screen after entry. Nothing in the code reads <see cref="Default"/> yet: the endpoint and the
/// <c>SiteExpenseDraft</c> entity still require an amount. Changing the default alone changes nothing until
/// that work is done.
/// </remarks>
public sealed record SiteExpenseDraftAmount(SiteExpenseDraftAmountMode Mode) : IRuling
{
    public static readonly SiteExpenseDraftAmount Default = new(SiteExpenseDraftAmountMode.EngineerTypesAmount);

    public RulingNote Note => new(
        "SiteExpenseDraftAmount",
        "KAFF-600: does the engineer type an amount on an expense draft? spec.md section 8 rule 9 (his entry is a "
        + "draft that Accounts confirms and posts) collides with AC-600-J (his screen shows no rate, amount, cost or "
        + "balance anywhere). Either he types a figure and AC-600-J is wrong, or he does not and the endpoint is wrong.",
        "Whether a site expense draft carries an amount entered by the engineer.",
        $"Mode {Mode}: DraftExpense takes a Money Amount from the engineer, which must be positive. GET daily-logs/mine "
        + "returns the draft's id, description and time only, never the amount. ASSUMED.",
        "If AccountsSuppliesAmount: Request drops Amount, SiteExpenseDraft stores the amount as null until Accounts "
        + "enters it at confirmation, and the Accounts confirmation step gains the amount field. If EngineerTypesAmount "
        + "is ruled: AC-600-J is reworded to allow an amount on the draft entry screen. Stored drafts keep their amounts.",
        RulingStatus.Assumed);
}
