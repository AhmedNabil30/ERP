using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.EditEmployee;

/// <summary>The corrected employee. KAFF-207.</summary>
public sealed record Response(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? Specialty,
    string? NationalId,
    string? Department,
    string? JobTitle,
    DateOnly? HiredOn,
    bool IsActive);
