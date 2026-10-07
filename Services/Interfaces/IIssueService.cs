using IssueFlow.DTOs;
using IssueFlow.Models;

namespace IssueFlow.Services.Interfaces
{
    public interface IIssueService
    {
        Task<List<Issue>> GetIssuesAsync(
            string? search, string? status, string? priority,
            ApplicationUser currentUser, bool isPrivilegedRole);

        Task<List<Issue>> GetIssuesFilteredAsync(
            string? search, string? status, string? priority, int? projectId, string? deadlineFilter,
            int? take = null);

        Task<Issue?> GetIssueDetailsAsync(int id);
        Task<Issue?> GetByIdAsync(int id);
        Task<Issue> CreateIssueAsync(CreateIssueDto dto, string createdById);
        Task<bool> UpdateIssueAsync(UpdateIssueDto dto);
        Task<bool> DeleteIssueAsync(int id);
        Task<bool> ChangeStatusAsync(int id, string newStatus, ApplicationUser currentUser, bool isPrivilegedRole);
        Task<Comment> AddCommentAsync(int issueId, string userId, string content);
        Task<bool> RequestExtensionAsync(int issueId, string developerId, DateTime requestedDeadline, string reason);
        Task<bool> ReviewExtensionAsync(int issueId, string managerId, bool approve);

        Task<bool> IsAssignedAsync(int issueId, string userId);
        Task<(bool Ok, string? Error)> AssignDeveloperAsync(int issueId, string developerId, string assignedByUserId);

        IssueDto ToDto(Issue issue);
    }
}
