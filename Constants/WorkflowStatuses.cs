namespace IssueFlow.Constants
{
    /// <summary>
    /// Canonical Jira-style issue workflow statuses.
    /// Use these constants everywhere — never hard-code status strings.
    /// Flow: Backlog → ToDo → InProgress → CodeReview → ReadyForTest → Testing → Done
    /// Fail path: Testing → Reopened → InProgress
    /// </summary>
    public static class IssueStatuses
    {
        public const string Backlog = "Backlog";
        public const string ToDo = "ToDo";
        public const string InProgress = "InProgress";
        public const string CodeReview = "CodeReview";
        public const string ReadyForTest = "ReadyForTest";
        public const string Testing = "Testing";
        public const string Done = "Done";
        public const string Reopened = "Reopened";

        /// <summary>All valid statuses in board column order.</summary>
        public static readonly string[] All =
        {
            Backlog, ToDo, InProgress, CodeReview, ReadyForTest, Testing, Done, Reopened
        };

        /// <summary>Statuses considered "open / active work" (not finished).</summary>
        public static readonly string[] Active =
        {
            Backlog, ToDo, InProgress, CodeReview, ReadyForTest, Testing, Reopened
        };

        /// <summary>Statuses available for developers to pick up as work.</summary>
        public static readonly string[] AvailableForWork =
        {
            Backlog, ToDo, Reopened
        };

        /// <summary>Statuses ready for QA review / testing.</summary>
        public static readonly string[] AvailableForReview =
        {
            ReadyForTest, CodeReview, Testing
        };

        /// <summary>Map any legacy status (from older data) to the current vocabulary.</summary>
        public static string Normalize(string? status) => status switch
        {
            "Open" => Backlog,
            "In Progress" => InProgress,
            "Resolved" => ReadyForTest,
            "ReadyToLaunch" => Done,
            "Closed" => Done,
            "Completed" => Done,
            null or "" => Backlog,
            _ when All.Contains(status) => status,
            _ => Backlog
        };

        public static bool IsDone(string? status)
        {
            var n = Normalize(status);
            return n == Done;
        }

        public static bool IsActive(string? status)
        {
            var n = Normalize(status);
            return Active.Contains(n);
        }
    }

    /// <summary>
    /// Bug lifecycle statuses.
    /// Flow: Reported → Assigned → Fixed → ReadyForReview → Verified
    /// Fail path: ReadyForReview → Reopened → Assigned (or stays Reopened until reassigned)
    /// </summary>
    public static class BugStatuses
    {
        public const string Reported = "Reported";
        public const string Assigned = "Assigned";
        public const string Fixed = "Fixed";
        public const string ReadyForReview = "ReadyForReview";
        public const string Verified = "Verified";
        public const string Reopened = "Reopened";

        public static readonly string[] All =
        {
            Reported, Assigned, Fixed, ReadyForReview, Verified, Reopened
        };

        public static readonly string[] Open =
        {
            Reported, Assigned, Fixed, ReadyForReview, Reopened
        };
    }

    /// <summary>Assignment request statuses.</summary>
    public static class AssignmentRequestStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Offered = "Offered";
        public const string OfferRejected = "OfferRejected";
    }

    /// <summary>Issue / Bug priority levels.</summary>
    public static class Priorities
    {
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
        public const string Critical = "Critical";

        public static readonly string[] All = { Low, Medium, High, Critical };

        public static int SortKey(string? priority) => priority switch
        {
            Critical => 0,
            High => 1,
            Medium => 2,
            _ => 3
        };
    }

    /// <summary>Issue type (Jira-style).</summary>
    public static class IssueTypes
    {
        public const string Epic = "Epic";
        public const string Story = "Story";
        public const string Task = "Task";
        public const string SubTask = "SubTask";

        public static readonly string[] All = { Epic, Story, Task, SubTask };
    }
}
