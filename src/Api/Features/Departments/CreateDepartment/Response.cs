namespace Kaff.Api.Features.Departments.CreateDepartment;

/// <summary>The created department. KAFF-321.</summary>
public sealed record Response(Guid Id, string NameAr, string NameEn, bool IsActive);
