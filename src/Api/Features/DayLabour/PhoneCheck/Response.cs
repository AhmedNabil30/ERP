namespace Kaff.Api.Features.DayLabour.PhoneCheck;

/// <summary>
/// A salaried record's phone matched, masked from the Site Engineer. decisions.md D-140 point 6.
/// </summary>
/// <remarks>
/// No id, code or name — a salaried name is salaried-register data, and D-139 §2 isolates that
/// register from the site. The audit record written on acknowledgement still names the real id
/// (KAFF-209 rule 9); only the response is masked.
/// </remarks>
/// <param name="Restricted">Always <c>true</c>. The property's presence is the whole signal.</param>
public sealed record RestrictedPhoneMatch(bool Restricted);

/// <summary>
/// Who already holds this number, from a Site Engineer's point of view. Empty when nobody does.
/// </summary>
/// <remarks>
/// <b>Each element is one of two shapes</b> — <see cref="Kaff.Api.Common.PhoneMatch"/> for a day-labour
/// match, <see cref="RestrictedPhoneMatch"/> for a salaried one (D-140 point 6). <c>Matches</c> is
/// declared <c>object</c> element-wise on purpose: <c>System.Text.Json</c> serialises an <c>object</c>
/// element by its runtime type, which is exactly the "two shapes, one array" wire contract D-140 asks
/// for, with no discriminator field and no third type invented to unify them.
/// </remarks>
/// <param name="Matches">Every match, archived included, ordered by code.</param>
public sealed record Response(IReadOnlyList<object> Matches);
