using IssueFlow.Models;

namespace IssueFlow.Services.Interfaces
{
    public interface IRatingService
    {
        /// <summary>Called when an Issue (task) is marked Completed.</summary>
        Task<Rating> RateIssueCompletionAsync(Issue issue, string userId);

        /// <summary>Called when a Bug is Verified (fix confirmed) or Reopened (fix rejected) — scores the Developer.</summary>
        Task<Rating> RateBugResolutionAsync(Bug bug, string userId, bool verified);

        /// <summary>
        /// Called when a Bug is Verified as valid — scores the Tester who originally reported it.
        /// A confirmed valid report always earns a high score (independent of developer reopen count).
        /// </summary>
        Task<Rating> RateBugReportAsync(Bug bug, string testerId);

        /// <summary>Average score (0-100) across a user's rated work, or null if they have none yet.</summary>
        Task<double?> GetAverageScoreAsync(string userId);

        Task<List<Rating>> GetRatingsForUserAsync(string userId);
    }
}
