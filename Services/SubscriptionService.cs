using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    /// <summary>
    /// Viewing projects/issues does not require a subscription.
    /// Creating projects/issues/bugs requires an active paid subscription row.
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;

        private static readonly Dictionary<string, (int Projects, int Issues, int Bugs)> PlanLimits = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Weekly"] = (10, 100, 100),
            ["Monthly"] = (50, 500, 500),
            ["Yearly"] = (-1, -1, -1)
        };

        public SubscriptionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>True only when user has an active subscription (paid plan) in the database.</summary>
        public async Task<bool> HasActiveSubscriptionAsync(string userId)
        {
            var now = DateTime.UtcNow;
            return await _unitOfWork.Subscriptions.Query()
                .AnyAsync(s => s.UserId == userId && s.IsActive && s.EndDate >= now);
        }

        public async Task<string> GetActivePlanAsync(string userId)
        {
            var now = DateTime.UtcNow;
            var sub = await _unitOfWork.Subscriptions.Query()
                .Where(s => s.UserId == userId && s.IsActive && s.EndDate >= now)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            return sub?.Plan ?? "None";
        }

        public async Task<(int MaxProjects, int MaxIssues, int MaxBugs)> GetLimitsAsync(string userId)
        {
            var plan = await GetActivePlanAsync(userId);
            if (plan == "None" || !PlanLimits.ContainsKey(plan))
                return (0, 0, 0);
            return PlanLimits[plan];
        }

        public async Task<(bool Allowed, string? Message)> CanCreateProjectAsync(string userId)
        {
            if (!await HasActiveSubscriptionAsync(userId))
            {
                return (false,
                    "An active subscription is required to create projects. Go to Subscription and purchase a plan.");
            }

            var (maxProjects, _, _) = await GetLimitsAsync(userId);
            if (maxProjects < 0) return (true, null);

            var count = await _unitOfWork.Projects.Query()
                .CountAsync(p => p.CreatedById == userId && p.IsActive);

            if (count >= maxProjects)
            {
                var plan = await GetActivePlanAsync(userId);
                return (false,
                    $"Your {plan} plan allows at most {maxProjects} active project(s). Upgrade your subscription to create more.");
            }
            return (true, null);
        }

        public async Task<(bool Allowed, string? Message)> CanCreateIssueAsync(string userId)
        {
            if (!await HasActiveSubscriptionAsync(userId))
            {
                return (false,
                    "An active subscription is required to create issues. Go to Subscription and purchase a plan.");
            }

            var (_, maxIssues, _) = await GetLimitsAsync(userId);
            if (maxIssues < 0) return (true, null);

            var count = await _unitOfWork.Issues.Query()
                .CountAsync(i => i.CreatedById == userId);

            if (count >= maxIssues)
            {
                var plan = await GetActivePlanAsync(userId);
                return (false,
                    $"Your {plan} plan allows at most {maxIssues} issue(s). Upgrade your subscription to create more.");
            }
            return (true, null);
        }

        public async Task<(bool Allowed, string? Message)> CanReportBugAsync(string userId)
        {
            if (!await HasActiveSubscriptionAsync(userId))
            {
                return (false,
                    "An active subscription is required to report bugs. Go to Subscription and purchase a plan.");
            }

            var (_, _, maxBugs) = await GetLimitsAsync(userId);
            if (maxBugs < 0) return (true, null);

            var count = await _unitOfWork.Bugs.Query()
                .CountAsync(b => b.ReportedById == userId);

            if (count >= maxBugs)
            {
                var plan = await GetActivePlanAsync(userId);
                return (false,
                    $"Your {plan} plan allows at most {maxBugs} bug report(s). Upgrade your subscription to report more.");
            }
            return (true, null);
        }

        /// <summary>No-op: Free plan self-activation is disabled. Kept for interface compatibility.</summary>
        public Task EnsureDefaultSubscriptionAsync(string userId) => Task.CompletedTask;
    }
}
