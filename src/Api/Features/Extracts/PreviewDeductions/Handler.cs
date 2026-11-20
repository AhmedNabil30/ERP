using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.PreviewDeductions;

/// <summary>
/// What the deductions would come to now: the gross (the certified value: work plus change orders), the printed column the
/// document will print (each addition and deduction, from the same <c>ExtractView.ColumnFor</c>), the hold this period and
/// to date, the tax the client is expected to withhold, and the net payable. Every figure is the calculator's; the browser
/// adds nothing (D-135). The ledger balances are read as they stand, so the applied figures can differ only if another
/// document posts to the same ledgers first.
/// </summary>
public sealed record DeductionsPreview(
    Money Gross,
    IReadOnlyList<PrintedRow> Column,
    Money HoldThisPeriod,
    Money HoldToDate,
    Money TaxAtSource,
    Money NetPayable);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        IContractTypeDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Extract? extract = await ExtractSupport.WithChildren(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (extract is null || project is null)
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        Result<IBillingCalculator> calculator = dispatcher.BillingCalculatorFor(project.ContractType);
        if (calculator.IsFailure)
        {
            return ResultExtensions.Problem(calculator.Error);
        }

        Result<BillingResult> billed = await extract.PreviewDeductionsAsync(
            calculator.Value, project, ExtractSupport.Balances(database, projectId), cancellationToken);
        if (billed.IsFailure)
        {
            return ResultExtensions.Problem(billed.Error);
        }

        Money Of(BillingComponentKind kind) =>
            Money.Sum(billed.Value.Components.Where(component => component.Kind == kind).Select(component => component.Amount));

        return Microsoft.AspNetCore.Http.Results.Ok(new DeductionsPreview(
            billed.Value.CertifiedValue,
            ExtractView.ColumnFor(Of),
            Of(BillingComponentKind.Hold),
            Of(BillingComponentKind.HoldToDate),
            Of(BillingComponentKind.TaxAtSource),
            billed.Value.NetPayable));
    }
}
