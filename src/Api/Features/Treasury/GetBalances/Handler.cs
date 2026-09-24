using Kaff.Api.Common.Results;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Treasury.GetBalances;

/// <summary>
/// Both reads go straight through <see cref="BalanceQueries"/> (KAFF-304) with no logic added here —
/// CLAUDE.md: "Never store a balance… If you find yourself adding a Balance column, stop." Neither
/// method writes, and neither adds a field that sums or nets two of the five ledgers (AC-306-C,
/// AC-306-D). KAFF-306.
/// </summary>
internal static class Handler
{
    public static IResult HandleLedgers(Guid projectId, KaffDbContext database)
    {
        ArgumentNullException.ThrowIfNull(database);

        LedgerBalances balances = BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId);

        return Microsoft.AspNetCore.Http.Results.Ok(new LedgerBalancesResponse(
            balances.ProjectId,
            balances.ClientAdvance,
            balances.Hold,
            balances.FirmAdvance,
            balances.PettyCashAdvance,
            balances.OwnerCurrentAccount,
            balances.MaterialAdvance));
    }

    public static IResult HandleAccount(Guid accountId, KaffDbContext database)
    {
        ArgumentNullException.ThrowIfNull(database);

        AccountBalance? balance = BalanceQueries.GetAccountBalance(database.AccountBalances, accountId);

        if (balance is null)
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(new AccountBalanceResponse(
            balance.AccountId,
            balance.AccountCode,
            balance.NameAr,
            balance.NameEn,
            balance.Inflow,
            balance.Outflow,
            balance.RawBalance,
            balance.SignedBalance,
            balance.PostingCount,
            balance.LastPostingDate));
    }
}
