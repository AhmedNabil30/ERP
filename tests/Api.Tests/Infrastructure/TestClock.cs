namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// A <see cref="TimeProvider"/> a test moves by hand.
/// </summary>
/// <remarks>
/// <para>
/// Two rules in this system are defined in minutes rather than in states — the fifteen-minute
/// lockout and the thirty-minute sliding session, both Karim's (spec.md §9 amendment, decisions.md
/// D-049 rulings 2 and 3) — and neither is observable without moving a clock. The alternative,
/// configuring the windows down to something a test can wait out, would assert a different number
/// from the shipped one.
/// </para>
/// <para>
/// <b>Twelve lines rather than <c>Microsoft.Extensions.TimeProvider.Testing</c>.</b> CLAUDE.md: "Do
/// not add a package that duplicates something the framework already does." <see cref="TimeProvider"/>
/// is the framework's abstraction and this is the two-method subclass of it that the two tests need;
/// <c>FakeTimeProvider</c>'s timer scheduling has no reader here.
/// </para>
/// <para>
/// <b>It starts at the real clock, not at a fixed date.</b> The API's JWT validation uses the
/// framework's own clock, which no <see cref="TimeProvider"/> reaches, so a token minted here has to
/// carry an expiry a real validator will believe. Starting from now and offsetting is what keeps the
/// two clocks in the same century.
/// </para>
/// </remarks>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now;

    public TestClock(TimeSpan offsetFromRealNow = default) => _now = DateTimeOffset.UtcNow + offsetFromRealNow;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward. There is deliberately no way to move it back.</summary>
    public void Advance(TimeSpan by) => _now += by;
}
