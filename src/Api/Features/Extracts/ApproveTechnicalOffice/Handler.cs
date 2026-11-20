using System.Security.Claims;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ApproveTechnicalOffice;

/// <summary>
/// Passes the quantity gate, in full or split. Writes no posting: the figures do not exist yet. A split holds the
/// remainder against an NCR, opened here in the same save (AC-500-E). From handover on a split is refused and no
/// NCR is opened (D-258 item 1).
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = services.Clock.GetUtcNow();
                Percentage portion = request.ApprovedPortion ?? Percentage.FromFraction(1m);

                Ncr? opened = null;
                Guid? heldFor = null;
                if (portion < Percentage.FromFraction(1m) && Extract.SplitAllowedIn(project.Status))
                {
                    // D-260 item 1: the split says what does not conform. A reused open NCR takes the new description.
                    Ncr? reused = await services.Database.Ncrs
                        .FirstOrDefaultAsync(ncr => ncr.ExtractId == extract.Id && ncr.Status == NcrStatus.Open, token);
                    if (reused is null)
                    {
                        Result<Ncr> created = Ncr.Open(extract, request.NcrDescription, actor, now);
                        if (created.IsFailure)
                        {
                            return Result.Failure<IReadOnlyList<Posting>>(created.Error);
                        }

                        opened = created.Value;
                    }
                    else if (reused.Describe(request.NcrDescription) is { IsFailure: true } described)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(described.Error);
                    }

                    heldFor = (opened ?? reused)!.Id;
                }

                Result approved = extract.ApproveTechnicalOffice(project.Status, actor, portion, heldFor, services.Rulings, now);
                if (approved.IsSuccess && opened is not null)
                {
                    services.Database.Ncrs.Add(opened);
                }

                return ExtractSupport.NoPostingsOr(approved);
            },
            cancellationToken);
    }
}
