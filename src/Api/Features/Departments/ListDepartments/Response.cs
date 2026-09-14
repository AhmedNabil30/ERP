namespace Kaff.Api.Features.Departments.ListDepartments;

/// <summary>One department, as a row in the flat list. KAFF-321.</summary>
/// <param name="Id">The department.</param>
/// <param name="NameAr">As stored — trimmed.</param>
/// <param name="NameEn">As stored — trimmed.</param>
/// <param name="IsActive">Active or archived. AC-321-E: an archived department stays valid on historical records.</param>
public sealed record DepartmentSummary(Guid Id, string NameAr, string NameEn, bool IsActive);

/// <summary>The departments, flat. Empty when none exist — never null.</summary>
/// <param name="Items">Ordered by <c>NameEn</c>.</param>
public sealed record Response(IReadOnlyList<DepartmentSummary> Items);
