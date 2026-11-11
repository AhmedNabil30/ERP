namespace Kaff.Api.Features.Boqs.CopyBoq;

/// <summary>Name exactly one source: a prepared base, or an earlier project the caller can open.</summary>
public sealed record Request(Guid? BaseId, Guid? SourceProjectId);
