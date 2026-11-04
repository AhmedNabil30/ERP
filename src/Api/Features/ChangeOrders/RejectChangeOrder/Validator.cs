using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.ChangeOrders.RejectChangeOrder;

/// <summary>Nothing to shape-check: <c>ChangeOrder.Reject</c> owns the rule, with <c>change_order.reason_required</c>.</summary>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Result.Success());
    }
}
