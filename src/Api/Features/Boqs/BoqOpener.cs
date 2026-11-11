using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs;

/// <summary>The project's BOQ with its lines, opened and added to the context when the first write arrives.</summary>
internal static class BoqOpener
{
    public static async Task<Result<Boq>> GetOrOpenAsync(
        KaffDbContext database, Project project, TimeProvider clock, CancellationToken cancellationToken)
    {
        Boq? boq = await database.Boqs
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == project.Id, cancellationToken);

        if (boq is not null)
        {
            return Result.Success(boq);
        }

        Result<Boq> opened = Boq.Open(project, clock.GetUtcNow());

        if (opened.IsSuccess)
        {
            database.Boqs.Add(opened.Value);
        }

        return opened;
    }
}
