namespace Kaff.Api.Features.Clients.PhoneCheck;

/// <summary>
/// The number the operator typed, as typed.
/// </summary>
/// <param name="Phone">
/// Entered form. <c>PhoneNumber.Create</c> normalises it, so the caller never has to know what the
/// deduplication key looks like.
/// </param>
public sealed record Request(string? Phone);
