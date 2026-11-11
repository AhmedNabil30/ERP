using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.QuickAddClient;

/// <summary>KAFF-419. Same duplicate-phone acknowledgement as <c>CreateClient</c>: a phone that belongs to another client warns, then allows.</summary>
public sealed record Request(string? Name, string? Phone, ClientKind Kind, bool AcknowledgedDuplicatePhone);
