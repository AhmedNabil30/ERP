namespace Kaff.Api.Features.Departments.EditDepartment;

/// <summary>
/// What an admin sends to correct a department's names. KAFF-321, AC-321-C.
/// </summary>
/// <param name="NameAr">Required.</param>
/// <param name="NameEn">Required.</param>
public sealed record Request(string? NameAr, string? NameEn);
