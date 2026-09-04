using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Clients.PhoneCheck;

/// <summary>
/// Answers "is this number already on file, and whose is it?" and changes nothing. KAFF-119.
/// </summary>
/// <remarks>
/// <para>
/// <b>Side-effect free, and that is why it exists as its own endpoint.</b> <c>ux/slice-1-flows.md</c>
/// S-013: <i>"the check still fires on blur of the phone field, which is why phone is still the first
/// field."</i> A check that fires on blur cannot be <c>POST /api/clients</c> — the operator has not
/// finished typing the rest of the form, and a registration attempt per keystroke-out would create
/// clients nobody asked for.
/// </para>
/// <para>
/// <b>It writes no audit record</b>, because nothing changed: no entity is touched and no event is
/// declared. What the operator did with the answer is recorded at the save, by
/// <c>AuditEventKind.DuplicatePhoneAcknowledged</c>, where it is a decision rather than a keystroke.
/// </para>
/// <para>
/// <b>This is not the enforcement.</b> A caller can skip it entirely; <c>CreateClient</c> re-runs the
/// same match server-side and refuses an unacknowledged duplicate with a 409. That is the shape
/// <c>CreateUser</c> already uses for its friendly username pre-check
/// [Verified: 2026-09-04 @ <c>src/Api/Features/Users/CreateUser/Handler.cs</c> -&gt;
/// <c>HandleAsync</c>].
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

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        List<PhoneMatch> matches =
            await PhoneMatches.FindAsync(database, phone.Value.Normalised, cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(matches));
    }
}
