using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Treasury.ReversePosting;

/// <summary>
/// Shape-checks the request before the handler sees it. KAFF-303.
/// </summary>
/// <remarks>
/// Nothing to check. <c>PostingDate</c> is a non-nullable <c>DateOnly</c> — there is no undefined
/// value it can bind to the way <c>PostMovement</c>'s enums can — and every business rule about
/// whether the reversal itself is legal belongs to <c>Posting.Reverse</c>, not a copy here.
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Result.Success());
    }
}
