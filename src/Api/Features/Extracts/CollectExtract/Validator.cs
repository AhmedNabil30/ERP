using Kaff.Api.Common.Validation;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>
/// Shape only: a missing method, date, reference or amount, or an over-long reference, is refused with its own translated
/// key. Whether the amounts are sane against the extract is <c>Extract.CheckPayment</c>'s rule.
/// </summary>
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
            : request.CashReceived is null ? CollectionErrors.AmountRequired
            : null;

        return ValueTask.FromResult(error is null ? Result.Success() : Result.Failure(error));
    }
}
