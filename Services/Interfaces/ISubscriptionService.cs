namespace IssueFlow.Services.Interfaces
{
    /// <summary>
    /// Plan limits and active-subscription checks.
    /// Free (no active sub): limited projects/issues.
    /// Weekly / Monthly / Yearly: higher or unlimited quotas.
    /// </summary>
    public interface ISubscriptionService
    {
        Task<bool> HasActiveSubscriptionAsync(string userId);
        Task<string> GetActivePlanAsync(string userId); // "Free", "Weekly", "Monthly", "Yearly"
        Task<(bool Allowed, string? Message)> CanCreateProjectAsync(string userId);
        Task<(bool Allowed, string? Message)> CanCreateIssueAsync(string userId);
        Task<(bool Allowed, string? Message)> CanReportBugAsync(string userId);
        Task<(int MaxProjects, int MaxIssues, int MaxBugs)> GetLimitsAsync(string userId);
        Task EnsureDefaultSubscriptionAsync(string userId);
    }
}