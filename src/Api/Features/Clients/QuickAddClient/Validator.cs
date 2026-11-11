using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.QuickAddClient;

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
