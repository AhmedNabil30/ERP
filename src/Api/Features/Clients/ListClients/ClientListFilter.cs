namespace Kaff.Api.Features.Clients.ListClients;

/// <summary>
/// Which clients the list is asked for. KAFF-124 rule 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three states, because the screen has three chips</b> — `[ All ] [ Active ] [ Archived ]`
/// (`ux/slice-1-flows.md` -&gt; `S-011 · Client list`). This started as a boolean
/// <c>includeArchived</c>, which satisfies KAFF-124 rule 2 as written — *"the default list excludes
/// archived clients; they remain findable through an explicit filter"* — and satisfies
/// <c>AC-124-E</c>, which only ever tests two states. It cannot express **archived alone**, which is
/// the third chip.
/// </para>
/// <para>
/// <b>Found by reading the UX spec against a shipped contract, before the screen was written</b>
/// (decisions.md D-111 §3). Changed rather than worked around, because nothing consumes the endpoint
/// yet: a boolean here would have become a screen filtering the third state client-side, which is a
/// list that lies as soon as there is more than one page of clients.
/// </para>
/// </remarks>
public enum ClientListFilter
{
    /// <summary>The default. Archived clients are excluded — KAFF-124 rule 2.</summary>
    Active = 1,

    /// <summary>Archived clients only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter.</summary>
internal static class ClientListFilterParsing
{
    /// <summary>
    /// Reads the <c>status</c> query parameter, defaulting to <see cref="ClientListFilter.Active"/>.
    /// </summary>
    /// <remarks>
    /// <b>An unknown value is refused rather than defaulted.</b> Silently treating
    /// <c>?status=archvied</c> as "active" answers a question nobody asked and looks exactly like an
    /// empty archive — the operator concludes there is nothing there. Absent is a default; wrong is a
    /// mistake, and the two must not produce the same list.
    /// </remarks>
    public static bool TryParse(string? status, out ClientListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = ClientListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter)
               && Enum.IsDefined(filter);
    }
}
