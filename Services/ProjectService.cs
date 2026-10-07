using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _uow;
        private readonly IProjectFileService _projectFiles;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(IUnitOfWork uow, IProjectFileService projectFiles, ILogger<ProjectService> logger)
        {
            _uow = uow;
            _projectFiles = projectFiles;
            _logger = logger;
        }

        public async Task<List<Project>> GetActiveProjectsAsync(string? search = null)
        {
            var query = _uow.Projects.Query()
                .Include(p => p.CreatedBy)
                .Include(p => p.Issues)
                .Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Description != null && p.Description.Contains(search)));
            }

            return await query.OrderByDescending(p => p.CreatedDate).ToListAsync();
        }

        public async Task<Project?> GetByIdAsync(int id)
            => await _uow.Projects.GetByIdAsync(id);

        public async Task<Project?> GetDetailsAsync(int id)
        {
            return await _uow.Projects.Query()
                .Include(p => p.CreatedBy)
                .Include(p => p.Issues!).ThenInclude(i => i.CreatedBy)
                .Include(p => p.Members!).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Project> CreateAsync(string name, string? description, string createdById)
        {
            var project = new Project
            {
                Name = name,
                Description = description,
                CreatedById = createdById,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            await _uow.Projects.AddAsync(project);
            await _uow.SaveChangesAsync();

            // Team is created with the creator as Project Leader (stored as ProjectManager for permissions).
            // Board is not a separate entity — it is the Kanban view of this project's issues (/Board/Project/{id}).
            await _uow.ProjectMembers.AddAsync(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = createdById,
                Role = "ProjectManager", // Project Leader
                JoinedDate = DateTime.UtcNow
            });
            await _uow.SaveChangesAsync();

            // Physical project folder: wwwroot/uploads/projects/{Id}_{Name}/
            try
            {
                await _projectFiles.EnsureProjectFolderAsync(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create folder for project {ProjectId}", project.Id);
            }

            _logger.LogInformation("Project {ProjectId} created; leader={UserId}; folder={Folder}; team+board ready",
                project.Id, createdById, project.FolderPath);
            return project;
        }

        public async Task<bool> UpdateAsync(int id, string name, string? description)
        {
            var existing = await _uow.Projects.GetByIdAsync(id);
            if (existing == null) return false;

            existing.Name = name;
            existing.Description = description;
            _uow.Projects.Update(existing);
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var project = await _uow.Projects.GetByIdAsync(id);
            if (project == null) return false;

            project.IsActive = false;
            _uow.Projects.Update(project);
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
            => await _uow.Projects.ExistsAsync(p => p.Id == id);

        public async Task<Project?> GetWithMembersAsync(int id)
        {
            return await _uow.Projects.Query()
                .Include(p => p.Members!).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<(bool Ok, string? Error)> AddMemberAsync(int projectId, string userId, string role)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
                return (false, "User and role required.");

            var exists = await _uow.ProjectMembers.ExistsAsync(m => m.ProjectId == projectId && m.UserId == userId);
            if (exists)
                return (false, "User is already a member. Remove and re-add to change role.");

            await _uow.ProjectMembers.AddAsync(new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = role,
                JoinedDate = DateTime.UtcNow
            });
            await _uow.SaveChangesAsync();
            return (true, null);
        }

        public async Task<bool> RemoveMemberAsync(int memberId)
        {
            var members = await _uow.ProjectMembers.FindAsync(m => m.Id == memberId);
            var m = members.FirstOrDefault();
            if (m == null) return false;

            _uow.ProjectMembers.Remove(m);
            await _uow.SaveChangesAsync();
            return true;
        }
    }
}
