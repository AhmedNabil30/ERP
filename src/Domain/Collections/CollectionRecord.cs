using Kaff.Domain.Common;

namespace Kaff.Domain.Collections;

/// <summary>How the client paid (spec.md section 6.5). The method is the recorded fact, never inferred.</summary>
public enum CollectionMethod
{
    Cash = 1,
    Cheque = 2,
    Transfer = 3,
}

/// <summary>
/// The recorded fact that a client paid an issued extract: how, when, under what reference, and what was
/// received. It stores what arrived, not a balance. The money itself lives in the postings written in the same
/// save (<c>ClientCollection</c> and <c>TaxWithheldAtSource</c>); a mistaken collection is corrected by reversing
/// those postings, never by editing this row.
/// </summary>
public sealed class CollectionRecord : Entity
{
    public const int MaxReferenceLength = 200;

    private CollectionRecord()
    {
    }

    private CollectionRecord(
        Guid projectId, Guid extractId, CollectionMethod method, DateOnly date, string reference,
        Money cashReceived, Money withheldAtSource, Guid recordedByUserId, DateTimeOffset recordedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        ExtractId = extractId;
        Method = method;
        Date = date;
        Reference = reference;
        CashReceived = cashReceived;
        WithheldAtSource = withheldAtSource;
        RecordedByUserId = recordedByUserId;
        RecordedAt = recordedAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid ExtractId { get; private set; }

    public CollectionMethod Method { get; private set; }

    public DateOnly Date { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    /// <summary>What reached the safe: the cash leg. Exactly what was received, no balancing entry (spec.md section 6.7).</summary>
    public Money CashReceived { get; private set; }

    /// <summary>What the client withheld at source: the second leg. Zero when the contract withholds nothing.</summary>
    public Money WithheldAtSource { get; private set; }

    public Guid RecordedByUserId { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>Method, date and reference are all required (spec.md section 6.5 rule 1).</summary>
    public static Result<CollectionRecord> Record(
        Guid projectId, Guid extractId, CollectionMethod? method, DateOnly? date, string? reference,
        Money cashReceived, Money withheldAtSource, Guid recordedByUserId, DateTimeOffset now)
    {
        if (method is null || !Enum.IsDefined(method.Value))
        {
            return Result.Failure<CollectionRecord>(CollectionErrors.MethodRequired);
        }

        if (date is null || date.Value == default)
        {
            return Result.Failure<CollectionRecord>(CollectionErrors.DateRequired);
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return Result.Failure<CollectionRecord>(CollectionErrors.ReferenceRequired);
        }

        string trimmed = reference.Trim();
        if (trimmed.Length > MaxReferenceLength)
        {
            return Result.Failure<CollectionRecord>(CollectionErrors.ReferenceTooLong);
        }

        return new CollectionRecord(
            projectId, extractId, method.Value, date.Value, trimmed, cashReceived, withheldAtSource, recordedByUserId, now);
    }
}
