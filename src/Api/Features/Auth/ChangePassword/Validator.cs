using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Auth.ChangePassword;

/// <summary>
/// Shape-checks the request before the handler sees it.
/// </summary>
/// <remarks>
/// Deliberately thin, the same shape as <c>Features/Users/CreateUser/Validator</c>: the one rule the
/// domain genuinely cannot see is the length, because <c>User.SetOwnPassword</c> is handed a hash and
/// never the plaintext. Whether the current password is correct is not a shape question — it needs the
/// caller's own stored hash, which only the handler has — so it is not checked here.
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < User.MinimumPasswordLength)
        {
            return ValueTask.FromResult(Result.Failure(AuthorizationErrors.PasswordTooShort));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
