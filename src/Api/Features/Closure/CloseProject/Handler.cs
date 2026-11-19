using System.Globalization;
using Kaff.Domain.Auditing;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.CloseProject;

/// <summary>
/// UnderWarranty to Closed. Every condition is read from the ledgers at this moment (the advance and the hold from the
/// balances view, the extracts from their own status); no request field can mark one met. A refusal names every unmet
/// condition. The readings are written onto the audit record.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId, KaffDbContext database, TimeProvider clock, IAuditContext audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);

        return ClosureSupport.RunAsync(
            projectId,
            database,
            async (project, token) =>
            {
                LedgerBalances ledgers = BalanceQueries.GetLedgerBalances(database.AccountBalances, project.Id);
                int unissued = await database.Extracts.CountAsync(
                    extract => extract.ProjectId == project.Id
                        && extract.Status != ExtractStatus.Issued
                        && extract.Status != ExtractStatus.Collected
                        && extract.Status != ExtractStatus.Void,
                    token);

                var readings = new ClosureReadings(unissued, ledgers.ClientAdvance, ledgers.Hold);
                Result result = ProjectClosure.Close(project, ClosureSupport.Today(clock), readings);

                if (result.IsSuccess)
                {
                    audit.SetReason(string.Create(
                        CultureInfo.InvariantCulture,
                        $"Closed with unissued extracts {unissued}, client advance {readings.ClientAdvance.Amount:0.0000}, hold {readings.Hold.Amount:0.0000}"));
                    return ClosureOutcome.Of(result);
                }

                return result.Error == ClosureErrors.NotSettled
                    ? new ClosureOutcome(
                        result,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["unmet"] = readings.Unmet().Select(condition => condition.ToString()).ToList(),
                            ["unissuedExtracts"] = unissued,
                            ["clientAdvance"] = readings.ClientAdvance.Amount.ToString("0.0000", CultureInfo.InvariantCulture),
                            ["hold"] = readings.Hold.Amount.ToString("0.0000", CultureInfo.InvariantCulture),
                        })
                    : ClosureOutcome.Of(result);
            },
            cancellationToken);
    }
}
