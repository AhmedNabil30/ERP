using Kaff.Domain.Common;

namespace Kaff.Domain.Projects;

/// <summary>Error catalogue for the project entity and its state machine (spec.md §13).</summary>
public static class ProjectErrors
{
    /// <summary>KAFF-400: defence in depth for GetProject. Reachable only if a project is deleted between the gate and the read — Project has no delete path.</summary>
    public static readonly Error NotFound =
        Error.NotFound("project.not_found", "errors.project.not_found");

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

    /// <summary>KAFF-400: a create request named no client, or named an empty guid.</summary>
    public static readonly Error ClientRequired =
        Error.Validation("project.client_required", "errors.project.client_required");

    /// <summary>KAFF-400: a rate percentage below zero. <c>Percentage.FromPercent</c> throws rather than refusing.</summary>
    public static readonly Error RateMustNotBeNegative =
        Error.Validation("project.rate_must_not_be_negative", "errors.project.rate_must_not_be_negative");

    public static readonly Error AreaRequired =
        Error.Validation("project.area_required", "errors.project.area_required");

    public static readonly Error AlreadySigned =
        Error.Conflict("project.already_signed", "errors.project.already_signed");

    public static readonly Error NotSigned =
        Error.Conflict("project.not_signed", "errors.project.not_signed");

    /// <summary>spec.md §5.4: "A project MUST NOT mutate from one type into another."</summary>
    public static readonly Error ProjectCannotLinkToItself =
        Error.Validation("project.cannot_link_to_itself", "errors.project.cannot_link_to_itself");

    /// <summary>
    /// KAFF-400: a create request may only start a project at Setup (the default) or skip straight to
    /// Active. spec.md §13's state machine has no other legal first step, and متعثرة / تم تأجيلها are
    /// health tags, never states (D-044 §7) — they cannot be an initial status either.
    /// </summary>
    public static readonly Error InitialStatusNotSupported =
        Error.Validation("project.initial_status_not_supported", "errors.project.initial_status_not_supported");
}
