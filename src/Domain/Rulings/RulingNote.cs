namespace Kaff.Domain.Rulings;

/// <summary>Whether the business has answered the question behind a rule.</summary>
public enum RulingStatus
{
    /// <summary>Nobody with authority has ruled. The code runs on a default we chose.</summary>
    Assumed = 1,

    /// <summary>The business ruled and the default now matches the ruling.</summary>
    Ruled = 2,
}

/// <summary>
/// The self-description every entry in <c>Kaff.Domain.Rulings</c> carries. It is data, so a test (and
/// later a screen) can list every rule the system is running on without anyone reading the code.
/// </summary>
/// <param name="Name">The rule's name, the same as the type that implements it.</param>
/// <param name="Question">The open question it answers (<c>Q58</c>, <c>Q82</c>, or a plain description).</param>
/// <param name="Rule">What the rule decides, in one sentence.</param>
/// <param name="RunningOn">The default the code runs on today.</param>
/// <param name="IfRuledDifferently">What changes when the business rules differently.</param>
/// <param name="Status">Assumed until the business answers.</param>
public sealed record RulingNote(
    string Name,
    string Question,
    string Rule,
    string RunningOn,
    string IfRuledDifferently,
    RulingStatus Status);

/// <summary>An entry in the rulings module. Every entry describes itself.</summary>
public interface IRuling
{
    RulingNote Note { get; }
}
