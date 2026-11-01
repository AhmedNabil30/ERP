using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.RejectExtract;

/// <summary>
/// Nothing to shape-check here. A blank or over-long reason is refused by <c>Extract.Reject</c>, the one place that
/// owns the rule, with <c>extract.reason_required</c> or <c>extract.reason_too_long</c>.
/// </summary>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Result.Success());
    }
}
