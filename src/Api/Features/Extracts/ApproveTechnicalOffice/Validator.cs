using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.ApproveTechnicalOffice;

/// <summary>
/// Nothing to shape-check: the portion's range and the NCR requirement are the aggregate's rules
/// (<c>Extract.ApproveTechnicalOffice</c>), and a second copy here would eventually disagree with it.
/// </summary>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Result.Success());
    }
}
