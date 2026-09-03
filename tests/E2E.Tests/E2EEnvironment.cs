using System.Runtime.CompilerServices;

namespace Kaff.E2E.Tests;

/// <summary>Where the running application is, and whether there is one.</summary>
public static class E2EEnvironment
{
    public const string BaseUrlVariable = "KAFF_E2E_BASE_URL";

    /// <summary>Same variable name and default as <c>driver.mjs</c>'s <c>KAFF_API</c>, deliberately —
    /// two health checks pointed at two different hosts would be worse than either alone.</summary>
    public const string ApiBaseUrlVariable = "KAFF_API";

    public static string? BaseUrl => Environment.GetEnvironmentVariable(BaseUrlVariable);

    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable(ApiBaseUrlVariable) is { Length: > 0 } value
            ? value
            : "http://localhost:5080";

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>
    /// True when this run is CI, where an unconfigured suite is a failure rather than a convenience.
    /// </summary>
    /// <remarks>
    /// Set by <c>.github/workflows/ci.yml</c> and by every CI provider worth using.
    /// </remarks>
    public static bool IsContinuousIntegration =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A test that needs the application actually running.
/// </summary>
/// <remarks>
/// Skipped rather than failed when <c>KAFF_E2E_BASE_URL</c> is unset, so the unit and integration
/// suites stay runnable on a laptop without a full stack. CI sets the variable in the end-to-end job,
/// where a skip would hide a real failure — see .github/workflows/ci.yml.
/// </remarks>
public sealed class E2EFactAttribute : FactAttribute
{
    // The caller-info parameters are forwarded to the base constructor so xUnit can report the test's
    // source location; xUnit3003 fails the build without them.
    public E2EFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!E2EEnvironment.IsConfigured)
        {
            Skip = $"{E2EEnvironment.BaseUrlVariable} is not set; the application is not running.";
        }
    }
}
