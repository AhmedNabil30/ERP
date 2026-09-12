namespace Kaff.Api.Features.DayLabour.PhoneCheck;

/// <summary>The number a site engineer typed. KAFF-209.</summary>
/// <param name="Phone">
/// Entered form. <c>PhoneNumber.Create</c> normalises it, so the caller never has to know what the
/// deduplication key looks like.
/// </param>
public sealed record Request(string? Phone);
