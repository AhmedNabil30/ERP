using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.ReactivateUser;

/// <summary>
/// Shape-checks the request before the handler sees it.
/// </summary>
/// <remarks>
/// The same one rule <c>CreateUser.Validator</c> exists for: the domain is handed a hash, never the
/// plaintext, so the minimum length can only be refused here. Every rule about the account itself —
/// already active, does not exist — is <c>User.Reactivate</c>'s and the handler's, not restated.
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.TemporaryPassword)
            && request.TemporaryPassword.Length < User.MinimumPasswordLength)
        {
            return ValueTask.FromResult(Result.Failure(AuthorizationErrors.PasswordTooShort));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
