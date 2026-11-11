namespace Kaff.Api.Features.Clients.ClientPicker;

/// <summary>Active clients, ordered by name. Id and name only, so the picker shows no code, phone, kind or notes.</summary>
public sealed record Response(IReadOnlyList<ClientOption> Clients);

/// <summary>One client in the picker.</summary>
public sealed record ClientOption(Guid Id, string Name);
