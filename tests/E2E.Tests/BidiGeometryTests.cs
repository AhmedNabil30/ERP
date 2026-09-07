using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;

namespace Kaff.E2E.Tests;

/// <summary>
/// One <c>&lt;bdi&gt;</c>, measured. What the direction, isolation and overflow checks cannot see.
/// </summary>
/// <param name="Text">The run inside it, for a failure message that names the element on screen.</param>
/// <param name="BoxWidth">The element's own border box.</param>
/// <param name="TextWidth">A <c>Range</c> over its text nodes — what the box would be if it fitted.</param>
/// <param name="BoxStart">The box's inline-start edge. Under RTL that is its <b>right</b> edge.</param>
/// <param name="TextStart">The text's inline-start edge, measured the same way.</param>
/// <param name="NameStart">
/// The inline-start edge of the row's name text, where the element sits in a row that has one. Null
/// elsewhere, which is why the sweep does not assert it.
/// </param>
internal sealed record BdiMeasurement(
    string Text,
    double BoxWidth,
    double TextWidth,
    double BoxStart,
    double TextStart,
    double? NameStart)
{
    /// <summary>
    /// Read out of the raw <c>JsonElement</c> rather than deserialised into.
    /// </summary>
    /// <remarks>
    /// Playwright's own argument converter constructs the target type with
    /// <c>Activator.CreateInstance</c> and fails on a record — <i>"No parameterless constructor
    /// defined"</i>. A settable class would satisfy it and would also let a renamed field arrive as a
    /// silent default; reading the element by name fails loudly instead.
    /// </remarks>
    public static BdiMeasurement From(JsonElement element) => new(
        element.GetProperty("text").GetString() ?? string.Empty,
        element.GetProperty("boxWidth").GetDouble(),
        element.GetProperty("textWidth").GetDouble(),
        element.GetProperty("boxStart").GetDouble(),
        element.GetProperty("textStart").GetDouble(),
        element.GetProperty("nameStart") is { ValueKind: JsonValueKind.Number } name
            ? name.GetDouble()
            : null);
}

