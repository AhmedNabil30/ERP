namespace Kaff.Api.Features.Departments.CreateDepartment;

/// <summary>
/// What an admin sends to add one department. KAFF-321, AC-321-B.
/// </summary>
/// <param name="NameAr">Required — the Arabic name shown in the UI.</param>
/// <param name="NameEn">Required.</param>
public sealed record Request(string? NameAr, string? NameEn);
