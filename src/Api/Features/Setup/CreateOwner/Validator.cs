using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Setup.CreateOwner;

/// <summary>
/// Shape-checks the request before the handler sees it.
/// </summary>
/// <remarks>
/// <para>
/// Two rules live here rather than in <c>User.CreateBootstrapOwner</c>. The password minimum is the
/// same reason <c>CreateUser.Validator</c> holds it: the domain is handed a hash and never the
/// plaintext, so the length can only be refused here.
/// </para>
/// <para>
/// <b>The reserved-username blocklist (<c>AC-100-G</c>) is scoped to this one screen, not to every
/// account.</b> Nothing stops an ordinary <c>CreateUser</c> call naming somebody <c>admin</c> — the
/// rule this waived criterion states is "a shared login must not survive review" on the door nobody
/// can lock afterwards, not a company-wide username policy, so it does not belong in <c>User.Create</c>
/// where every other caller would inherit it. ⚠️ **UNCITED — waived, Q45**, see
/// <see cref="IdentityErrors.UserNameReserved"/>.
/// </para>
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    private static readonly string[] ReservedUserNames = ["admin", "root", "kaff"];

    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Unlike CreateUser.Validator, an absent password is not a legal state here (rule 7 — the
        // Owner types his own; there is no "no credential yet" reading for the one account that must
        // sign in to prove the setup worked, AC-100-A). Checked before PasswordHasher.Hash ever sees
        // the value: that helper throws on an empty string rather than returning a Result, because a
        // credential rule belongs where it can carry an i18n key, not in a crypto helper.
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ValueTask.FromResult(Result.Failure(IdentityErrors.PasswordHashRequired));
        }

        if (request.Password.Length < User.MinimumPasswordLength)
        {
            return ValueTask.FromResult(Result.Failure(AuthorizationErrors.PasswordTooShort));
        }

        string normalisedUserName = (request.UserName ?? string.Empty).Trim().ToLowerInvariant();

        if (ReservedUserNames.Contains(normalisedUserName, StringComparer.Ordinal))
        {
            return ValueTask.FromResult(Result.Failure(IdentityErrors.UserNameReserved));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
