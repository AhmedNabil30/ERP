using Kaff.Api.Common.Validation;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.CreateProject;

/// <summary>
/// Shape-checks the request before the handler sees it. KAFF-400.
/// </summary>
/// <remarks>
/// Deliberately thin, the same reasoning as <c>CreateClient.Validator</c>: the name, the contract
/// value and the state transition are all rules the entity itself enforces
/// (<c>Project.Create</c>, <c>Project.SetLumpSumTerms</c>, <c>Project.TransitionTo</c>). What is left
/// is what the domain genuinely cannot see before it is asked — an undefined <c>ContractType</c> binds
/// to <c>0</c> the same way <c>ClientKind</c> does, and a negative <c>Percentage</c> throws rather than
/// returning a <c>Result</c>, so a negative rate must be refused here before it ever reaches
/// <c>Percentage.FromPercent</c>.
/// </remarks>
public sealed class Validator : IRequestValidator<Request>
{
    public ValueTask<Result> ValidateAsync(Request request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.ContractType))
        {
            return ValueTask.FromResult(Result.Failure(ProjectErrors.TermsDoNotMatchContractType));
        }

        if (request.ClientId == Guid.Empty)
        {
            return ValueTask.FromResult(Result.Failure(ProjectErrors.ClientRequired));
        }

        // D-151: HoldPercentage is already Percentage — a negative value is refused by the type's own
        // constructor during deserialisation (ApiErrors.MalformedBody), so there is nothing left to
        // check here. ClientAdvance stays a bare decimal (out of KAFF-400's scope, see Request's
        // remarks) and still needs the manual guard.
        if (request.ClientAdvance is < 0m)
        {
            return ValueTask.FromResult(Result.Failure(ProjectErrors.RateMustNotBeNegative));
        }

        if (request.ContractType == ContractType.LumpSum
            && request.ContractValue is null or <= 0m)
        {
            return ValueTask.FromResult(Result.Failure(ProjectErrors.ContractValueRequired));
        }

        if (request.Status is not (null or ProjectStatus.Setup or ProjectStatus.Active))
        {
            return ValueTask.FromResult(Result.Failure(ProjectErrors.InitialStatusNotSupported));
        }

        return ValueTask.FromResult(Result.Success());
    }
}
