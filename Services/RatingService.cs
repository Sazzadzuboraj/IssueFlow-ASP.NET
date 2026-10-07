using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    /// <summary>
    /// Automatically scores completed work — never set by a Manager by hand.
    ///
    /// Scoring rules (documented here so they're easy to defend/explain in a
    /// report or viva):
    ///
    /// Issue (task) completion:
    ///   - Base score: 100
    ///   - No deadline set: score stays 100 (nothing to measure against)
    ///   - Completed on/before deadline: 100
    ///   - Completed late: -5 points per day late, floored at 30
    ///   - An approved extension resets the "deadline" used for lateness to the
    ///     new agreed date, but caps the max score at 90 (finished later than
    ///     originally promised, even if the delay was authorized)
    ///   - A rejected extension that still finishes late is scored purely on
    ///     the original deadline (no leniency)
    ///
    /// Bug resolution:
    ///   - Verified by tester on first review: 100
    ///   - Verified after 1 reopen (fix needed a retry): 75
    ///   - Verified after 2+ reopens: 50
    ///   - Reopened (fix rejected): 40 for that attempt
    /// </summary>
    public class RatingService : IRatingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public RatingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Rating> RateIssueCompletionAsync(Issue issue, string userId)
        {
            double score = 100;
            string reason;

            var completedDate = issue.UpdatedDate ?? DateTime.UtcNow;

            if (issue.Deadline == null)
            {
                reason = "Completed (no deadline was set).";
            }
            else if (completedDate.Date <= issue.Deadline.Value.Date)
            {
                reason = "Completed on or before the deadline.";
            }
            else
            {
                var daysLate = (completedDate.Date - issue.Deadline.Value.Date).Days;
                score = Math.Max(30, 100 - (daysLate * 5));

                if (issue.ExtensionStatus == "Approved")
                {
                    score = Math.Min(score, 90);
                    reason = $"Completed {daysLate} day(s) after the original deadline (an extension was approved).";
                }
                else
                {
                    reason = $"Completed {daysLate} day(s) after the deadline.";
                }
            }

            var rating = new Rating
            {
                UserId = userId,
                IssueId = issue.Id,
                Score = score,
                Reason = reason,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Ratings.AddAsync(rating);
            await _unitOfWork.SaveChangesAsync();
            return rating;
        }

        public async Task<Rating> RateBugResolutionAsync(Bug bug, string userId, bool verified)
        {
            double score;
            string reason;

            if (!verified)
            {
                score = 40;
                reason = "Fix was reopened by the tester (did not pass review).";
            }
            else
            {
                // Count how many times this bug was previously reopened, using the activity log.
                var reopenCount = await _unitOfWork.ActivityLogs
                    .Query()
                    .Where(a => a.BugId == bug.Id && a.Action == "BugReopened")
                    .CountAsync();

                score = reopenCount switch
                {
                    0 => 100,
                    1 => 75,
                    _ => 50
                };
                reason = reopenCount == 0
                    ? "Fix verified by tester on the first review."
                    : $"Fix verified by tester after {reopenCount} reopen(s).";
            }

            var rating = new Rating
            {
                UserId = userId,
                BugId = bug.Id,
                Score = score,
                Reason = reason,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Ratings.AddAsync(rating);
            await _unitOfWork.SaveChangesAsync();
            return rating;
        }

        /// <summary>
        /// Scores the Tester who reported the bug once it is verified as a valid defect.
        /// Always awards 100 — a confirmed valid report is good work regardless of how
        /// many times the developer needed to retry the fix.
        /// </summary>
        public async Task<Rating> RateBugReportAsync(Bug bug, string testerId)
        {
            var rating = new Rating
            {
                UserId = testerId,
                BugId = bug.Id,
                Score = 100,
                Reason = "Bug report verified as valid by review.",
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Ratings.AddAsync(rating);
            await _unitOfWork.SaveChangesAsync();
            return rating;
        }

        public async Task<double?> GetAverageScoreAsync(string userId)
        {
            var ratings = await _unitOfWork.Ratings.FindAsync(r => r.UserId == userId);
            var list = ratings.ToList();
            return list.Count == 0 ? null : list.Average(r => r.Score);
        }

        public async Task<List<Rating>> GetRatingsForUserAsync(string userId)
        {
            var results = await _unitOfWork.Ratings.FindAsync(r => r.UserId == userId);
            return results.OrderByDescending(r => r.CreatedDate).ToList();
        }
    }
}
