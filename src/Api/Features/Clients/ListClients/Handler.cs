using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients.ListClients;

/// <summary>
/// Finds clients by name, code or phone. KAFF-124.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three ways in, one of which is not a string comparison.</b> A phone is matched on
/// <c>PhoneNormalised</c> after the search term goes through <c>PhoneNumber.Create</c>, so
/// <c>+20 100 123 4567</c>, <c>0020 100 1234567</c> and <c>01001234567</c> all find the same client
/// (<c>AC-124-A</c>). Comparing the typed text against <c>PhoneEntered</c> would find only the format
/// the operator happened to use when registering — which is the failure this deduplication key exists
/// to prevent, and it would be invisible until somebody typed a number a different way.
/// </para>
/// <para>
/// <b>The code is compared upper-cased</b> because <c>Client.Create</c> upper-cases what it stores
/// [Verified: 2026-09-04 @ <c>src/Domain/MasterData/Client.cs</c> -&gt; <c>Create</c>]. That is
/// <c>AC-124-C</c>'s second half — <c>c-10001</c> finds <c>C-10001</c> — and it holds because of the
/// entity's normalisation rather than because this query is clever, which is worth knowing if the
/// entity ever stops doing it.
/// </para>
/// <para>
/// <b>A term that is not a valid phone is not an error.</b> <c>PhoneNumber.Create</c> refuses fewer
/// than seven digits, and a name search legitimately contains none — so a failed parse simply drops
/// the phone branch rather than refusing the request. A search box that 400s on the word "شركة" is
/// not a search box.
/// </para>
/// <para>
/// <b>Archived clients are excluded by default and reachable on request</b> (rule 2). They are not
/// deleted and never can be: spec.md §3 requires a reopened opportunity to attach to the
/// <i>original</i> client, so an archived client that could not be found again would force somebody
/// to create the duplicate this whole feature exists to prevent.
/// </para>
/// <para>
/// <b>No paging, and that is a decision rather than an omission.</b> Slice 1 has no volume, and a
/// page contract invented before anyone has a screen to page is a contract that will be wrong. It is
/// one <c>Take</c> away when a screen needs it, and the response is already a wrapper object rather
/// than a bare array, so adding a total does not break the shape.
/// </para>
/// <para>
/// <b>No audit record and no money.</b> It is a read (KAFF-124's audit note), and there is no balance
/// on the entity to project — rule 5 is about what this must not <i>join</i>, which is asserted
/// against the response type rather than left to good intentions.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? search = null,
        bool includeArchived = false)
    {
        ArgumentNullException.ThrowIfNull(database);

        IQueryable<Client> query = database.Clients;

        if (!includeArchived)
        {
            query = query.Where(client => client.IsActive);
        }

        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (term is not null)
        {
            string namePattern = "%" + Escape(term) + "%";
            string code = term.ToUpperInvariant();

            // A term with too few digits to be a phone is an ordinary name search, not a bad request.
            Result<PhoneNumber> phone = PhoneNumber.Create(term);
            string? normalisedPhone = phone.IsSuccess ? phone.Value.Normalised : null;

            query = query.Where(client =>
                EF.Functions.ILike(client.Name, namePattern, LikeEscape)
                || client.Code == code
                || (normalisedPhone != null && client.PhoneNormalised == normalisedPhone));
        }

        List<ClientSummary> clients = await query
            .OrderBy(client => client.Code)
            .Select(client => new ClientSummary(
                client.Id,
                client.Code,
                client.Name,
                client.PhoneEntered,
                client.Kind,
                client.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(clients));
    }

    private const string LikeEscape = "\\";

    /// <summary>
    /// Neutralises the wildcards <c>ILIKE</c> would otherwise read out of a search term.
    /// </summary>
    /// <remarks>
    /// Without this, searching <c>%</c> returns every client and searching <c>_</c> matches any
    /// single character — a search box quietly acting as a query language. The value is still a
    /// parameter, so this is about the operator getting the results they asked for, not about
    /// injection. The backslash goes first: escaping it after the others would escape the escapes.
    /// </remarks>
    private static string Escape(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
