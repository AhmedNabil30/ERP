using Kaff.Api.Common.Results;
using Kaff.Api.Features.Adjustments;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.SignProject;

/// <summary>
/// Signs the contract. The BOQ total becomes the contract value, the BOQ is frozen and the date is
/// stamped — all three in the one <c>SaveChangesAsync</c>, so a project is never signed with a BOQ that
/// can still change, nor a BOQ frozen under a project that is not signed.
/// </summary>
/// <remarks>
/// Which source each contract type takes its value from, which quantity prices a line, and how a line
/// rounds are rulings (<see cref="ContractValueSource"/>), not code here. No audit record is written by
/// hand: the project and the BOQ header are tracked entities, so the interceptor writes one <c>Modified</c>
/// record for each. Freezing touches no line row, so a 400-line BOQ writes two records, not 402.
/// <para>
/// <b>An execution project converted from a design (KAFF-415)</b> raises its 30% design credit in the same save: one
/// <see cref="AdjustmentType.DesignCredit"/> adjustment, status Raised, nothing posted. The signer is its raiser, so the
/// signer cannot approve it (Q-207-8). The two accounts its posting will need are opened here if the project lacks
/// them: a Lump Sum project has no revenue account until its first extract, and a Cost Plus project has no client
/// sub-ledger at all.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        Project? project = await database.Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        // AC-417-J: an archived project is reopened before it can be signed.
        if (project.IsArchived(clock.GetUtcNow(), rulings.FollowUp))
        {
            return ResultExtensions.Problem(ProjectErrors.Archived);
        }

        Boq? boq = await database.Boqs
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        if (project.IsSigned)
        {
            return ResultExtensions.Problem(ProjectErrors.AlreadySigned);
        }

        // AC-417-K: a Design project's area is entered here; no other route sets it.
        if (request.AreaSquareMetres is not null && project.ContractType != ContractType.Design)
        {
            return ResultExtensions.Problem(ProjectErrors.SigningTermsNotForContractType);
        }

        if (project.ContractType == ContractType.LumpSum)
        {
            // D-217 item 29, KAFF-418 AC-9: the hold and the advance start empty and are typed for every project.
            // A missing figure is refused; no default is applied.
            if (request.HoldPercentage is not { } hold || request.ClientAdvance is not { } advance)
            {
                return ResultExtensions.Problem(ProjectErrors.SigningTermsRequired);
            }

            // D-227 item 3: the recovery rate is typed too. The silent 25% no longer applies at signing.
            if (request.AdvanceRecoveryRate is not { } recovery)
            {
                return ResultExtensions.Problem(ProjectErrors.AdvanceRecoveryRateRequired);
            }

            if (advance < 0m || recovery < Percentage.Zero)
            {
                return ResultExtensions.Problem(ProjectErrors.RateMustNotBeNegative);
            }

            // KAFF-417: terms move to signature. The other three terms are left as they are. The project is a
            // tracked entity, so the audit interceptor records before and after in the same save.
            Result termsSet = project.SetLumpSumTerms(
                Percentage.FromPercent(advance),
                hold,
                recovery,
                project.MaterialAdvanceRate ?? ContractDefaults.MaterialAdvanceRate,
                project.DelayPenaltyEnabled);

            if (termsSet.IsFailure)
            {
                return ResultExtensions.Problem(termsSet.Error);
            }
        }
        else if (request.HoldPercentage is not null || request.ClientAdvance is not null || request.AdvanceRecoveryRate is not null)
        {
            return ResultExtensions.Problem(ProjectErrors.SigningTermsNotForContractType);
        }

        if (project.ContractType == ContractType.Design && !project.IsSigned)
        {
            if (request.AreaSquareMetres is null && project.AreaSquareMetres is null)
            {
                return ResultExtensions.Problem(ProjectErrors.AreaRequired);
            }

            if (request.AreaSquareMetres is { } area)
            {
                Result designSet = project.SetDesignTerms(area, ContractDefaults.DesignRatePerSquareMetre);

                if (designSet.IsFailure)
                {
                    return ResultExtensions.Problem(designSet.Error);
                }
            }
        }

        DateOnly signedOn = request.SignedOn ?? SiteTime.Today(clock);

        Money? boqTotal = null;

        if (ContractValueSource.RequiresBoq(project.ContractType))
        {
            if (boq is null)
            {
                return ResultExtensions.Problem(BoqErrors.Required);
            }

            // D-217 item 23: the refusal names the line that has no quantity.
            BoqLine? unquantified = boq.FirstLineWithoutQuantity(rulings.ContractValueSource);

            if (unquantified is not null)
            {
                return ResultExtensions.Problem(
                    BoqErrors.LineQuantityMissing,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["lineId"] = unquantified.Id,
                        ["position"] = unquantified.Position,
                        ["itemCode"] = unquantified.ItemCode,
                        ["descriptionAr"] = unquantified.DescriptionAr,
                    });
            }

            Result<Money> total = boq.Total(rulings.ContractValueSource);

            if (total.IsFailure)
            {
                return ResultExtensions.Problem(total.Error);
            }

            boqTotal = total.Value;
        }

        Result signed = project.Sign(signedOn, boqTotal);

        if (signed.IsFailure)
        {
            return ResultExtensions.Problem(signed.Error);
        }

        if (boq is not null)
        {
            Result frozen = boq.Freeze(signedOn);

            if (frozen.IsFailure)
            {
                return ResultExtensions.Problem(frozen.Error);
            }
        }

        Adjustment? designCredit = null;

        if (project.LinkType == ProjectLinkType.DesignToExecution && project.LinkedProjectId is { } designId)
        {
            Result<Adjustment> raised = await RaiseDesignCreditAsync(
                database, project, designId, rulings, currentUser.UserId ?? Guid.Empty, clock, cancellationToken);

            if (raised.IsFailure)
            {
                return ResultExtensions.Problem(raised.Error);
            }

            designCredit = raised.Value;
        }

        // SaveNewAsync is GuardedSave plus the adjustment number race, which a concurrent second signature would lose.
        Error? refused = await AdjustmentSupport.SaveNewAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(project.Id, signedOn, project.ContractValue, boq?.FrozenOn, designCredit?.Id));
    }

    /// <summary>
    /// Raises the design credit on <paramref name="execution"/> and adds it, and any account it needed opened, to the
    /// context unsaved.
    /// </summary>
    private static async Task<Result<Adjustment>> RaiseDesignCreditAsync(
        KaffDbContext database,
        Project execution,
        Guid designId,
        RulingSet rulings,
        Guid actor,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Project? design = await database.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == designId, cancellationToken);

        if (design is null)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.DesignCreditNotLinked);
        }

        DateOnly today = SiteTime.Today(clock);
        List<Account> own = await database.Accounts
            .Where(account => account.ProjectId == execution.Id && account.IsActive)
            .ToListAsync(cancellationToken);

        Account? revenue = null;
        foreach (AccountType type in (AccountType[])[AccountType.ContractRevenue, AccountType.ClientReceivable])
        {
            Account? account = own.FirstOrDefault(candidate => candidate.Type == type);
            if (account is null)
            {
                Result<Account> opened = ProjectAccountSetFactory.Open(execution, type, today);
                if (opened.IsFailure)
                {
                    return Result.Failure<Adjustment>(opened.Error);
                }

                account = opened.Value;
                database.Accounts.Add(account);
            }

            if (type == AccountType.ContractRevenue)
            {
                revenue = account;
            }
        }

        bool alreadyRaised = await database.Adjustments.AnyAsync(
            candidate => candidate.ProjectId == execution.Id && candidate.Type == AdjustmentType.DesignCredit, cancellationToken);
        int number = (await database.Adjustments
            .Where(candidate => candidate.ProjectId == execution.Id)
            .Select(candidate => (int?)candidate.Number)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        Result<Adjustment> raised = Adjustment.RaiseDesignCredit(
            execution, number, design, revenue, alreadyRaised, rulings, actor, clock.GetUtcNow());
        if (raised.IsSuccess)
        {
            database.Adjustments.Add(raised.Value);
        }

        return raised;
    }
}
