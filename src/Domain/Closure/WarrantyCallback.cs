using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Closure;

/// <summary>
/// A defect reported during the warranty (spec.md section 11). Attached to the project's warranty, which starts at
/// handover. Carries no cost: who pays is Q112 and needs the Adjustment object (KAFF-512), so nothing a callback does
/// touches a posting, and the hold was released once at handover.
/// </summary>
public sealed class WarrantyCallback : Entity
{
    public const int MaxDescriptionLength = 1000;

    private WarrantyCallback()
    {
    }

    private WarrantyCallback(Guid projectId, int number, string description, DateOnly raisedOn, Guid raisedBy, DateTimeOffset now)
        : base(NewId())
    {
        ProjectId = projectId;
        Number = number;
        Description = description;
        RaisedOn = raisedOn;
        RaisedByUserId = raisedBy;
        RaisedAt = now;
    }

    public Guid ProjectId { get; private set; }

    public int Number { get; private set; }

    public string Description { get; private set; } = null!;

    public DateOnly RaisedOn { get; private set; }

    public Guid RaisedByUserId { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsResolved => ResolvedAt is not null;

    /// <summary>
    /// RULING: Q116. A callback attaches to a warranty that has started (Handover or UnderWarranty) and has not ended.
    /// See <c>Rulings.WarrantyCallbackWindow</c>.
    /// </summary>
    public static Result<WarrantyCallback> Raise(
        Project project, int number, string? description, DateOnly today, Guid raisedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.ContractType == ContractType.Design)
        {
            return Result.Failure<WarrantyCallback>(ClosureErrors.NotApplicableToDesign);
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > MaxDescriptionLength)
        {
            return Result.Failure<WarrantyCallback>(ClosureErrors.CallbackDescriptionRequired);
        }

        if (project.Status is not (ProjectStatus.Handover or ProjectStatus.UnderWarranty) || project.WarrantyEndsOn is null)
        {
            return Result.Failure<WarrantyCallback>(ClosureErrors.CallbackNoWarranty);
        }

        if (today > project.WarrantyEndsOn)
        {
            return Result.Failure<WarrantyCallback>(ClosureErrors.CallbackAfterWarranty);
        }

        return Result.Success(new WarrantyCallback(project.Id, number, description.Trim(), today, raisedBy, now));
    }

    public Result Resolve(Guid actor, DateTimeOffset now)
    {
        if (IsResolved)
        {
            return Result.Failure(ClosureErrors.CallbackAlreadyResolved);
        }

        ResolvedByUserId = actor;
        ResolvedAt = now;
        return Result.Success();
    }
}
