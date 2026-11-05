namespace Kaff.Api.Features.Closure.TerminateProject;

/// <summary>Why termination is asked for. The request is always refused (Q108); nothing reads the reason.</summary>
/// <param name="Reason">Why.</param>
public sealed record Request(string? Reason);
