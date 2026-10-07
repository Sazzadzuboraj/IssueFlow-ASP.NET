using IssueFlow.Models;

namespace IssueFlow.Services.Interfaces
{
    /// <summary>Owns Project CRUD, membership, and lookup lists for dropdowns.</summary>
    public interface IProjectService
    {
        Task<List<Project>> GetActiveProjectsAsync(string? search = null);
        Task<Project?> GetByIdAsync(int id);
        Task<Project?> GetDetailsAsync(int id);
        Task<Project> CreateAsync(string name, string? description, string createdById);
        Task<bool> UpdateAsync(int id, string name, string? description);
        Task<bool> SoftDeleteAsync(int id);
        Task<bool> ExistsAsync(int id);

        Task<Project?> GetWithMembersAsync(int id);
        Task<(bool Ok, string? Error)> AddMemberAsync(int projectId, string userId, string role);
        Task<bool> RemoveMemberAsync(int memberId);
    }
}
