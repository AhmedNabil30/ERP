namespace Kaff.Api.Features.DayLabour.RateEngagement;

/// <summary>Out of 5 — decisions.md D-139 §3. AC-210-F: refused outside 1–5.</summary>
public sealed record Request(int? Rating);
