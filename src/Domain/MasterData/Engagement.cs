using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>
/// One continuous stretch of a worker's engagement on one project. KAFF-210.
/// </summary>
/// <remarks>
/// <para>
/// spec.md §10 asks for "engagement history and per-engagement ratings" without saying what one
/// engagement is. decisions.md D-139 §3 (Nabil) rules the middle of three registered readings: "an
/// engagement is one continuous stretch of work on one project — not a single day, and not a whole
/// career." Frequency is therefore a count of engagements, and the average day rate is over
/// engagements, not days or projects.
/// </para>
/// <para>
/// <b>Closes only by an explicit manual close</b> — D-139 §3: "The engagement closes only by an
/// explicit manual close by the Site Engineer, when the worker leaves the site or the work scope
/// ends... There is no automatic day-count timeout." Nothing in this entity or elsewhere ever closes
/// one on its own.
/// </para>
/// <para>
/// <b>No pool figure is stored here.</b> Average day rate, frequency and rating are derived by
/// reading engagements, never cached — CLAUDE.md: "Never store a balance... balances are derived by
/// summing." A stored average is a stored balance under another name.
/// </para>
/// <para>
/// <b><see cref="DayRate"/> is nullable and no route in this slice sets it.</b> decisions.md D-140
/// leaves open whether a Site Engineer may see or record the agreed day rate on the shared
/// <c>DayLabourSiteManage</c> route (Q76, unanswered). Until that is ruled, no request this slice maps
/// carries a rate, so every engagement this slice creates opens with <c>DayRate: null</c>. The member
/// exists so the day the question is answered, recording one needs no schema change.
/// </para>
/// <para>
/// <b>No <c>Posting</c>, no account, no ledger.</b> This is a record of what was agreed, not an
/// instruction to pay — day labour is costed from the daily log and paid as a treasury event
/// (spec.md §10, slice 6). CLAUDE.md: the five ledgers are not reachable from here.
/// </para>
/// </remarks>
public sealed class Engagement : Entity
{
    private Engagement()
    {
    }

    private Engagement(
        Guid id,
        Guid workerId,
        Guid projectId,
        Money? dayRate,
        DateOnly openedOn,
        DateTimeOffset openedAt)
        : base(id)
    {
        WorkerId = workerId;
        ProjectId = projectId;
        DayRate = dayRate;
        OpenedOn = openedOn;
        OpenedAt = openedAt;
    }

    /// <summary>The <see cref="Employee"/> engaged. Company-wide pool — decisions.md D-140 point 3;
    /// this column is what ties an engagement to a project, never a column on <see cref="Employee"/>.</summary>
    public Guid WorkerId { get; private set; }

    /// <summary>
    /// The project this engagement authorises. decisions.md D-140 point 3's second bullet: "for an
    /// engagement, the engagement's own <c>ProjectId</c>" is where the project an act happened on is
    /// recorded — unlike <see cref="Employee"/>, which carries none.
    /// </summary>
    public Guid ProjectId { get; private set; }

    /// <summary>
    /// The agreed day rate, when one has been recorded. ⛔ Null on every engagement this slice's routes
    /// open — decisions.md D-140, Q76, unruled. See the type's remarks.
    /// </summary>
    public Money? DayRate { get; private set; }

    public DateOnly OpenedOn { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateOnly? ClosedOn { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// Out of 5 (D-139 §3). Null until rated — rule 8 (KAFF-210): an unrated man and a rating of `0`
    /// are different facts and must not render the same.
    /// </summary>
    public int? Rating { get; private set; }

    /// <summary>Computed, never stored independently — <see cref="ClosedAt"/> is the one fact.</summary>
    public bool IsOpen => ClosedAt is null;

    public static Result<Engagement> Open(
        Guid workerId,
        Guid projectId,
        DateOnly openedOn,
        DateTimeOffset openedAt,
        Money? dayRate = null)
    {
        if (dayRate is { IsPositive: false })
        {
            // Rule 4 (KAFF-210): a day rate is a record of what was agreed, never invented — an
            // explicit non-positive value is refused rather than silently accepted as "no rate yet".
            // Omitting the argument (null) is how "not recorded" is expressed instead.
            return Result.Failure<Engagement>(MasterDataErrors.EngagementDayRateMustBePositive);
        }

        return Result.Success(new Engagement(NewId(), workerId, projectId, dayRate, openedOn, openedAt));
    }

    /// <summary>
    /// Closes the engagement. decisions.md D-139 §3: manual only, and the caller — the handler, not
    /// this method — is what enforces that the closing request named this engagement's own project
    /// (rule 6a, KAFF-210, D-140's SM-30 test 14).
    /// </summary>
    public Result Close(DateOnly closedOn, DateTimeOffset closedAt)
    {
        if (!IsOpen)
        {
            return Result.Failure(MasterDataErrors.EngagementAlreadyClosed);
        }

        ClosedOn = closedOn;
        ClosedAt = closedAt;
        return Result.Success();
    }

    /// <summary>Records a rating out of 5 (D-139 §3). AC-210-F: refused outside 1–5.</summary>
    public Result Rate(int rating)
    {
        if (rating is < 1 or > 5)
        {
            return Result.Failure(MasterDataErrors.EngagementRatingOutOfRange);
        }

        Rating = rating;
        return Result.Success();
    }
}
