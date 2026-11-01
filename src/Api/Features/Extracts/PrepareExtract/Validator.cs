using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;

namespace Kaff.Api.Features.Extracts.PrepareExtract;

/// <summary>
/// Shape only: a period that is absent binds to the default date, which no real period starts on. Whether the
/// period runs forwards and whether the money is sane are <c>Extract.Prepare</c>'s rules.
/// </summary>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(
            request.PeriodStart == default || request.PeriodEnd == default
                ? Result.Failure(ExtractErrors.PeriodInvalid)
                : Result.Success());
    }
}
