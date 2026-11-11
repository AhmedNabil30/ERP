using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.BoqBases.ListBoqBases;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<BoqBaseSummary> bases = await database.BoqBases
            .AsNoTracking()
            .Where(boqBase => boqBase.ArchivedAt == null)
            .OrderBy(boqBase => boqBase.Name)
            .Select(boqBase => new BoqBaseSummary(boqBase.Id, boqBase.Name, boqBase.Lines.Count, boqBase.CreatedAt))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(bases));
    }
}

/// <summary>A prepared base in the picker: its name and how many lines it carries.</summary>
public sealed record BoqBaseSummary(Guid Id, string Name, int LineCount, DateTimeOffset CreatedAt);

public sealed record Response(IReadOnlyList<BoqBaseSummary> Bases);
