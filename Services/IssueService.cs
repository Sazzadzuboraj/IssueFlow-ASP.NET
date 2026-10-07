using IssueFlow.Constants;
using IssueFlow.DTOs;
using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    /// <summary>
    /// Owns all Issue business rules. Controllers only translate HTTP ↔ this service.
    /// </summary>
    public class IssueService : IIssueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActivityLogService _activityLog;
        private readonly IRatingService _ratingService;
        private readonly INotificationService _notifications;
        private readonly ISubscriptionService _subscriptions;
        private readonly ILogger<IssueService> _logger;

        public IssueService(
            IUnitOfWork unitOfWork,
            IActivityLogService activityLog,
            IRatingService ratingService,
            INotificationService notifications,
            ISubscriptionService subscriptions,
            ILogger<IssueService> logger)
        {
            _unitOfWork = unitOfWork;
            _activityLog = activityLog;
            _ratingService = ratingService;
            _notifications = notifications;
            _subscriptions = subscriptions;
            _logger = logger;
        }

        public async Task<List<Issue>> GetIssuesAsync(
            string? search, string? status, string? priority,
            ApplicationUser currentUser, bool isPrivilegedRole)
        {
            var query = BaseIssueQuery();

            if (!isPrivilegedRole)
                query = query.Where(i => i.Assignments!.Any(a => a.UserId == currentUser.Id));

            query = ApplySearchStatusPriority(query, search, status, priority);

            return await query
                .OrderBy(i => i.Priority == "Critical" ? 0 : i.Priority == "High" ? 1 : i.Priority == "Medium" ? 2 : 3)
                .ThenByDescending(i => i.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Issue>> GetIssuesFilteredAsync(
            string? search, string? status, string? priority, int? projectId, string? deadlineFilter,
            int? take = null)
        {
            var query = BaseIssueQuery();
            query = ApplySearchStatusPriority(query, search, status, priority);

            if (projectId.HasValue && projectId.Value > 0)
                query = query.Where(i => i.ProjectId == projectId.Value);

            if (!string.IsNullOrWhiteSpace(deadlineFilter))
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);
                var weekEnd = today.AddDays(8);
                query = deadlineFilter switch
                {
                    "overdue" => query.Where(i => i.Deadline != null && i.Deadline < today),
                    "today" => query.Where(i => i.Deadline != null && i.Deadline >= today && i.Deadline < tomorrow),
                    "week" => query.Where(i => i.Deadline != null && i.Deadline >= today && i.Deadline < weekEnd),
                    "none" => query.Where(i => i.Deadline == null),
                    _ => query
                };
            }

            query = query
                .OrderBy(i => i.Priority == "Critical" ? 0 : i.Priority == "High" ? 1 : i.Priority == "Medium" ? 2 : 3)
                .ThenByDescending(i => i.CreatedDate);

            if (take.HasValue)
                query = query.Take(take.Value);

            return await query.ToListAsync();
        }

        public async Task<Issue?> GetIssueDetailsAsync(int id)
        {
            return await _unitOfWork.Issues.Query()
                .Include(i => i.Project)
                .Include(i => i.CreatedBy)
                .Include(i => i.Assignments!).ThenInclude(a => a.User)
                .Include(i => i.Comments!).ThenInclude(c => c.User)
                .Include(i => i.Attachments!).ThenInclude(a => a.UploadedBy)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Issue?> GetByIdAsync(int id)
            => await _unitOfWork.Issues.GetByIdAsync(id);

        public async Task<Issue> CreateIssueAsync(CreateIssueDto dto, string createdById)
        {
            var issue = new Issue
            {
                Title = dto.Title,
                Description = dto.Description,
                Priority = string.IsNullOrWhiteSpace(dto.Priority) ? Priorities.Medium : dto.Priority,
                ProjectId = dto.ProjectId,
                CreatedById = createdById,
                CreatedDate = DateTime.UtcNow,
                Status = IssueStatuses.Backlog,
                Type = string.IsNullOrWhiteSpace(dto.Type) ? IssueTypes.Task : dto.Type,
                Deadline = dto.Deadline,
                ExtensionStatus = "None"
            };

            await _unitOfWork.Issues.AddAsync(issue);
            await _unitOfWork.SaveChangesAsync();
            await _activityLog.LogAsync(createdById, "IssueCreated", $"Issue '{issue.Title}' created.", issueId: issue.Id);
            _logger.LogInformation("Issue {IssueId} created by {UserId}", issue.Id, createdById);
            return issue;
        }

        public async Task<bool> UpdateIssueAsync(UpdateIssueDto dto)
        {
            var existing = await _unitOfWork.Issues.GetByIdAsync(dto.Id);
            if (existing == null) return false;

            existing.Title = dto.Title;
            existing.Description = dto.Description;
            existing.Status = IssueStatuses.Normalize(dto.Status);
            existing.Priority = dto.Priority;
            existing.ProjectId = dto.ProjectId;
            existing.Deadline = dto.Deadline;
            existing.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Issues.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteIssueAsync(int id)
        {
            var issue = await _unitOfWork.Issues.GetByIdAsync(id);
            if (issue == null) return false;

            _unitOfWork.Issues.Remove(issue);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Issue {IssueId} deleted", id);
            return true;
        }

        public async Task<bool> ChangeStatusAsync(int id, string newStatus, ApplicationUser currentUser, bool isPrivilegedRole)
        {
            var issue = await _unitOfWork.Issues.GetByIdAsync(id);
            if (issue == null) return false;

            var isAssigned = await IsAssignedAsync(id, currentUser.Id);
            if (!isPrivilegedRole && !isAssigned)
            {
                _logger.LogWarning("User {UserId} unauthorized status change on Issue {IssueId}", currentUser.Id, id);
                throw new UnauthorizedAccessException("You are not permitted to change this issue's status.");
            }

            var normalized = IssueStatuses.Normalize(newStatus);
            issue.Status = normalized;
            issue.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            await _activityLog.LogAsync(currentUser.Id, "StatusChanged", $"Status changed to {normalized}.", issueId: id);

            if (normalized == IssueStatuses.Done)
            {
                var assignees = await _unitOfWork.IssueAssignments.FindAsync(a => a.IssueId == id);
                foreach (var assignment in assignees)
                    await _ratingService.RateIssueCompletionAsync(issue, assignment.UserId);
            }

            return true;
        }

        public async Task<Comment> AddCommentAsync(int issueId, string userId, string content)
        {
            var comment = new Comment
            {
                IssueId = issueId,
                UserId = userId,
                Content = content,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Comments.AddAsync(comment);
            await _unitOfWork.SaveChangesAsync();
            await _activityLog.LogAsync(userId, "CommentAdded",
                content.Length > 100 ? content[..100] + "..." : content, issueId: issueId);
            return comment;
        }

        public async Task<bool> RequestExtensionAsync(int issueId, string developerId, DateTime requestedDeadline, string reason)
        {
            var issue = await _unitOfWork.Issues.GetByIdAsync(issueId);
            if (issue == null) return false;

            if (!await IsAssignedAsync(issueId, developerId))
                throw new UnauthorizedAccessException("Only a developer assigned to this issue can request a deadline extension.");

            issue.ExtensionRequested = true;
            issue.RequestedNewDeadline = requestedDeadline;
            issue.ExtensionReason = reason;
            issue.ExtensionStatus = "Pending";

            await _unitOfWork.SaveChangesAsync();
            await _activityLog.LogAsync(developerId, "ExtensionRequested",
                $"Requested new deadline {requestedDeadline:yyyy-MM-dd}: {reason}", issueId: issueId);

            if (!string.IsNullOrEmpty(issue.CreatedById) && issue.CreatedById != developerId)
            {
                await _notifications.NotifyAsync(
                    issue.CreatedById,
                    "Deadline extension requested",
                    $"Extension requested for issue #{issue.Id}: {issue.Title}",
                    "ExtensionRequested",
                    $"/Issue/Details/{issue.Id}");
            }

            return true;
        }

        public async Task<bool> ReviewExtensionAsync(int issueId, string managerId, bool approve)
        {
            var issue = await _unitOfWork.Issues.GetByIdAsync(issueId);
            if (issue == null || issue.ExtensionStatus != "Pending") return false;

            if (approve && issue.RequestedNewDeadline.HasValue)
            {
                issue.Deadline = issue.RequestedNewDeadline;
                issue.ExtensionStatus = "Approved";
            }
            else
            {
                issue.ExtensionStatus = "Rejected";
            }
            issue.ExtensionRequested = false;

            await _unitOfWork.SaveChangesAsync();
            await _activityLog.LogAsync(managerId, approve ? "ExtensionApproved" : "ExtensionRejected",
                approve
                    ? $"New deadline approved: {issue.Deadline:yyyy-MM-dd}."
                    : "Extension rejected — original deadline stands.",
                issueId: issueId);

            var assignees = await _unitOfWork.IssueAssignments.FindAsync(a => a.IssueId == issueId);
            foreach (var a in assignees)
            {
                await _notifications.NotifyAsync(
                    a.UserId,
                    approve ? "Extension approved" : "Extension rejected",
                    approve
                        ? $"New deadline for issue #{issue.Id}: {issue.Deadline:yyyy-MM-dd}"
                        : $"Extension rejected for issue #{issue.Id}. Original deadline still applies.",
                    approve ? "ExtensionApproved" : "ExtensionRejected",
                    $"/Issue/Details/{issue.Id}");
            }

            return true;
        }

        public async Task<bool> IsAssignedAsync(int issueId, string userId)
            => await _unitOfWork.IssueAssignments.ExistsAsync(a => a.IssueId == issueId && a.UserId == userId);

        public async Task<(bool Ok, string? Error)> AssignDeveloperAsync(int issueId, string developerId, string assignedByUserId)
        {
            if (!await _subscriptions.HasActiveSubscriptionAsync(developerId))
                return (false, "Cannot assign: this developer does not have an active subscription plan.");

            if (await IsAssignedAsync(issueId, developerId))
                return (false, "This developer is already assigned to the issue.");

            var issue = await _unitOfWork.Issues.GetByIdAsync(issueId);
            if (issue == null)
                return (false, "Issue not found.");

            await _unitOfWork.IssueAssignments.AddAsync(new IssueAssignment
            {
                IssueId = issueId,
                UserId = developerId,
                AssignedDate = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync();

            await _notifications.NotifyAsync(
                developerId,
                "Issue assigned to you",
                $"You have been assigned issue #{issueId}: {issue.Title}",
                "IssueAssigned",
                $"/Issue/Details/{issueId}");

            await _activityLog.LogAsync(assignedByUserId, "IssueAssigned",
                $"Assigned to user {developerId}.", issueId: issueId);

            return (true, null);
        }

        public IssueDto ToDto(Issue issue) => new()
        {
            Id = issue.Id,
            Title = issue.Title,
            Description = issue.Description,
            Status = issue.Status,
            Priority = issue.Priority,
            ProjectId = issue.ProjectId,
            ProjectName = issue.Project?.Name,
            CreatedById = issue.CreatedById,
            CreatedByName = issue.CreatedBy?.FullName ?? issue.CreatedBy?.Email,
            CreatedDate = issue.CreatedDate,
            UpdatedDate = issue.UpdatedDate,
            AssignedDeveloperNames = issue.Assignments?
                .Where(a => a.User != null)
                .Select(a => a.User!.FullName ?? a.User!.Email ?? "Unknown")
                .ToList() ?? new List<string>()
        };

        private IQueryable<Issue> BaseIssueQuery()
            => _unitOfWork.Issues.Query()
                .Include(i => i.Project)
                .Include(i => i.CreatedBy)
                .Include(i => i.Assignments!).ThenInclude(a => a.User);

        private static IQueryable<Issue> ApplySearchStatusPriority(
            IQueryable<Issue> query, string? search, string? status, string? priority)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(i =>
                    i.Title.Contains(search) ||
                    (i.Description != null && i.Description.Contains(search)));
            }
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.Status == status);
            if (!string.IsNullOrWhiteSpace(priority))
                query = query.Where(i => i.Priority == priority);
            return query;
        }
    }
}
