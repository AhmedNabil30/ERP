using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Assignments.AssignUserToProject;

/// <summary>
/// Who is being put on the project, and at what seniority. The project is named by the route.
/// </summary>
/// <param name="UserId">An existing, active, non-external user.</param>
/// <param name="Level">
/// Seniority <b>on this assignment</b>, not on the person. Karim, 2026-08-20: "an engineer can be a
/// Supervisor on one project and a Junior on another" (D-044 ruling 5), so the same user id may
/// appear on two projects with two different values here.
/// <para>
/// <c>Junior</c> and <c>Supervisor</c> are legal only for <c>Role.SiteEngineer</c>, and
/// <c>Standard</c> is legal only for everybody else — both halves refused by
/// <c>ProjectAssignment.Create</c> [Verified: 2026-08-24 @ <c>ProjectAssignment.cs</c> -&gt;
/// <c>Create</c>]. Sent as the member name, not a number.
/// </para>
/// </param>
public sealed record Request(Guid UserId, AssignmentLevel Level);
