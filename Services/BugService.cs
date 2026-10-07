using IssueFlow.Constants;
using IssueFlow.DTOs;
using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    /// <summary>
    /// Owns the Bug lifecycle: Reported -> Assigned -> Fixed -> ReadyForReview
    /// -> Verified (closed) or Reopened (back to the developer). Also handles
    /// screenshot/file attachments uploaded as proof when a bug is reported.
    /// </summary>
    public class BugService : IBugService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActivityLogService _activityLog;
        private readonly IRatingService _ratingService;
        private readonly INotificationService _notifications;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BugService> _logger;

        // Keep uploads small and image-only — this is a bug-proof screenshot feature, not general file storage.
        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public BugService(
            IUnitOfWork unitOfWork,
            IActivityLogService activityLog,
            IRatingService ratingService,
            INotificationService notifications,
            IWebHostEnvironment env,
            ILogger<BugService> logger)
        {
            _unitOfWork = unitOfWork;
            _activityLog = activityLog;
            _ratingService = ratingService;
            _notifications = notifications;
            _env = env;
            _logger = logger;
        }

        public async Task<List<Bug>> GetBugsAsync(string? search, string? status, string? priority, ApplicationUser currentUser, bool isPrivilegedRole)
        {
            var query = _unitOfWork.Bugs.Query()
                .Include(b => b.Project)
                .Include(b => b.ReportedBy)
                .Include(b => b.AssignedDeveloper)
                .Include(b => b.Attachments)
                .AsQueryable();

            // Developers see bugs assigned to them; Testers see bugs they reported; Admin/PM see all.
            if (!isPrivilegedRole)
            {
                query = query.Where(b => b.AssignedDeveloperId == currentUser.Id || b.ReportedById == currentUser.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b => b.Title.Contains(search) ||
                                          (b.Description != null && b.Description.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(b => b.Status == status);

            if (!string.IsNullOrWhiteSpace(priority))
                query = query.Where(b => b.Priority == priority);

            // Highest priority first, as requested — Critical bugs surface at the top.
            return await query
                .OrderBy(b => b.Priority == "Critical" ? 0 : b.Priority == "High" ? 1 : b.Priority == "Medium" ? 2 : 3)
                .ThenByDescending(b => b.CreatedDate)
                .ToListAsync();
        }

        public async Task<Bug?> GetBugDetailsAsync(int id)
        {
            return await _unitOfWork.Bugs.Query()
                .Include(b => b.Project)
                .Include(b => b.ReportedBy)
                .Include(b => b.AssignedDeveloper)
                .Include(b => b.Attachments!)
                    .ThenInclude(a => a.UploadedBy)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<Bug> ReportBugAsync(CreateBugDto dto, string reportedById, List<IFormFile>? attachments)
        {
            var bug = new Bug
            {
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                ProjectId = dto.ProjectId,
                ReportedById = reportedById,
                Status = BugStatuses.Reported,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Bugs.AddAsync(bug);
            await _unitOfWork.SaveChangesAsync(); // need bug.Id before saving attachments

            if (attachments != null && attachments.Count > 0)
            {
                await SaveAttachmentsAsync(bug.Id, reportedById, attachments);
            }

            await _activityLog.LogAsync(reportedById, "BugReported", $"Bug '{bug.Title}' reported.", bugId: bug.Id);
            _logger.LogInformation("Bug {BugId} reported by {UserId}", bug.Id, reportedById);
            return bug;
        }

        private async Task SaveAttachmentsAsync(int bugId, string uploaderId, List<IFormFile> files)
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "bugs", bugId.ToString());
            Directory.CreateDirectory(folder);

            foreach (var file in files)
            {
                if (file.Length == 0 || file.Length > MaxFileSizeBytes) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext)) continue; // silently skip disallowed types

                var safeName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(folder, safeName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var attachment = new BugAttachment
                {
                    BugId = bugId,
                    FileName = file.FileName,
                    FilePath = $"/uploads/bugs/{bugId}/{safeName}",
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length,
                    UploadedById = uploaderId,
                    UploadedDate = DateTime.UtcNow
                };
                await _unitOfWork.BugAttachments.AddAsync(attachment);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> AssignBugAsync(AssignBugDto dto)
        {
            var bug = await _unitOfWork.Bugs.GetByIdAsync(dto.BugId);
            if (bug == null) return false;

            bug.AssignedDeveloperId = dto.DeveloperId;
            bug.Status = BugStatuses.Assigned;
            bug.UpdatedDate = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            await _activityLog.LogAsync(dto.DeveloperId, "BugAssigned", "Bug assigned to developer.", bugId: bug.Id);

            await _notifications.NotifyAsync(
                dto.DeveloperId,
                "Bug assigned to you",
                $"You have been assigned bug #{bug.Id}: {bug.Title}",
                "BugAssigned",
                $"/Bug/Details/{bug.Id}");

            return true;
        }

        public async Task<bool> SubmitFixAsync(int bugId, string developerId)
        {
            var bug = await _unitOfWork.Bugs.GetByIdAsync(bugId);
            if (bug == null) return false;

            if (bug.AssignedDeveloperId != developerId)
                throw new UnauthorizedAccessException("Only the assigned developer can submit this fix.");

            bug.Status = BugStatuses.ReadyForReview;
            bug.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            await _activityLog.LogAsync(developerId, "BugFixSubmitted", "Fix submitted, awaiting tester review.", bugId: bug.Id);

            // Notify the tester who reported it that a fix is ready
            if (!string.IsNullOrEmpty(bug.ReportedById))
            {
                await _notifications.NotifyAsync(
                    bug.ReportedById,
                    "Bug fix ready for review",
                    $"A fix was submitted for bug #{bug.Id}: {bug.Title}",
                    "BugReadyForReview",
                    $"/Bug/Details/{bug.Id}");
            }

            return true;
        }

        public async Task<bool> ReviewFixAsync(int bugId, string testerId, bool approve, string? reviewNotes)
        {
            var bug = await _unitOfWork.Bugs.GetByIdAsync(bugId);
            if (bug == null) return false;

            bug.ReviewNotes = reviewNotes;
            bug.UpdatedDate = DateTime.UtcNow;

            if (approve)
            {
                bug.Status = BugStatuses.Verified;
                bug.VerifiedDate = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();

                await _activityLog.LogAsync(testerId, "BugVerified", reviewNotes, bugId: bug.Id);

                if (!string.IsNullOrEmpty(bug.AssignedDeveloperId))
                {
                    await _ratingService.RateBugResolutionAsync(bug, bug.AssignedDeveloperId, verified: true);
                    await _notifications.NotifyAsync(
                        bug.AssignedDeveloperId,
                        "Bug verified",
                        $"Your fix for bug #{bug.Id} was verified.",
                        "BugVerified",
                        $"/Bug/Details/{bug.Id}");
                }

                if (!string.IsNullOrEmpty(bug.ReportedById))
                {
                    await _ratingService.RateBugReportAsync(bug, bug.ReportedById);
                }
            }
            else
            {
                bug.Status = BugStatuses.Reopened;
                await _unitOfWork.SaveChangesAsync();

                await _activityLog.LogAsync(testerId, "BugReopened", reviewNotes, bugId: bug.Id);

                if (!string.IsNullOrEmpty(bug.AssignedDeveloperId))
                {
                    await _ratingService.RateBugResolutionAsync(bug, bug.AssignedDeveloperId, verified: false);
                    await _notifications.NotifyAsync(
                        bug.AssignedDeveloperId,
                        "Bug reopened",
                        $"Bug #{bug.Id} was reopened. Notes: {reviewNotes}",
                        "BugReopened",
                        $"/Bug/Details/{bug.Id}");
                }
            }

            return true;
        }
    }
}
