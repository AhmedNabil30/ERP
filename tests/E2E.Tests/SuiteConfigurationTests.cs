namespace Kaff.E2E.Tests;

/// <summary>
/// Guards the guard: asserts this suite is actually configured to run when it matters.
/// </summary>
/// <remarks>
/// <para>
/// Every test here is an <c>[E2EFact]</c>, which skips itself when <c>KAFF_E2E_BASE_URL</c> is
/// unset so the suite stays runnable on a laptop with no stack up. That convenience has a sharp
/// edge: with the variable unset the whole run reports <b>4 skipped, exit code 0</b> — a green job
/// that tested nothing. Drop the variable from the workflow, or mistype it, and the end-to-end gate
/// silently stops being a gate.
/// </para>
/// <para>
/// This is a plain <c>[Fact]</c> so it runs unconditionally, and it fails only where a skip is a
/// lie. It is the same failure shape as decisions.md D-046: a command that looked like it passed
/// because nothing ran.
/// </para>
/// </remarks>
public sealed class SuiteConfigurationTests
{
    [Fact]
    public void The_suite_is_configured_when_running_in_CI()
    {
        if (!E2EEnvironment.IsContinuousIntegration)
        {
            // Locally an unconfigured run is the normal case, not a defect.
            return;
        }

        E2EEnvironment.IsConfigured.Should().BeTrue(
            $"CI must set {E2EEnvironment.BaseUrlVariable}; without it every end-to-end test skips "
            + "and the job passes having verified nothing");
    }
}
