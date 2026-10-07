using IssueFlow.Models;

namespace IssueFlow.Models.ViewModels
{
    /// <summary>Shared stats shown on every role dashboard.</summary>
    public class DashboardStats
    {
        public int TotalProjects { get; set; }
        public int TotalIssues { get; set; }
        public int OpenIssues { get; set; }
        public int InProgressIssues { get; set; }
        public int ResolvedIssues { get; set; }
        public int ClosedIssues { get; set; }
        public int TotalBugs { get; set; }
        public int OpenBugs { get; set; }
        public int ReadyForReviewBugs { get; set; }
        public int ReadyForReviewIssues { get; set; }
        public int VerifiedBugs { get; set; }
    }

    /// <summary>Admin / ProjectManager dashboard.</summary>
    public class ManagerDashboardViewModel
    {
        public DashboardStats Stats { get; set; } = new();
        public List<Issue> PendingExtensions { get; set; } = new();
        public List<Bug> UnassignedBugs { get; set; } = new();
        public List<Bug> ReadyForReviewBugs { get; set; } = new();
        public List<TeamMemberPerformance> TeamPerformance { get; set; } = new();
        public List<ActivityLog> RecentActivity { get; set; } = new();
        public List<Issue> RecentIssues { get; set; } = new();

        // Chart data (serialized to JSON in the view)
        public Dictionary<string, int> IssuesByStatus { get; set; } = new();
        public Dictionary<string, int> BugsByStatus { get; set; } = new();
        public Dictionary<string, int> IssuesByPriority { get; set; } = new();
    }

    public class TeamMemberPerformance
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public double? AverageScore { get; set; }
        public int CompletedCount { get; set; }
        public int OpenAssignments { get; set; }
    }

    /// <summary>Developer dashboard — only their assigned work.</summary>
    public class DeveloperDashboardViewModel
    {
        public DashboardStats Stats { get; set; } = new();
        public List<Issue> MyIssues { get; set; } = new();
        public List<Bug> MyBugs { get; set; } = new();
        public List<Issue> PendingExtensionRequests { get; set; } = new();
        public double? MyAverageScore { get; set; }
        public List<Rating> RecentRatings { get; set; } = new();
        public List<ActivityLog> RecentActivity { get; set; } = new();
    }

    /// <summary>Tester / QA dashboard.</summary>
    public class TesterDashboardViewModel
    {
        public DashboardStats Stats { get; set; } = new();
        public List<Bug> MyReportedBugs { get; set; } = new();
        public List<Bug> AwaitingMyReview { get; set; } = new();
        public double? MyAverageScore { get; set; }
        public List<Rating> RecentRatings { get; set; } = new();
        public List<ActivityLog> RecentActivity { get; set; } = new();
    }
}
