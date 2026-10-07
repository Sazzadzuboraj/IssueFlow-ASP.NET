using IssueFlow.Constants;
using IssueFlow.Data;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IssueFlow.Models;

namespace IssueFlow.Services
{
    public class ProjectPermissionService : IProjectPermissionService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        private static readonly Dictionary<string, string[]> Transitions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Backlog"] = new[] { "ToDo" },
            ["ToDo"] = new[] { "InProgress", "Backlog" },
            ["InProgress"] = new[] { "CodeReview", "ReadyForTest" },
            ["CodeReview"] = new[] { "ReadyForTest", "InProgress" },
            ["ReadyForTest"] = new[] { "Testing", "InProgress" },
            ["Testing"] = new[] { "Done", "Reopened" },
            ["Reopened"] = new[] { "InProgress" },
            ["Done"] = new[] { "Reopened" },
            // legacy statuses still used in older data
            ["Open"] = new[] { "ToDo", "InProgress" },
            ["Resolved"] = new[] { "ReadyForTest", "Testing", "Done" },
            ["ReadyToLaunch"] = new[] { "Done" },
            ["Closed"] = new[] { "Reopened" }
        };

        public ProjectPermissionService(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<bool> IsMemberAsync(int projectId, string userId)
        {
            if (await IsGlobalAdmin(userId)) return true;
            return await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        }

        public async Task<string?> GetProjectRoleAsync(int projectId, string userId)
        {
            if (await IsGlobalAdmin(userId)) return "Admin";
            var m = await _db.ProjectMembers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId);
            return m?.Role;
        }

        public async Task<bool> CanManageMembersAsync(int projectId, string userId)
        {
            var role = await GetProjectRoleAsync(projectId, userId);
            return role is "Admin" or "ProjectManager";
        }

        public async Task<bool> CanAssignAsync(int projectId, string userId)
            => await CanManageMembersAsync(projectId, userId);

        public async Task<bool> IsProjectManagerAsync(int projectId, string userId)
        {
            var role = await GetProjectRoleAsync(projectId, userId);
            return role is "Admin" or "ProjectManager";
        }

        public async Task<IReadOnlyList<string>> AllowedTransitionsAsync(
            int projectId, string userId, string currentStatus, bool isAssignee)
        {
            if (!Transitions.TryGetValue(currentStatus ?? "", out var next))
                return Array.Empty<string>();

            var role = await GetProjectRoleAsync(projectId, userId) ?? "";
            var allowed = new List<string>();

            foreach (var n in next)
            {
                if (role is "Admin" or "ProjectManager")
                {
                    allowed.Add(n);
                    continue;
                }
                // Developer: progress work
                if (role == "Developer" && isAssignee &&
                    n is "InProgress" or "CodeReview" or "ReadyForTest")
                    allowed.Add(n);
                // QA: testing outcomes
                if (role == "QATester" &&
                    n is "Testing" or "Done" or "Reopened")
                    allowed.Add(n);
            }
            return allowed.Distinct().ToList();
        }

        private async Task<bool> IsGlobalAdmin(string userId)
        {
            var u = await _users.FindByIdAsync(userId);
            return u != null && await _users.IsInRoleAsync(u, "Admin");
        }
    }
}
