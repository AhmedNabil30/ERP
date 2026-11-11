using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients.ClientPicker;

/// <summary>
/// Active clients by name for the project-create screen. A read: no audit record, no money. The search term matches on
/// the name only.
/// </summary>
internal static class Handler
{
    private const string LikeEscape = "\\";

    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? search = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        IQueryable<Kaff.Domain.MasterData.Client> query = database.Clients.Where(client => client.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = "%" + search.Trim()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";

            query = query.Where(client => EF.Functions.ILike(client.Name, pattern, LikeEscape));
        }

        List<ClientOption> clients = await query
            .OrderBy(client => client.Name)
            .Select(client => new ClientOption(client.Id, client.Name))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(clients));
    }
}
