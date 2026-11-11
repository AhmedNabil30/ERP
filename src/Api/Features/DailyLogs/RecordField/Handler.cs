using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.RecordField;

/// <summary>Records a single-valued field against the caller's log. Nothing is overwritten; the value read is the one with the latest OccurredAt.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await DailyLogAccess.RecordAsync(
            projectId,
            request.EntryId,
            request.OccurredAt,
            database,
            currentUser,
            rulings,
            clock,
            DailyLogAccess.EntryGate.Entry,
            (log, now) => log.RecordField(request.EntryId!.Value, request.Field, request.Text, request.OccurredAt!.Value, now),
            cancellationToken);
    }
}
