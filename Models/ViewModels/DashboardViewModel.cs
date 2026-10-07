namespace IssueFlow.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProjects { get; set; }
        public int TotalIssues { get; set; }
        public int OpenIssues { get; set; }
        public int ClosedIssues { get; set; }
        public int InProgressIssues { get; set; }
        public int ResolvedIssues { get; set; }
        public List<Issue> RecentIssues { get; set; } = new();
        public Subscription? ActiveSubscription { get; set; }
    }
}
