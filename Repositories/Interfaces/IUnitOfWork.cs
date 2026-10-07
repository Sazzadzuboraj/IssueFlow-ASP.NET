using IssueFlow.Models;

namespace IssueFlow.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<Issue> Issues { get; }
        IGenericRepository<Project> Projects { get; }
        IGenericRepository<IssueAssignment> IssueAssignments { get; }
        IGenericRepository<Comment> Comments { get; }
        IGenericRepository<Subscription> Subscriptions { get; }
        IGenericRepository<Bug> Bugs { get; }
        IGenericRepository<BugAttachment> BugAttachments { get; }
        IGenericRepository<Rating> Ratings { get; }
        IGenericRepository<ActivityLog> ActivityLogs { get; }
        IGenericRepository<Notification> Notifications { get; }
        IGenericRepository<ProjectMember> ProjectMembers { get; }
        IGenericRepository<IssueAttachment> IssueAttachments { get; }

        Task<int> SaveChangesAsync();
    }
}
