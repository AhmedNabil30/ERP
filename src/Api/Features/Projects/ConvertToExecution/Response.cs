using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.ConvertToExecution;

/// <summary>The execution project just opened from a design project. KAFF-415.</summary>
/// <param name="Id">The new execution project.</param>
/// <param name="Code">Generated.</param>
/// <param name="Name">As stored, trimmed.</param>
/// <param name="ClientId">The design project's client.</param>
/// <param name="ContractType">LumpSum or CostPlus.</param>
/// <param name="Status">Always Setup.</param>
/// <param name="DesignProjectId">The design project it links back to.</param>
/// <param name="CreatedAt">When the project row was written.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    Guid ClientId,
    ContractType ContractType,
    ProjectStatus Status,
    Guid DesignProjectId,
    DateTimeOffset CreatedAt);
