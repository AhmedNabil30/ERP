using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Departments.CreateDepartment;

/// <summary>
/// Adds one department. KAFF-321, AC-321-B.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard the entity already has is the entity's, not this handler's.</b>
/// <c>Department.Create</c> refuses a blank Arabic or English name — the same shape
/// <c>CreateBab.Handler</c> uses for <c>Bab.Create</c>.
/// </para>
/// <para>
/// <b>No code, no uniqueness index.</b> Unlike <see cref="Bab"/> and <see cref="CatalogueItem"/>, a
/// department carries no business-facing code — decisions.md D-162 (Q85) names only Arabic and English
/// names — so there is nothing here for the database to race on.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Created</c> record in the same transaction. <c>GrantPath</c> stays null —
/// <c>DepartmentManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Department> created = Department.Create(request.NameAr ?? string.Empty, request.NameEn ?? string.Empty);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Department department = created.Value;

        database.Departments.Add(department);
        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/departments/{department.Id}",
            new Response(department.Id, department.NameAr, department.NameEn, department.IsActive));
    }
}
