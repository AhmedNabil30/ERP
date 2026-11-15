namespace Kaff.Api.Features.DailyLogs;

/// <summary>Where a stored site photo is fetched from. The one place the route is spelled for the reads and the upload.</summary>
internal static class PhotoRoute
{
    public static string Of(Guid projectId, string reference) => $"/api/projects/{projectId}/daily-logs/photos/{reference}";
}
