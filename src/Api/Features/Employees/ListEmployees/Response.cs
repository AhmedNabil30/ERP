using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.ListEmployees;

/// <summary>One employee, as a row in the register. KAFF-207.</summary>
/// <remarks>AC-207-E: no salary, day rate, wage or other money-typed member appears here.</remarks>
public sealed record EmployeeSummary(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? Specialty,
    bool IsActive);

/// <summary>The employees. Empty when none match — never null.</summary>
/// <param name="Items">Ordered by <c>Code</c>.</param>
public sealed record Response(IReadOnlyList<EmployeeSummary> Items);
