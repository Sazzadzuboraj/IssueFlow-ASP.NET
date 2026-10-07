using IssueFlow.Data;
using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;

namespace IssueFlow.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        private IGenericRepository<Issue>? _issues;
        private IGenericRepository<Project>? _projects;
        private IGenericRepository<IssueAssignment>? _issueAssignments;
        private IGenericRepository<Comment>? _comments;
        private IGenericRepository<Subscription>? _subscriptions;
        private IGenericRepository<Bug>? _bugs;
        private IGenericRepository<BugAttachment>? _bugAttachments;
        private IGenericRepository<Rating>? _ratings;
        private IGenericRepository<ActivityLog>? _activityLogs;
        private IGenericRepository<Notification>? _notifications;
        private IGenericRepository<ProjectMember>? _projectMembers;
        private IGenericRepository<IssueAttachment>? _issueAttachments;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IGenericRepository<Issue> Issues => _issues ??= new GenericRepository<Issue>(_context);
        public IGenericRepository<Project> Projects => _projects ??= new GenericRepository<Project>(_context);
        public IGenericRepository<IssueAssignment> IssueAssignments => _issueAssignments ??= new GenericRepository<IssueAssignment>(_context);
        public IGenericRepository<Comment> Comments => _comments ??= new GenericRepository<Comment>(_context);
        public IGenericRepository<Subscription> Subscriptions => _subscriptions ??= new GenericRepository<Subscription>(_context);
        public IGenericRepository<Bug> Bugs => _bugs ??= new GenericRepository<Bug>(_context);
        public IGenericRepository<BugAttachment> BugAttachments => _bugAttachments ??= new GenericRepository<BugAttachment>(_context);
        public IGenericRepository<Rating> Ratings => _ratings ??= new GenericRepository<Rating>(_context);
        public IGenericRepository<ActivityLog> ActivityLogs => _activityLogs ??= new GenericRepository<ActivityLog>(_context);
        public IGenericRepository<Notification> Notifications => _notifications ??= new GenericRepository<Notification>(_context);
        public IGenericRepository<ProjectMember> ProjectMembers => _projectMembers ??= new GenericRepository<ProjectMember>(_context);
        public IGenericRepository<IssueAttachment> IssueAttachments => _issueAttachments ??= new GenericRepository<IssueAttachment>(_context);

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
