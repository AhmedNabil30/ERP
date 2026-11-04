using System.Collections.Frozen;
using Kaff.Domain.Common;

namespace Kaff.Domain.Collections;

/// <summary>spec.md section 6.5: received, deposited, cleared, bounced.</summary>
public enum ChequeState
{
    Received = 1,
    Deposited = 2,
    Cleared = 3,
    Bounced = 4,
}

/// <summary>
/// A cheque a client paid with. It has a life of its own: a cheque clears or bounces from deposited only.
/// A state change writes audit (the interceptor, on the same save) and posts nothing: Q91 records that no
/// account type represents a cheque in hand or in transit, so the three cheque posting types stay unenforced.
/// </summary>
public sealed class Cheque : Entity
{
    private static readonly FrozenSet<(ChequeState From, ChequeState To)> LegalTransitions =
        new HashSet<(ChequeState, ChequeState)>
        {
            (ChequeState.Received, ChequeState.Deposited),
            (ChequeState.Deposited, ChequeState.Cleared),
            (ChequeState.Deposited, ChequeState.Bounced),
        }.ToFrozenSet();

    private Cheque()
    {
    }

    private Cheque(Guid projectId, Guid collectionId, Guid actorUserId, DateTimeOffset now)
        : base(NewId())
    {
        ProjectId = projectId;
        CollectionId = collectionId;
        State = ChequeState.Received;
        StateChangedByUserId = actorUserId;
        StateChangedAt = now;
    }

    public Guid ProjectId { get; private set; }

    public Guid CollectionId { get; private set; }

    public ChequeState State { get; private set; }

    public Guid StateChangedByUserId { get; private set; }

    public DateTimeOffset StateChangedAt { get; private set; }

    public static bool IsLegalTransition(ChequeState from, ChequeState to) => LegalTransitions.Contains((from, to));

    /// <summary>A cheque collection starts the cheque in <see cref="ChequeState.Received"/>.</summary>
    public static Cheque Receive(CollectionRecord collection, Guid actorUserId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return new Cheque(collection.ProjectId, collection.Id, actorUserId, now);
    }

    public Result MoveTo(ChequeState target, Guid actorUserId, DateTimeOffset now)
    {
        if (!IsLegalTransition(State, target))
        {
            return Result.Failure(CollectionErrors.ChequeIllegalTransition);
        }

        State = target;
        StateChangedByUserId = actorUserId;
        StateChangedAt = now;
        return Result.Success();
    }
}
