using System.Globalization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Sales;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Opportunities.CreateOpportunity;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        // The client is found or created through the client endpoints first (a repeated phone warns there).
        // An archived client is not refused: spec.md §3 attaches reopened work to the same client.
        bool clientExists = await database.Clients.AnyAsync(client => client.Id == request.ClientId, cancellationToken);

        if (!clientExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.ClientNotFound);
        }

        Result<Opportunity> created = Opportunity.Create(
            await NextCodeAsync(database, cancellationToken),
            request.ClientId,
            request.Title ?? string.Empty,
            clock.GetUtcNow());

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Opportunity opportunity = created.Value;

        database.Opportunities.Add(opportunity);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/opportunities/{opportunity.Id}",
            new Response(opportunity.Id, opportunity.Code, opportunity.ClientId, opportunity.Title, opportunity.Stage, opportunity.Status));
    }

    private static async Task<string> NextCodeAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // The sequence name is a compile-time constant of this assembly, never user input.
        long number = await database.Database
            .SqlQueryRaw<long>($"SELECT nextval('{KaffDbContext.OpportunityCodeSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken);
#pragma warning restore EF1002

        return string.Create(CultureInfo.InvariantCulture, $"O-{number}");
    }
}
