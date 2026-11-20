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
/// portion through <see cref="Extract.ReleaseHeldPortion"/>, which records the release and posts nothing
/// (<c>ExtractPartialApprovalScope</c>). Carries no money: the held amount is derived from the extract on every read.
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

    private Ncr(Guid projectId, Guid extractId, Guid openedBy, DateTimeOffset openedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        ExtractId = extractId;
        Status = NcrStatus.Open;
        OpenedByUserId = openedBy;
        OpenedAt = openedAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid ExtractId { get; private set; }

    public NcrStatus Status { get; private set; }

    public Guid OpenedByUserId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? CloseNote { get; private set; }

    /// <summary>Opened for the extract whose Technical Office gate is being split.</summary>
    public static Ncr Open(Extract extract, Guid openedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extract);
        return new Ncr(extract.ProjectId, extract.Id, openedBy, now);
    }

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
