using System.Security.Claims;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.ApplyDeductions;

/// <summary>
/// Accounts applies the deductions. The calculator is the project's own contract type's, resolved through the
/// dispatcher; the ledger balances it reads are summed from the postings inside the row lock; when the ruled
/// accrual moment is this stage the postings commit with the status change in one save.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IContractTypeDispatcher dispatcher,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: true,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                Result<IBillingCalculator> calculator = dispatcher.BillingCalculatorFor(project.ContractType);
                if (calculator.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(calculator.Error);
                }

                ExtractLedgerBalances balances = ExtractSupport.Balances(services.Database, projectId);
                Result<PostingSet?> applied = await extract.ApplyDeductionsAsync(
                    services.CurrentUser.UserId ?? Guid.Empty,
                    calculator.Value,
                    balances,
                    context!,
                    services.Clock.GetUtcNow(),
                    token);
                return ExtractSupport.Of(applied);
            },
            cancellationToken);
    }
}
