namespace Kaff.Api.Features.Subcontractors.PhoneCheck;

/// <summary>The number the operator typed, as typed.</summary>
/// <param name="Phone">Entered form; <c>PhoneNumber.Create</c> normalises it.</param>
public sealed record Request(string? Phone);
