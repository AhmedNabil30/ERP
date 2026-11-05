namespace Kaff.Api.Features.Closure.RaiseCallback;

/// <summary>A callback against the running warranty. The date is today; the warranty end is never typed.</summary>
/// <param name="Description">What the client reports.</param>
public sealed record Request(string? Description);
