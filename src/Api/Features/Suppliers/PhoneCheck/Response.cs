using Kaff.Api.Common;

namespace Kaff.Api.Features.Suppliers.PhoneCheck;

/// <summary>Who already holds this number. Empty when nobody does. decisions.md D-141.</summary>
/// <param name="Matches">Every match, archived included, ordered by code.</param>
public sealed record Response(IReadOnlyList<PhoneMatch> Matches);
