namespace Kaff.Api.Features.DayLabour.ListCrew;

/// <summary>
/// One man engaged on the project. <b>No day rate, phone, code or rating</b>: the site engineer who records hours must not
/// see money or contact. The trade is the name of his باب.
/// </summary>
public sealed record CrewMember(Guid WorkerId, string Name, string? Trade);

public sealed record Response(IReadOnlyList<CrewMember> Items);
