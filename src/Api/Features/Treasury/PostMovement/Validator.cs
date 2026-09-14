using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.PostMovement;

/// <summary>
/// Shape-checks the request before the handler sees it. KAFF-301.
/// </summary>
/// <remarks>
/// Deliberately thin, for the reason every other validator in this codebase states: every business
/// rule here belongs to <c>Posting.Create</c> — positive amount, distinct accounts, matching currency,
/// ledger netting, the hold, the project tag. A second copy here would eventually disagree with the
/// entity every other caller goes through.
/// <para>
/// What is left is the one rule the domain genuinely cannot see. <c>PostingType</c> and
/// <c>SourceDocumentType</c> are non-nullable enums, so a request that omits either binds to <c>0</c> —
/// not a defined member of either enum — and by the time <c>Posting.Create</c> holds it, it is already
/// whatever the binder produced. Left unchecked, that stores the literal text of an undefined enum
/// value in a column the database guards compare against named literals (<c>001_guards.sql</c>).
/// </para>
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Type))
        {
            return ValueTask.FromResult(Result.Failure(TreasuryErrors.PostingTypeRequired));
        }

        if (!Enum.IsDefined(request.SourceDocumentType))
        {
            return ValueTask.FromResult(Result.Failure(TreasuryErrors.SourceDocumentTypeRequired));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
