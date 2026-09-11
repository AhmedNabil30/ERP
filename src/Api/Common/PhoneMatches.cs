using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Common;

/// <summary>
/// One client, employee, subcontractor or supplier already holding the phone number that was typed.
/// </summary>
/// <remarks>
/// <b>The name is the point.</b> spec.md §2's amendment says the system <i>"shows the operator which
/// client already holds it"</i>, and KAFF-119 rule 4 says so again: a warning that says only "this
/// number exists" is not what was ruled. <see cref="IsArchived"/> is here because rule 6 requires the
/// warning to fire on an archived record and to say that it is one — §3 attaches a reopened
/// opportunity to the original client, so an archived match is exactly the case the operator most
/// needs to see.
/// </remarks>
/// <param name="Id">The matched record.</param>
/// <param name="Code">Its generated code, e.g. <c>C-10001</c> or <c>E-10001</c>.</param>
/// <param name="Name">Its name, as entered. Arabic, normally.</param>
/// <param name="IsArchived">Derived from the record's <c>IsActive</c>; there is no archived column.</param>
public sealed record PhoneMatch(Guid Id, string Code, string Name, bool IsArchived);

/// <summary>
/// The one query behind the duplicate-phone warning, shared across every master record that
/// deduplicates by phone. decisions.md D-141 §4 moved this here from
/// <c>Kaff.Api.Features.Clients</c> because employees, subcontractors and suppliers now share it too.
/// </summary>
/// <remarks>
/// <para>
/// <b>One query per table, two callers each, and they must not be able to disagree.</b> A
/// <c>phone-check</c> endpoint produces the warning the operator reads, and the matching create/edit
/// handler re-runs the same match server-side to decide whether the acknowledgement flag means
/// anything. A match found by one and missed by the other is a warning nobody sees or an
/// acknowledgement of nothing.
/// </para>
/// <para>
/// <b>Not a repository and not a service layer</b> — CLAUDE.md forbids both. Four static methods
/// returning data, called directly, living in <c>Api/Common</c> rather than <c>Domain/</c>, which has
/// no EF Core reference at all. The only <i>domain</i> logic in the match is normalisation, and that is
/// already shared and uncopied
/// [Verified: 2026-09-04 @ <c>src/Domain/Common/PhoneNumber.cs</c> -&gt; <c>Normalise</c>].
/// </para>
/// <para>
/// <b>No shared interface across the four entities.</b> decisions.md D-141 §4: "Four five-line queries
/// are less code than an abstraction with four implementations."
/// </para>
/// </remarks>
internal static class PhoneMatches
{
    /// <summary>
    /// Every client whose normalised phone equals <paramref name="normalisedPhone"/>, archived
    /// included, ordered by code so the same request warns in the same order twice.
    /// </summary>
    /// <param name="excluding">
    /// A client that is not a match against itself. Null on the registration path, where there is no
    /// self to exclude.
    /// </param>
    public static async Task<List<PhoneMatch>> ClientsAsync(
        KaffDbContext database,
        string normalisedPhone,
        CancellationToken cancellationToken,
        Guid? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        return await database.Clients
            .Where(client => client.PhoneNormalised == normalisedPhone)
            .Where(client => excluding == null || client.Id != excluding)
            .OrderBy(client => client.Code)
            .Select(client => new PhoneMatch(client.Id, client.Code, client.Name, !client.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every employee whose normalised phone equals <paramref name="normalisedPhone"/>, archived
    /// included, ordered by code. Matches every <c>EmployeeKind</c> — the salaried-only refusal is a
    /// separate query in <c>CreateEmployee</c>/<c>EditEmployee</c>, checked before this one runs.
    /// </summary>
    public static async Task<List<PhoneMatch>> EmployeesAsync(
        KaffDbContext database,
        string normalisedPhone,
        CancellationToken cancellationToken,
        Guid? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        return await database.Employees
            .Where(employee => employee.PhoneNormalised == normalisedPhone)
            .Where(employee => excluding == null || employee.Id != excluding)
            .OrderBy(employee => employee.Code)
            .Select(employee => new PhoneMatch(employee.Id, employee.Code, employee.FullName, !employee.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every subcontractor whose normalised phone equals <paramref name="normalisedPhone"/>, archived
    /// included, ordered by code. Built by decisions.md D-141 ahead of <c>KAFF-211</c>'s own endpoint.
    /// </summary>
    public static async Task<List<PhoneMatch>> SubcontractorsAsync(
        KaffDbContext database,
        string normalisedPhone,
        CancellationToken cancellationToken,
        Guid? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        return await database.Subcontractors
            .Where(subcontractor => subcontractor.PhoneNormalised == normalisedPhone)
            .Where(subcontractor => excluding == null || subcontractor.Id != excluding)
            .OrderBy(subcontractor => subcontractor.Code)
            .Select(subcontractor => new PhoneMatch(
                subcontractor.Id, subcontractor.Code, subcontractor.Name, !subcontractor.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every supplier whose normalised phone equals <paramref name="normalisedPhone"/>, archived
    /// included, ordered by code. Built by decisions.md D-141 ahead of <c>KAFF-212</c>'s own endpoint.
    /// </summary>
    public static async Task<List<PhoneMatch>> SuppliersAsync(
        KaffDbContext database,
        string normalisedPhone,
        CancellationToken cancellationToken,
        Guid? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        return await database.Suppliers
            .Where(supplier => supplier.PhoneNormalised == normalisedPhone)
            .Where(supplier => excluding == null || supplier.Id != excluding)
            .OrderBy(supplier => supplier.Code)
            .Select(supplier => new PhoneMatch(supplier.Id, supplier.Code, supplier.Name, !supplier.IsActive))
            .ToListAsync(cancellationToken);
    }
}
