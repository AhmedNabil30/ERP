using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.CreateClient;

/// <summary>
/// Shape-checks the request before the handler sees it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately thin, and for the reason <c>CreateUser</c>'s validator states: every business rule
/// here belongs to the entity. The name is <c>Client.Create</c>'s, the phone is
/// <c>PhoneNumber.Create</c>'s, and "an individual does not withhold" is
/// <c>Client.SetTaxRegistration</c>'s. A second copy in a validator is a copy that will disagree with
/// the entity eventually, and the entity is what every other caller goes through.
/// </para>
/// <para>
/// What is left is the one rule the domain genuinely cannot see. <c>ClientKind</c> is a non-nullable
/// enum, so an absent <c>kind</c> binds to <c>0</c> — not a member of the enum — and by the time
/// <c>Client.Create</c> holds it, it is already whatever the binder produced. The enum-as-string
/// convention would then store the literal text <c>"0"</c> in a column whose whole purpose is to be
/// readable long after this code is gone. KAFF-119 rule 8: a client is either Individual or
/// Corporate.
/// </para>
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(
            Enum.IsDefined(request.Kind)
                ? Result.Success()
                : Result.Failure(MasterDataErrors.ClientKindRequired));
    }
}
