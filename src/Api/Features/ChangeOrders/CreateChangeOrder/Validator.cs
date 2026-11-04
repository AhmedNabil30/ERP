using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.ChangeOrders.CreateChangeOrder;

/// <summary>Nothing to shape-check: <c>ChangeOrder.Create</c> owns every rule and refuses with a translated key.</summary>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(Result.Success());
    }
}
