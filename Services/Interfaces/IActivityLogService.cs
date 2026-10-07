using IssueFlow.Models;

namespace IssueFlow.Services.Interfaces
{
    public interface IActivityLogService
    {
        Task LogAsync(string userId, string action, string? details, int? issueId = null, int? bugId = null);
        Task<List<ActivityLog>> GetForIssueAsync(int issueId);
        Task<List<ActivityLog>> GetForBugAsync(int bugId);
        Task<List<ActivityLog>> GetRecentAsync(int count = 20);
    }
}
