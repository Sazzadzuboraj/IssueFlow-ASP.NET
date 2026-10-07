using IssueFlow.DTOs;
using IssueFlow.Models;
using Microsoft.AspNetCore.Http;

namespace IssueFlow.Services.Interfaces
{
    public interface IBugService
    {
        Task<List<Bug>> GetBugsAsync(string? search, string? status, string? priority, ApplicationUser currentUser, bool isPrivilegedRole);
        Task<Bug?> GetBugDetailsAsync(int id);
        Task<Bug> ReportBugAsync(CreateBugDto dto, string reportedById, List<IFormFile>? attachments);
        Task<bool> AssignBugAsync(AssignBugDto dto);

        /// <summary>Developer marks a bug as fixed and sends it to review.</summary>
        Task<bool> SubmitFixAsync(int bugId, string developerId);

        /// <summary>Tester verifies the fix (closes the bug) or rejects it (reopens for the developer).</summary>
        Task<bool> ReviewFixAsync(int bugId, string testerId, bool approve, string? reviewNotes);
    }
}
