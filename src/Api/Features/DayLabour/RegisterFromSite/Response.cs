namespace Kaff.Api.Features.DayLabour.RegisterFromSite;

/// <summary>
/// The day labourer just registered. KAFF-209.
/// </summary>
/// <remarks>
/// <b>No day rate, wage, salary or money member — rule 8, AC-209-G.</b> §10's "average day rate" is
/// derived from engagements (<c>KAFF-210</c>), never stored, and never typed onto a worker's card at
/// registration.
/// </remarks>
public sealed record Response(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    Guid? BabId,
    string? Specialty,
    bool IsActive);
