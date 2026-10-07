namespace IssueFlow.Services.Interfaces
{
    public interface IProjectPermissionService
    {
        Task<bool> IsMemberAsync(int projectId, string userId);
        Task<string?> GetProjectRoleAsync(int projectId, string userId);
        Task<bool> CanManageMembersAsync(int projectId, string userId);
        Task<bool> CanAssignAsync(int projectId, string userId);
        Task<bool> IsProjectManagerAsync(int projectId, string userId);
        Task<IReadOnlyList<string>> AllowedTransitionsAsync(int projectId, string userId, string currentStatus, bool isAssignee);
    }
}
