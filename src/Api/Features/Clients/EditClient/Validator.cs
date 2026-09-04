using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.EditClient;

/// <summary>
/// Shape-checks the request before the handler sees it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately thin, and identical in reasoning to <c>CreateClient.Validator</c>: every business
/// rule here belongs to the entity. The name is <c>Client.Rename</c>'s, the phone is
/// <c>PhoneNumber.Create</c>'s, and "an individual does not withhold" is
/// <c>Client.SetClassification</c>'s. KAFF-121 rule 6 says so in as many words — the guard lives with
/// the setter, not in a validator — because a second copy in a validator is a copy the domain's other
/// callers bypass and that will disagree with the entity eventually.
/// </para>
/// <para>
/// What is left is the one rule the domain genuinely cannot see. <c>ClientKind</c> is a non-nullable
/// enum, so an absent <c>kind</c> binds to <c>0</c> — not a member — and by the time
/// <c>SetClassification</c> holds it, it is already whatever the binder produced. Unchecked, the
/// enum-as-string convention would write the literal text <c>"0"</c> over a perfectly good kind.
/// <b>On the edit path that is worse than on the create path</b>, because it destroys a value that
/// was previously correct.
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
