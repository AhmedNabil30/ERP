using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Suppliers.EditSupplier;

/// <summary>Corrects one supplier's file, including its tax registration number, and leaves the before-state in the trail. KAFF-212.</summary>
/// <remarks>
/// <para>
/// <b>A supplier is never its own duplicate</b> — the match excludes the row being edited (decisions.md
/// D-107 §2), same as <c>EditClient</c>, <c>EditEmployee</c> and <c>EditSubcontractor</c>.
/// </para>
/// <para>
/// <b>The address and the tax registration number are the fields somebody will later ask who
/// changed</b> (rule 10, AC-212-J) — <c>AuditSaveChangesInterceptor</c> writes both fields' before and
/// after in the same <c>Modified</c> record, because both are edited by the same role through the same
/// endpoint — unlike the subcontractor's tax registration number, split onto its own endpoint by
/// D-147 because there the two fields have two different owning roles.
/// </para>
/// <para>
/// <b>No audit record is hand-written for the edit itself.</b> The acknowledgement is declared through
/// <c>IAuditContext.Record</c>, same as <c>CreateSupplier</c>. <c>GrantPath</c> stays null —
/// <c>SupplierManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid supplierId,
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        Supplier? supplier = await database.Suppliers
            .FirstOrDefaultAsync(candidate => candidate.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.SupplierNotFound);
        }

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        List<PhoneMatch> matches = await PhoneMatches.SuppliersAsync(
            database, phone.Value.Normalised, cancellationToken, excluding: supplierId);

        if (matches.Count > 0 && !request.AcknowledgedDuplicatePhone)
        {
            return ResultExtensions.Problem(MasterDataErrors.DuplicatePhoneNotAcknowledged);
        }

        Result edited = supplier.Edit(request.Name ?? string.Empty, phone.Value, request.Address, request.TaxRegistrationNumber);

        if (edited.IsFailure)
        {
            return ResultExtensions.Problem(edited.Error);
        }

        foreach (PhoneMatch match in matches)
        {
            audit.Record<Supplier>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(
                supplier.Id,
                supplier.Code,
                supplier.Name,
                supplier.PhoneEntered,
                supplier.Address,
                supplier.TaxRegistrationNumber,
                supplier.IsActive));
    }
}
