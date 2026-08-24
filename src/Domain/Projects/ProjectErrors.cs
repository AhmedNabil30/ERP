using Kaff.Domain.Common;

namespace Kaff.Domain.Projects;

/// <summary>Error catalogue for the project entity and its state machine (spec.md §13).</summary>
public static class ProjectErrors
{
    public static readonly Error CodeRequired =
        Error.Validation("project.code_required", "errors.project.code_required");

    public static readonly Error NameRequired =
        Error.Validation("project.name_required", "errors.project.name_required");

    public static readonly Error IllegalTransition =
        Error.Conflict("project.illegal_transition", "errors.project.illegal_transition");

    public static readonly Error ReasonRequired =
        Error.Validation("project.reason_required", "errors.project.reason_required");

    /// <summary>Terms belonging to another contract type were supplied (spec.md §5).</summary>
    public static readonly Error TermsDoNotMatchContractType =
        Error.Conflict("project.terms_do_not_match_contract_type", "errors.project.terms_do_not_match_contract_type");

    public static readonly Error ContractValueRequired =
        Error.Validation("project.contract_value_required", "errors.project.contract_value_required");

    public static readonly Error AreaRequired =
        Error.Validation("project.area_required", "errors.project.area_required");

    public static readonly Error AlreadySigned =
        Error.Conflict("project.already_signed", "errors.project.already_signed");

    public static readonly Error NotSigned =
        Error.Conflict("project.not_signed", "errors.project.not_signed");

    /// <summary>spec.md §5.4: "A project MUST NOT mutate from one type into another."</summary>
    public static readonly Error ProjectCannotLinkToItself =
        Error.Validation("project.cannot_link_to_itself", "errors.project.cannot_link_to_itself");
}
