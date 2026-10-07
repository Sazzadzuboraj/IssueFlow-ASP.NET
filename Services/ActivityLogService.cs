using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    /// <summary>
    /// Records an audit trail entry for every meaningful action on an Issue or
    /// Bug. Read by the Activity timeline on detail pages and by RatingService
    /// when it needs history (e.g. how many extensions were requested).
    /// </summary>
    public class ActivityLogService : IActivityLogService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ActivityLogService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task LogAsync(string userId, string action, string? details, int? issueId = null, int? bugId = null)
        {
            var entry = new ActivityLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                IssueId = issueId,
                BugId = bugId,
                Timestamp = DateTime.UtcNow
            };
            await _unitOfWork.ActivityLogs.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<ActivityLog>> GetForIssueAsync(int issueId)
        {
            var results = await _unitOfWork.ActivityLogs.FindAsync(a => a.IssueId == issueId);
            return results
                .OrderByDescending(a => a.Timestamp)
                .ToList();
        }

        public async Task<List<ActivityLog>> GetForBugAsync(int bugId)
        {
            var results = await _unitOfWork.ActivityLogs.FindAsync(a => a.BugId == bugId);
            return results
                .OrderByDescending(a => a.Timestamp)
                .ToList();
        }

        public async Task<List<ActivityLog>> GetRecentAsync(int count = 20)
        {
            return await _unitOfWork.ActivityLogs.Query()
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToListAsync();
        }
    }
}
