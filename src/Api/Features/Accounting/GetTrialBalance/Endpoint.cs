using System.Globalization;
using System.Text;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.GetTrialBalance;

/// <summary>
/// The trial balance for a date range, as JSON and as a CSV file Excel opens with the Arabic names readable. KAFF-704.
/// </summary>
/// <remarks>
/// Every figure is summed from postings dated inside the range (<c>from</c> and <c>to</c> both inclusive, either optional). Nothing is
/// read from a stored balance. The five ledgers are separate rows and never net.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/trial-balance";
    public const string ExportRoute = "/api/treasury/trial-balance/export";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, async (KaffDbContext database, DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
            {
                Result<TrialBalanceResult> built = await Handler.BuildAsync(database, from, to, cancellationToken);
                return built.IsFailure
                    ? ResultExtensions.Problem(built.Error)
                    : Microsoft.AspNetCore.Http.Results.Ok(Response.Of(built.Value, from, to));
            })
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("GetTrialBalance")
            .WithTags("Accounting");

        app.MapGet(ExportRoute, async (KaffDbContext database, DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
            {
                Result<TrialBalanceResult> built = await Handler.BuildAsync(database, from, to, cancellationToken);
                return built.IsFailure
                    ? ResultExtensions.Problem(built.Error)
                    : Microsoft.AspNetCore.Http.Results.File(Handler.ToCsv(built.Value), "text/csv; charset=utf-8", "trial-balance.csv");
            })
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ExportTrialBalance")
            .WithTags("Accounting");
    }
}

public sealed record Line(
    Guid AccountId,
    string Code,
    string NameAr,
    string NameEn,
    AccountType Type,
    AccountClass Class,
    NormalBalance NormalBalance,
    LedgerKind? LedgerKind,
    Guid? ProjectId,
    Money Debit,
    Money Credit);

public sealed record Response(DateOnly? From, DateOnly? To, IReadOnlyList<Line> Lines, Money TotalDebit, Money TotalCredit, bool IsBalanced)
{
    public static Response Of(TrialBalanceResult result, DateOnly? from, DateOnly? to) => new(
        from,
        to,
        [
            .. result.Rows.Select(row => new Line(
                row.Account.AccountId, row.Account.Code, row.Account.NameAr, row.Account.NameEn, row.Account.Type, row.Account.Class,
                row.Account.NormalBalance, row.Account.LedgerKind, row.Account.ProjectId, row.Debit, row.Credit)),
        ],
        result.TotalDebit,
        result.TotalCredit,
        result.IsBalanced);
}

internal static class Handler
{
    public static async Task<Result<TrialBalanceResult>> BuildAsync(
        KaffDbContext database, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (from is not null && to is not null && from > to)
        {
            return Result.Failure<TrialBalanceResult>(AccountingErrors.ReportRangeInvalid);
        }

        IQueryable<Posting> postings = database.Postings;

        if (from is not null)
        {
            postings = postings.Where(posting => posting.PostingDate >= from);
        }

        if (to is not null)
        {
            postings = postings.Where(posting => posting.PostingDate <= to);
        }

        var movements = await postings
            .Select(posting => new { posting.FromAccountId, posting.ToAccountId, posting.Amount })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Money> debits = Sum(movements.Select(movement => (movement.ToAccountId, movement.Amount)));
        Dictionary<Guid, Money> credits = Sum(movements.Select(movement => (movement.FromAccountId, movement.Amount)));

        List<Guid> touched = [.. debits.Keys.Union(credits.Keys)];

        List<TrialBalanceAccount> accounts = await database.Accounts
            .Where(account => touched.Contains(account.Id))
            .Select(account => new TrialBalanceAccount(
                account.Id, account.Code, account.NameAr, account.NameEn, account.Type, account.Class, account.NormalBalance,
                account.ProjectId, account.LedgerKind))
            .ToListAsync(cancellationToken);

        return Result.Success(TrialBalance.Build(accounts, debits, credits));
    }

    /// <summary>UTF-8 with a byte-order mark, so Excel reads the Arabic names. Figures show two decimals; the stored four are untouched.</summary>
    public static byte[] ToCsv(TrialBalanceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder csv = new();
        csv.Append("Code,NameAr,NameEn,Type,Class,Project,Debit,Credit\r\n");

        foreach (TrialBalanceRow row in result.Rows)
        {
            csv.Append(Cell(row.Account.Code)).Append(',')
                .Append(Cell(row.Account.NameAr)).Append(',')
                .Append(Cell(row.Account.NameEn)).Append(',')
                .Append(row.Account.Type).Append(',')
                .Append(row.Account.Class).Append(',')
                .Append(row.Account.ProjectId).Append(',')
                .Append(Figure(row.Debit)).Append(',')
                .Append(Figure(row.Credit)).Append("\r\n");
        }

        csv.Append("Total,,,,,,").Append(Figure(result.TotalDebit)).Append(',').Append(Figure(result.TotalCredit)).Append("\r\n");

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(csv.ToString())];
    }

    private static string Figure(Money money) => money.Amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Cell(string text)
    {
        // A cell that starts with a formula character is read by Excel as a formula; a quote guards it.
        string safe = text.Length > 0 && text[0] is '=' or '+' or '-' or '@' ? "'" + text : text;
        return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static Dictionary<Guid, Money> Sum(IEnumerable<(Guid AccountId, Money Amount)> amounts) =>
        amounts.GroupBy(amount => amount.AccountId)
            .ToDictionary(group => group.Key, group => Money.Sum(group.Select(amount => amount.Amount)));
}
