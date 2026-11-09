using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Projects;

/// <summary>A project just opened, with the account set that opens in the same save. Neither is persisted yet.</summary>
public sealed record OpenedProject(Project Project, IReadOnlyList<Account> Accounts);

/// <summary>
/// The one way a project is opened: <c>CreateProject</c> and the design-to-execution conversion (KAFF-415) both come
/// through here, so a converted project's account set is built exactly as any new project's (AC-415-C).
/// </summary>
public static class ProjectOpening
{
    /// <summary>
    /// Builds a project and its account set. A Lump Sum project takes <paramref name="advanceRate"/> and
    /// <paramref name="holdRate"/> where given and spec.md section 5.1's stated defaults for the rest.
    /// </summary>
    public static Result<OpenedProject> Open(
        string? name,
        Guid clientId,
        ContractType contractType,
        Percentage? advanceRate,
        Percentage? holdRate,
        DateTimeOffset now)
    {
        Result<Project> created = Project.Create(GenerateCode(), name ?? string.Empty, clientId, contractType, now);
        if (created.IsFailure)
        {
            return Result.Failure<OpenedProject>(created.Error);
        }

        Project project = created.Value;

        if (contractType == ContractType.LumpSum)
        {
            Result termsSet = project.SetLumpSumTerms(
                advanceRate ?? ContractDefaults.AdvanceRate,
                holdRate ?? ContractDefaults.HoldRate,
                ContractDefaults.AdvanceRecoveryRate,
                ContractDefaults.MaterialAdvanceRate,
                ContractDefaults.DelayPenaltyEnabled);
            if (termsSet.IsFailure)
            {
                return Result.Failure<OpenedProject>(termsSet.Error);
            }
        }

        Result<IReadOnlyList<Account>> accounts = ProjectAccountSetFactory.Create(
            project, existingTypes: new HashSet<AccountType>(), DateOnly.FromDateTime(now.UtcDateTime));

        return accounts.IsFailure
            ? Result.Failure<OpenedProject>(accounts.Error)
            : new OpenedProject(project, accounts.Value);
    }

    /// <summary>
    /// Opens the execution project a design project converts into (spec.md section 5.4, KAFF-415): same client, a type
    /// the user chose, its own account set, and a <see cref="ProjectLinkType.DesignToExecution"/> link back to the design.
    /// The design project itself is not touched: a project never mutates from one type into another.
    /// </summary>
    /// <param name="alreadyConverted">True when an execution project already links to <paramref name="design"/>.</param>
    public static Result<OpenedProject> ConvertToExecution(
        Project design,
        bool alreadyConverted,
        string? name,
        ContractType executionType,
        RulingSet rulings,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(rulings);

        if (design.ContractType != ContractType.Design)
        {
            return Result.Failure<OpenedProject>(ProjectErrors.NotDesign);
        }

        if (!rulings.DesignConversionTargets.Allows(executionType))
        {
            return Result.Failure<OpenedProject>(ProjectErrors.ExecutionTypeNotAllowed);
        }

        if (alreadyConverted)
        {
            return Result.Failure<OpenedProject>(ProjectErrors.AlreadyConverted);
        }

        Result<OpenedProject> opened = Open(name, design.ClientId, executionType, null, null, now);
        if (opened.IsFailure)
        {
            return opened;
        }

        Result linked = opened.Value.Project.LinkTo(design.Id, ProjectLinkType.DesignToExecution);
        return linked.IsFailure ? Result.Failure<OpenedProject>(linked.Error) : opened;
    }

    /// <summary>
    /// <c>PRJ-</c> plus eight hex characters from a fresh <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// <b>A named, deliberate simplification, not the house pattern.</b> <c>Client</c>, <c>Employee</c>,
    /// <c>Subcontractor</c> and <c>Supplier</c> all draw a sequential code from a PostgreSQL sequence, because a
    /// read-max-and-increment loses a race between two concurrent registrations (decisions.md D-107 section 1). There
    /// is no <c>project_code_seq</c> today. A <see cref="Guid"/>-derived code needs no sequence and cannot lose that
    /// race, at the cost of a code that is not sequential. Upgrade path: add <c>project_code_seq</c> in a migration.
    /// </remarks>
    private static string GenerateCode() =>
        "PRJ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
