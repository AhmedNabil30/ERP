namespace Kaff.Domain.Rulings;

/// <summary>D-217 item 36: Finance is added to every new project at creation. Registered; Karim has not confirmed it.</summary>
public sealed record FinanceStaffing : IRuling
{
    public static readonly FinanceStaffing Default = new();

    public RulingNote Note => new(
        "FinanceStaffing",
        "Karim to confirm, D-217 item 36: which Finance users are added to a new project, and may Finance be removed from one?",
        "Who is assigned to a project the moment it is created, beyond the people who create it.",
        "Every active user with the Finance role gets a Standard assignment on each new project, in the same save that opens "
        + "the project. The assignment is an ordinary row: HR or the Owner may revoke it. A Finance user registered later is not "
        + "added to projects that already exist. ASSUMED.",
        "Change the user filter in CreateProject.Handler, for example to one named Finance user, or drop the step and let HR "
        + "assign Finance by hand as before.",
        RulingStatus.Assumed);
}

/// <summary>D-218 item 6: the supervising site engineer sees the project's money figures. Ruled by Nabil, Karim to confirm.</summary>
public sealed record SupervisorSeesProjectMoney : IRuling
{
    public static readonly SupervisorSeesProjectMoney Default = new();

    public RulingNote Note => new(
        "SupervisorSeesProjectMoney",
        "Q-218-1: may the supervising site engineer see the claimed, collected and remaining figures, and the executed value of "
        + "progress, when spec.md section 9 says site engineers approve nothing financial?",
        "Which roles receive the money block of GET /api/projects/{id} and the money fields of the progress read.",
        "The owner, Finance, the Technical Office and the supervising site engineer receive them (permission ProjectMoneyRead). "
        + "The junior site engineer never receives them; the fields are absent from his JSON. Nabil ruled it; Karim has not "
        + "confirmed it.",
        "Remove engineerSupervisor from the ProjectMoneyRead row of PermissionCatalogue. No handler changes.",
        RulingStatus.Ruled);
}
