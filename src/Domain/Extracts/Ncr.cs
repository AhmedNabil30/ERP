using Kaff.Domain.Common;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Extracts;

/// <summary>Whether the NCR still holds back part of an extract.</summary>
public enum NcrStatus
{
    Open = 1,
    Closed = 2,
}

/// <summary>
/// The non-conformance report a split Technical Office gate holds the remainder of an extract against (spec.md section 7,
/// AC-500-E, D-250 item 1). Opened by the partial approval itself, closed once, with a note. Closing it releases the held
/// portion through <see cref="Extract.ReleaseHeldPortion"/>, which records the release and posts nothing; the next
/// extract prepared after the close bills it (<c>ExtractPartialApprovalScope</c>, D-253). Carries no money: the held amount is derived from the extract on every read.
/// </summary>
/// <remarks>
/// Who closes it is the <c>NcrCloser</c> ruling, an assumption: the Technical Office. spec.md does not bar the person who
/// split the gate from closing the NCR, so no separation of duties is applied here.
/// </remarks>
public sealed class Ncr : Entity
{
    private Ncr()
    {
    }

    private Ncr(Guid projectId, Guid extractId, string description, Guid openedBy, DateTimeOffset openedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        ExtractId = extractId;
        Description = description;
        Status = NcrStatus.Open;
        OpenedByUserId = openedBy;
        OpenedAt = openedAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid ExtractId { get; private set; }

    public NcrStatus Status { get; private set; }

    /// <summary>
    /// What does not conform, as the Technical Office wrote it when it split the gate. Null only on an NCR opened before
    /// the description was required (D-260 item 1); every NCR opened since carries one.
    /// </summary>
    public string? Description { get; private set; }

    public Guid OpenedByUserId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? CloseNote { get; private set; }

    /// <summary>Opened for the extract whose Technical Office gate is being split, with what does not conform.</summary>
    public static Result<Ncr> Open(Extract extract, string? description, Guid openedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extract);
        Result described = CheckDescription(description);
        return described.IsFailure
            ? Result.Failure<Ncr>(described.Error)
            : new Ncr(extract.ProjectId, extract.Id, description!.Trim(), openedBy, now);
    }

    /// <summary>
    /// A second split of the same extract (after a rejection returned it to Draft) reuses its open NCR, and the gate
    /// writes what does not conform now. The interceptor audits the change with its before and after.
    /// </summary>
    public Result Describe(string? description)
    {
        Result described = CheckDescription(description);
        if (described.IsFailure)
        {
            return described;
        }

        if (Status == NcrStatus.Closed)
        {
            return Result.Failure(ExtractErrors.NcrAlreadyClosed);
        }

        Description = description!.Trim();
        return Result.Success();
    }

    private static Result CheckDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) || description.Trim().Length > Project.MaxReasonLength
            ? Result.Failure(ExtractErrors.NcrDescriptionRequired)
            : Result.Success();

    /// <summary>
    /// Closes the NCR with its note and, when <paramref name="extract"/> still holds its remainder against this NCR,
    /// releases it. An extract that a rejection returned to Draft holds nothing any more, so the NCR closes alone.
    /// </summary>
    public Result Close(Extract extract, string? note, Guid actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extract);

        if (extract.Id != ExtractId)
        {
            throw new InvalidOperationException("The NCR is closed against its own extract.");
        }

        if (Status == NcrStatus.Closed)
        {
            return Result.Failure(ExtractErrors.NcrAlreadyClosed);
        }

        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure(ExtractErrors.NcrCloseNoteRequired);
        }

        if (extract.HeldForNcrId == Id && extract.HeldPortionReleasedAt is null)
        {
            Result released = extract.ReleaseHeldPortion(actor, now);
            if (released.IsFailure)
            {
                return released;
            }
        }

        Status = NcrStatus.Closed;
        ClosedByUserId = actor;
        ClosedAt = now;
        CloseNote = note.Trim();
        return Result.Success();
    }
}
