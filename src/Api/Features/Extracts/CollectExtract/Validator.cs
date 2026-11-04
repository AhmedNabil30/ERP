using Kaff.Api.Common.Validation;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>Shape only: a missing method, date or reference, or an over-long reference, is refused with its own translated key.</summary>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Error? error =
            request.Method is null || !Enum.IsDefined(request.Method.Value) ? CollectionErrors.MethodRequired
            : request.Date is null || request.Date.Value == default ? CollectionErrors.DateRequired
            : string.IsNullOrWhiteSpace(request.Reference) ? CollectionErrors.ReferenceRequired
            : request.Reference.Trim().Length > CollectionRecord.MaxReferenceLength ? CollectionErrors.ReferenceTooLong
            : null;

        return ValueTask.FromResult(error is null ? Result.Success() : Result.Failure(error));
    }
}
