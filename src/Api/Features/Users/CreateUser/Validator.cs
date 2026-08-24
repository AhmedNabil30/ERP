using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.CreateUser;

/// <summary>
/// Shape-checks the request before the handler sees it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately thin. Every rule about <i>who may hold what</i> — the HR department binding, the
/// external-role department refusal, the client scope, the Operations sub-department — is enforced
/// by <c>User.Create</c> and is not restated here. A second copy of a business rule in a validator
/// is a copy that will disagree with the entity eventually, and the entity is the one every other
/// caller goes through.
/// </para>
/// <para>
/// What is left is the one rule the domain genuinely cannot see: the password minimum. The domain is
/// handed a hash and never the plaintext (<c>User.SetTemporaryPassword</c>), so the length can only
/// be refused here.
/// </para>
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Not a length rule and not a strength rule — D-049 ruling 3 is "at least 8 characters, no
        // forced complexity", and AC-106-I exists to prove that eight lower-case letters are enough.
        if (!string.IsNullOrWhiteSpace(request.TemporaryPassword)
            && request.TemporaryPassword.Length < User.MinimumPasswordLength)
        {
            return ValueTask.FromResult(Result.Failure(AuthorizationErrors.PasswordTooShort));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
