namespace Kaff.Api.Features.Departments.EditDepartment;

/// <summary>The edited department. KAFF-321.</summary>
public sealed record Response(Guid Id, string NameAr, string NameEn, bool IsActive);
