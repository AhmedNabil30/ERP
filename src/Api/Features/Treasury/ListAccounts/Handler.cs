using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Treasury.ListAccounts;

/// <summary>One account an accounting form can pick. <c>ProjectId</c> is null for a company account.</summary>
public sealed record AccountRow(Guid Id, string Code, string NameAr, string NameEn, AccountType Type, Guid? ProjectId);

internal static class Handler
{
    /// <summary>The only types this list serves: what the accounting forms need, nothing else.</summary>
    private static readonly AccountType[] Served =
    [
        AccountType.AccruedExpense,
        AccountType.CompanyExpense,
        AccountType.Prepayment,
        AccountType.AccumulatedDepreciation,
        AccountType.DepreciationExpense,
        AccountType.ProjectCost,
    ];

    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken,
        AccountType? type = null,
        Guid? projectId = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(authorization);

        // A project's accounts need the project-scoped read as well: role and assignment, never the company-wide grant alone.
        if (projectId is not null
            && !(await authorization.AuthorizeAsync(
                user, resource: null, PermissionRequirement.ToPolicyName(Permission.TreasuryReadProject, ProjectScope.FromQuery()))).Succeeded)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        IQueryable<Account> query = database.Accounts.AsNoTracking()
            .Where(account => account.IsActive && Served.Contains(account.Type) && account.ProjectId == projectId);
        if (type is { } wanted)
        {
            query = query.Where(account => account.Type == wanted);
        }

        List<AccountRow> rows = await query
            .OrderBy(account => account.Code)
            .Select(account => new AccountRow(account.Id, account.Code, account.NameAr, account.NameEn, account.Type, account.ProjectId))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
