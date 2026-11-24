using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Portal.ChangePassword;

public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(
            string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < User.MinimumPasswordLength
                ? Result.Failure(AuthorizationErrors.PasswordTooShort)
                : Result.Success());
    }
}
