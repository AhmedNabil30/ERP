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

    /// <summary>D-217 item 29: a Lump Sum signature carries the hold and the advance, typed for this project. No default applies.</summary>
    public static readonly Error SigningTermsRequired =
        Error.Validation("project.signing_terms_required", "errors.project.signing_terms_required");

    /// <summary>
    /// KAFF-417: hold and advance are Lump Sum terms; a Cost Plus or Design signature may not carry them. A
    /// validation (400) error, not the Conflict above, but it reuses that error's message key because the
    /// sentence is the same and the translation files belong to the frontend lane.
    /// </summary>
    public static readonly Error SigningTermsNotForContractType =
        Error.Validation("project.signing_terms_not_for_contract_type", "errors.project.terms_do_not_match_contract_type");

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

    public static readonly Error Archived =
        Error.Validation("project.archived", "errors.project.archived");

    public static readonly Error NotArchived =
        Error.Conflict("project.not_archived", "errors.project.not_archived");

    public static readonly Error AlreadySigned =
        Error.Conflict("project.already_signed", "errors.project.already_signed");

    public static readonly Error NotSigned =
        Error.Conflict("project.not_signed", "errors.project.not_signed");

    /// <summary>spec.md §5.4: "A project MUST NOT mutate from one type into another."</summary>
    public static readonly Error ProjectCannotLinkToItself =
        Error.Validation("project.cannot_link_to_itself", "errors.project.cannot_link_to_itself");

    /// <summary>KAFF-415: only a design project converts into an execution project.</summary>
    public static readonly Error NotDesign =
        Error.Conflict("project.not_design", "errors.project.not_design");

    /// <summary>KAFF-415, Q-207-7: one execution project per design project.</summary>
    public static readonly Error AlreadyConverted =
        Error.Conflict("project.already_converted", "errors.project.already_converted");

    /// <summary>KAFF-415, Q-207-9: a design project converts into Lump Sum or Cost Plus only.</summary>
    public static readonly Error ExecutionTypeNotAllowed =
        Error.Validation("project.execution_type_not_allowed", "errors.project.execution_type_not_allowed");

    /// <summary>
    /// KAFF-400: a create request may only start a project at Setup (the default) or skip straight to
    /// Active. spec.md §13's state machine has no other legal first step, and متعثرة / تم تأجيلها are
    /// health tags, never states (D-044 §7) — they cannot be an initial status either.
    /// </summary>
    public static readonly Error InitialStatusNotSupported =
        Error.Validation("project.initial_status_not_supported", "errors.project.initial_status_not_supported");
}