/// <summary>
/// <c>TC-1-271</c> and <c>TC-1-272</c> — <c>F-1</c>, and the sweep that closes its class.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why geometry, when everything else was already green.</b> <c>F-1</c> shipped inside
/// <c>AC-126-A</c>, which was reported discharged on 2026-09-04 by a session that drove Chromium and
/// took a screenshot. The row does not overflow — the measurement is <b>0px</b> — <c>dir</c> is right,
/// the isolation is right and the strings are right. <c>TC-1-200</c> asserts exactly those and is green
/// with the defect in shipped code. A stretched box and a shrink-wrapped one have identical direction,
/// identical isolation and identical overflow; <b>the only thing that separates them is where the box
/// ends.</b>
/// </para>
/// <para>
/// <b>What the defect is.</b> A <c>&lt;bdi&gt;</c> that is a grid item is stretched by
/// <c>justify-self</c>'s initial value, so its box spans the whole grid column. Its own resolved
/// direction is LTR — that is what first-strong isolation means for a phone number or a client code —
/// so its text sits at the box's <b>left</b> while the Arabic name above it is aligned right, and the
/// card reads as two columns that do not line up. The remedy shrinks the box to its content; the
/// isolation is untouched, because this moves the box and not the text inside it.
/// </para>
/// <para>
/// <b>The positive control is in the same run, and it is not decoration.</b> The user list is built the
/// same way and already carries the fix. A suite that only measured the broken screen could not tell
/// <i>"the rule is enforced"</i> from <i>"the measurement never fired"</i> — D-046's whole subject.
/// </para>
/// <para>
/// <b>Watched failing before it was watched passing</b> — <c>qa/strategy.md</c> §5, and this project's
/// most repeated defect is a check that reports a safety it does not have. Against <c>bce77f4</c> with
/// the CSS unfixed, on a stack seeded by <c>scripts/seed-demo.ps1</c>:
/// <code>
/// on the client list, &lt;bdi&gt;01001234567&lt;/bdi&gt;
///   box  308.0px starting at 349.0
///   text  85.4px starting at 126.4
///   name        starting at 349.0
/// </code>
/// The user list, measured first in the same run, passed. <b>Note which assertion caught it:</b> the
/// box's inline-start edge and the name's agreed to the pixel — 349.0 against 349.0 — because both
/// boxes were stretched to the same column. Alignment alone is green against <c>F-1</c>. The width is
/// what separates a stretched box from a shrink-wrapped one, and the 222.6px between the box's start
/// and its own text's is the gap a reader sees.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public sealed class BidiGeometryTests
{
    private readonly PlaywrightFixture _playwright;

    public BidiGeometryTests(PlaywrightFixture playwright) => _playwright = playwright;

    /// <summary>
    /// Subpixel text metrics move by fractions of a pixel between runs; a stretched grid item misses by
    /// hundreds. <c>TC-1-271</c> names ±1px and that is what this is.
    /// </summary>
    private const double Tolerance = 1.0;

    /// <summary>
    /// Every <c>&lt;bdi&gt;</c> in the document, enumerated from the DOM.
    /// </summary>
    /// <remarks>
    /// <b>Enumerated, never listed.</b> A hand-written list of five selectors is the shape that stayed
    /// green when a row was deleted (D-122 §4), and it is also the shape that cannot see the sixth. The
    /// row's name is found through the nearest element carrying a test id, so the comparison is against
    /// the row the element actually belongs to rather than against whichever name happens to be first
    /// in the document.
    /// </remarks>
    private const string MeasureEveryBdi = """
        () => {
          const rtl = getComputedStyle(document.documentElement).direction === 'rtl';
          const startOf = (rect) => (rtl ? rect.right : rect.left);
          const textRect = (element) => {
            const range = document.createRange();
            range.selectNodeContents(element);
            return range.getBoundingClientRect();
          };

          return Array.from(document.querySelectorAll('bdi')).map((bdi) => {
            const box = bdi.getBoundingClientRect();
            const text = textRect(bdi);
            const row = bdi.closest('[data-testid]');
            const name = row === null ? null : row.querySelector('.row-name');

            return {
              text: (bdi.textContent || '').trim(),
              boxWidth: box.width,
              textWidth: text.width,
              boxStart: startOf(box),
              textStart: startOf(text),
              nameStart: name === null ? null : startOf(textRect(name)),
            };
          });
        }
        """;

    /// <summary>
    /// <c>TC-1-271</c> · <c>AC-126-A</c> — the client list's <c>&lt;bdi&gt;</c> sits under the name it
    /// belongs to, with the user list measured in the same run as the positive control.
    /// </summary>
    [E2EFact]
    public async Task The_client_lists_bdi_boxes_shrink_to_their_own_text()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await E2ESession.SignInAsync(page, E2ESession.OwnerUser, E2ESession.OwnerPassword);

        // The positive control first, so a measurement that never fires is caught before the screen
        // under test rather than after it.
        IReadOnlyList<BdiMeasurement> control = await MeasureAsync(page, "/users", "user-rows");

        control.Should().NotBeEmpty(
            "an empty list has no <bdi> and the measurement passes over nothing at all — the absence "
            + "test that could not fail, in its most ordinary disguise");

        AssertShrinkWrapped(control, "the user list, which already carries `justify-self: start`");
        AssertAlignedWithTheName(control, "the user list");

        IReadOnlyList<BdiMeasurement> clients = await MeasureAsync(page, "/clients", "client-rows");

        clients.Should().NotBeEmpty("seeded by scripts/seed-demo.ps1 — two clients, C-10001 and C-10002");

        AssertShrinkWrapped(clients, "the client list");
        AssertAlignedWithTheName(clients, "the client list");
    }

    /// <summary>
    /// <c>TC-1-272</c> · <c>AC-126-A</c>, <c>AC-127-A</c>, <c>AC-128-A</c> — every <c>&lt;bdi&gt;</c> in
    /// the application, swept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name comparison is not asserted here and that is deliberate.</b> Not every screen is a
    /// list of rows with a name above the isolated run, so a sweep that required one would either skip
    /// the screens that have none or invent a relationship that is not there. What generalises is the
    /// self-referential half: <b>a box no wider than its own text, starting where its own text starts.</b>
    /// A stretched grid item fails both, on any screen, in any layout.
    /// </para>
    /// <para>
    /// <b>Data on the screen is part of the case</b>, for the reason the empty-list assertion states.
    /// </para>
    /// </remarks>
    [E2EFact]
    public async Task Every_bdi_in_the_application_shrinks_to_its_own_text()
    {
        IPage page = await _playwright.NewMobilePageAsync();

        await E2ESession.SignInAsync(page, E2ESession.OwnerUser, E2ESession.OwnerPassword);

        int measured = 0;

        foreach ((string route, string readyTestId) in SweptScreens)
        {
            IReadOnlyList<BdiMeasurement> screen = await MeasureAsync(page, route, readyTestId);

            AssertShrinkWrapped(screen, route);

            measured += screen.Count;
        }

        measured.Should().BeGreaterThan(
            0,
            "a sweep that enumerated nothing reports a safety it does not have — every screen listed "
            + "must have been rendered with data on it");
    }

    /// <summary>
    /// The slice-1 screens that render isolated Latin runs, each with the test id that says it arrived.
    /// </summary>
    /// <remarks>
    /// A route added to this list is a route swept. A screen built later and not added is the gap this
    /// list cannot close on its own — which is why <c>TC-1-272</c> is worded against the DOM within a
    /// screen rather than against the set of screens.
    /// </remarks>
    private static readonly (string Route, string ReadyTestId)[] SweptScreens =
    [
        ("/clients", "client-rows"),
        ("/users", "user-rows"),

        // KAFF-128. `AC-128-A` names timestamps and identifiers specifically, and a timestamp is the
        // densest Latin-in-Arabic run in the slice — four Latin runs and three separators.
        ("/audit", "audit-rows"),
    ];

    private static async Task<IReadOnlyList<BdiMeasurement>> MeasureAsync(
        IPage page,
        string route,
        string readyTestId)
    {
        await page.GotoAsync(route);
        await page.GetByTestId(readyTestId).WaitForAsync();

        JsonElement measured = await page.EvaluateAsync<JsonElement>(MeasureEveryBdi);

        return [.. measured.EnumerateArray().Select(BdiMeasurement.From)];
    }

    /// <summary>The measurement <c>F-1</c> fails: the box is the whole grid column, the text is not.</summary>
    private static void AssertShrinkWrapped(IReadOnlyList<BdiMeasurement> measurements, string screen)
    {
        foreach (BdiMeasurement bdi in measurements)
        {
            bdi.BoxWidth.Should().BeLessThanOrEqualTo(
                bdi.TextWidth + Tolerance,
                Because(
                    screen,
                    bdi,
                    "its box spans the whole grid column while its Latin content renders hard against "
                    + "the physical left, under a right-aligned Arabic name"));

            Math.Abs(bdi.BoxStart - bdi.TextStart).Should().BeLessThanOrEqualTo(
                Tolerance,
                Because(screen, bdi, "the box and the run inside it must begin at the same edge"));
        }
    }

    /// <summary>And the row reads as one column: the run begins where the name begins.</summary>
    private static void AssertAlignedWithTheName(IReadOnlyList<BdiMeasurement> measurements, string screen)
    {
        foreach (BdiMeasurement bdi in measurements)
        {
            bdi.NameStart.Should().NotBeNull(
                Because(screen, bdi, "every row on these two lists carries a name to line up with"));

            Math.Abs(bdi.BoxStart - bdi.NameStart!.Value).Should().BeLessThanOrEqualTo(
                Tolerance,
                Because(screen, bdi, "the card must not read as two columns that do not line up"));
        }
    }

    private static string Because(string screen, BdiMeasurement bdi, string what) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"on {screen}, <bdi>{bdi.Text}</bdi> — {what}. box {bdi.BoxWidth:F1}px starting at "
            + $"{bdi.BoxStart:F1}, text {bdi.TextWidth:F1}px starting at {bdi.TextStart:F1}, "
            + $"name starting at {bdi.NameStart:F1}");
}
