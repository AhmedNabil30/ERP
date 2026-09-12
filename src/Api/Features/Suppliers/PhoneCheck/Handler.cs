using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Suppliers.PhoneCheck;

/// <summary>Answers "is this number already on file, and whose is it?" and changes nothing. decisions.md D-141.</summary>
/// <remarks>
/// Side-effect free, same reasoning as <c>Subcontractors.PhoneCheck.Handler</c>. This is not the
/// enforcement: <c>CreateSupplier</c> and <c>EditSupplier</c> re-run the same match server-side.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        List<PhoneMatch> matches =
            await PhoneMatches.SuppliersAsync(database, phone.Value.Normalised, cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(matches));
    }
}
