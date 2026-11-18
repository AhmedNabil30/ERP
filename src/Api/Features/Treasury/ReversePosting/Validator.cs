using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.ReversePosting;

/// <summary>
/// Shape-checks the request before the handler sees it. KAFF-303.
/// </summary>
/// <remarks>
/// The date is the one thing the caller says (<see cref="Request"/>), so it is the one thing checked here: required and
/// plausible (<c>TypedPostingDate</c>, Verifier F-7). Every rule about whether the reversal itself is legal belongs to
/// <c>Posting.Reverse</c>, not a copy here.
/// </remarks>
public sealed class Validator(TimeProvider clock) : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(TypedPostingDate.Check(request.PostingDate, SiteTime.Today(clock)));
    }
}
