using Kaff.Domain.Contracts;

namespace Kaff.Api.Features.Projects.ConvertToExecution;

/// <summary>What opens the execution project. KAFF-415.</summary>
/// <param name="Name">Required. The execution project's own name.</param>
/// <param name="ContractType">LumpSum or CostPlus (Q-207-9). Design, or none, is refused.</param>
public sealed record Request(string? Name, ContractType? ContractType);
