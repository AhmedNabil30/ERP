using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.CreateEmployee;

/// <summary>The created employee. KAFF-207.</summary>
/// <param name="Code">Generated — decisions.md D-130 §6. Never supplied, never edited.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? Specialty,
    string? NationalId,
    string? JobTitle,
    DateOnly? HiredOn,
    bool IsActive);
