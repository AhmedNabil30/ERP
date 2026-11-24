using Kaff.Domain.Common;
using Kaff.Domain.Projects;

namespace Kaff.Api.Tests.Infrastructure;

internal static class ProjectTestExtensions
{
    /// <summary>
    /// Marks a freshly created project as signed, without the contract terms. The portal shows a project only from signing onward,
    /// so a test about the client's access needs a signed one.
    /// </summary>
    public static Project AsSigned(this Project project)
    {
        typeof(Project).GetProperty(nameof(Project.SignedOn))!.SetValue(project, new DateOnly(2026, 9, 24));
        typeof(Project).GetProperty(nameof(Project.ContractValue))!.SetValue(project, new Money(1_000m));
        return project;
    }
}
