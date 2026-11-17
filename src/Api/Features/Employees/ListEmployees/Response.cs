using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.ListEmployees;

/// <summary>One employee, as a row in the register. KAFF-207.</summary>
/// <remarks>
/// AC-207-E: no salary, day rate, wage or other money-typed member appears here, and D-232 item 4 keeps a rate off the
/// worker. A salaried row fills <c>JobTitle</c>, <c>Department</c> and <c>HiredOn</c>; a day-labour row fills the باب names
/// and the two company-wide figures <c>TimesEngaged</c> and <c>AverageRating</c> (not money). The rest are null.
/// </remarks>
public sealed record EmployeeSummary(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? BabNameAr,
    string? BabNameEn,
    string? Specialty,
    string? JobTitle,
    string? Department,
    DateOnly? HiredOn,
    int? TimesEngaged,
    decimal? AverageRating,
    bool IsActive);

/// <summary>The employees. Empty when none match — never null.</summary>
/// <param name="Items">Ordered by <c>Code</c>.</param>
public sealed record Response(IReadOnlyList<EmployeeSummary> Items);
