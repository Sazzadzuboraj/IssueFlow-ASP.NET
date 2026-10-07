using IssueFlow.Constants;
using IssueFlow.DTOs;
using IssueFlow.Models;
using IssueFlow.Models.ViewModels;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IssueFlow.Controllers
{
    /// <summary>
    /// HTTP only — all data access and business rules go through services.
    /// </summary>
    [Authorize]
    public class IssueController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIssueService _issueService;
        private readonly IProjectService _projectService;
        private readonly IActivityLogService _activityLog;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IProjectPermissionService _perm;
        private readonly IProjectFileService _projectFiles;

        public IssueController(
            UserManager<ApplicationUser> userManager,
            IIssueService issueService,
            IProjectService projectService,
            IActivityLogService activityLog,
            ISubscriptionService subscriptionService,
            IProjectPermissionService perm,
            IProjectFileService projectFiles)
        {
            _userManager = userManager;
            _issueService = issueService;
            _projectService = projectService;
            _activityLog = activityLog;
            _subscriptionService = subscriptionService;
            _perm = perm;
            _projectFiles = projectFiles;
        }

        public async Task<IActionResult> Index(string search, string status, string priority, int? projectId, string? deadline)
        {
            var issues = await _issueService.GetIssuesFilteredAsync(search, status, priority, projectId, deadline);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.ProjectId = projectId;
            ViewBag.Deadline = deadline;
            ViewBag.StatusList = new SelectList(IssueStatuses.All, status);
            ViewBag.PriorityList = new SelectList(Priorities.All, priority);
            ViewBag.ProjectList = await BuildProjectSelectListAsync(projectId);

            return View(issues);
        }

        [HttpGet]
        public async Task<IActionResult> SearchResults(string search, string status, string priority, int? projectId, string? deadline)
        {
            var list = await _issueService.GetIssuesFilteredAsync(search, status, priority, projectId, deadline, take: 200);
            return PartialView("_IssueRows", list);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var issue = await _issueService.GetIssueDetailsAsync(id.Value);
            if (issue == null) return NotFound();

            ViewBag.ActivityLog = await _activityLog.GetForIssueAsync(id.Value);
            ViewBag.FileTypeList = new SelectList(IssueAttachmentTypes.All);
            return View(issue);
        }

        /// <summary>
        /// Developer (or privileged role) uploads source files into the project folder,
        /// organized by type: Source / Document / Config / Test / Other.
        /// Reviewers see these files on the same Details page when the issue is in review.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(25_000_000)]
        public async Task<IActionResult> UploadSourceFiles(int issueId, string fileType, List<IFormFile>? files)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var issue = await _issueService.GetByIdAsync(issueId);
            if (issue == null) return NotFound();

            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            var isAssigned = await _issueService.IsAssignedAsync(issueId, user.Id);
            if (!isPrivileged && !isAssigned)
            {
                TempData["Error"] = "Only the assigned developer (or a manager) can upload source files.";
                return RedirectToAction(nameof(Details), new { id = issueId });
            }

            var (ok, error, saved) = await _projectFiles.UploadIssueFilesAsync(
                issueId, user.Id, fileType ?? IssueAttachmentTypes.Source, files ?? new List<IFormFile>());

            TempData[ok ? "Success" : "Error"] = ok
                ? $"{saved.Count} file(s) uploaded to project folder."
                : (error ?? "Upload failed.");

            return RedirectToAction(nameof(Details), new { id = issueId });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var att = await _projectFiles.GetAttachmentByIdAsync(id);
            if (att == null) return NotFound();

            var physical = _projectFiles.GetPhysicalPath(att.FilePath);
            if (physical == null || !System.IO.File.Exists(physical))
                return NotFound();

            var contentType = string.IsNullOrWhiteSpace(att.ContentType)
                ? "application/octet-stream"
                : att.ContentType;
            return PhysicalFile(physical, contentType, att.FileName);
        }

        /// <summary>
        /// Download all source files for an issue as a single ZIP
        /// (optional filter by FileType: Source, Document, …).
        /// Reviewers use this when the issue is in Code Review / Test.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadIssueZip(int issueId, string? fileType = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var issue = await _issueService.GetByIdAsync(issueId);
            if (issue == null) return NotFound();

            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            var isQa = User.IsInRole("QATester");
            var isAssigned = await _issueService.IsAssignedAsync(issueId, user.Id);
            if (!isPrivileged && !isQa && !isAssigned)
            {
                TempData["Error"] = "You do not have permission to download these files.";
                return RedirectToAction(nameof(Details), new { id = issueId });
            }

            var (ok, error, stream, fileName) = await _projectFiles.BuildIssueZipAsync(issueId, fileType);
            if (!ok || stream == null)
            {
                TempData["Error"] = error ?? "Could not build ZIP.";
                return RedirectToAction(nameof(Details), new { id = issueId });
            }

            // File() disposes the stream after the response is sent
            return File(stream, "application/zip", fileName ?? $"Issue{issueId}_files.zip");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int issueId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            var (ok, error) = await _projectFiles.DeleteAttachmentAsync(id, user.Id, isPrivileged);
            TempData[ok ? "Success" : "Error"] = ok ? "File deleted." : (error ?? "Delete failed.");
            return RedirectToAction(nameof(Details), new { id = issueId });
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Create()
        {
            ViewBag.ProjectId = await BuildProjectSelectListAsync(null);
            return View(new CreateIssueDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Create(CreateIssueDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ProjectId = await BuildProjectSelectListAsync(dto.ProjectId);
                return View(dto);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!User.IsInRole("Admin"))
            {
                var (allowed, message) = await _subscriptionService.CanCreateIssueAsync(user.Id);
                if (!allowed)
                {
                    TempData["Error"] = message;
                    return RedirectToAction(nameof(Index));
                }
            }

            await _issueService.CreateIssueAsync(dto, user.Id);
            TempData["Success"] = "Issue created successfully!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var issue = await _issueService.GetByIdAsync(id.Value);
            if (issue == null) return NotFound();

            var dto = new UpdateIssueDto
            {
                Id = issue.Id,
                Title = issue.Title,
                Description = issue.Description,
                Status = issue.Status,
                Priority = issue.Priority,
                ProjectId = issue.ProjectId,
                Deadline = issue.Deadline
            };

            ViewBag.ProjectId = await BuildProjectSelectListAsync(issue.ProjectId);
            ViewBag.StatusList = new SelectList(IssueStatuses.All, issue.Status);
            ViewBag.PriorityList = new SelectList(Priorities.All, issue.Priority);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Edit(int id, UpdateIssueDto dto)
        {
            if (id != dto.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectId = await BuildProjectSelectListAsync(dto.ProjectId);
                ViewBag.StatusList = new SelectList(IssueStatuses.All, dto.Status);
                ViewBag.PriorityList = new SelectList(Priorities.All, dto.Priority);
                return View(dto);
            }

            var ok = await _issueService.UpdateIssueAsync(dto);
            if (!ok) return NotFound();

            TempData["Success"] = "Issue updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var issue = await _issueService.GetIssueDetailsAsync(id.Value);
            if (issue == null) return NotFound();
            return View(issue);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _issueService.DeleteIssueAsync(id);
            TempData["Success"] = "Issue deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Assign(int? id)
        {
            if (id == null) return NotFound();
            var issue = await _issueService.GetByIdAsync(id.Value);
            if (issue == null) return NotFound();

            var developers = await _userManager.GetUsersInRoleAsync("Developer");
            var model = new AssignIssueViewModel
            {
                IssueId = issue.Id,
                IssueTitle = issue.Title,
                Developers = developers.Select(d => new SelectListItem
                {
                    Value = d.Id,
                    Text = d.FullName ?? d.Email
                }).ToList()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Assign(AssignIssueViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var developers = await _userManager.GetUsersInRoleAsync("Developer");
                model.Developers = developers.Select(d => new SelectListItem
                {
                    Value = d.Id,
                    Text = d.FullName ?? d.Email
                }).ToList();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var (ok, error) = await _issueService.AssignDeveloperAsync(model.IssueId, model.UserId, user!.Id);
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Assign), new { id = model.IssueId });
            }

            TempData["Success"] = "Developer assigned successfully!";
            return RedirectToAction(nameof(Details), new { id = model.IssueId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, string status)
        {
            var issue = await _issueService.GetByIdAsync(id);
            if (issue == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var isAssigned = await _issueService.IsAssignedAsync(id, user.Id);
            var allowed = await _perm.AllowedTransitionsAsync(issue.ProjectId, user.Id, issue.Status, isAssigned);

            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            if (isPrivileged)
                allowed = allowed.Concat(new[] { status }).Distinct().ToList();

            var normalized = IssueStatuses.Normalize(status);
            if (!allowed.Contains(status) && !allowed.Contains(normalized) && !isPrivileged)
            {
                TempData["Error"] = $"Cannot move from {issue.Status} to {status} with your role.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                await _issueService.ChangeStatusAsync(id, normalized, user, isPrivileged);
                TempData["Success"] = $"Status → {normalized}";
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int issueId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id = issueId });
            }

            var user = await _userManager.GetUserAsync(User);
            await _issueService.AddCommentAsync(issueId, user!.Id, content);
            TempData["Success"] = "Comment added!";
            return RedirectToAction(nameof(Details), new { id = issueId });
        }

        [Authorize(Roles = "Developer,Admin")]
        public async Task<IActionResult> RequestExtension(int? id)
        {
            if (id == null) return NotFound();
            var issue = await _issueService.GetByIdAsync(id.Value);
            if (issue == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var isAssigned = await _issueService.IsAssignedAsync(id.Value, user!.Id);
            if (!isAssigned && !User.IsInRole("Admin"))
                return Forbid();

            return View(new RequestExtensionViewModel
            {
                IssueId = issue.Id,
                IssueTitle = issue.Title,
                CurrentDeadline = issue.Deadline,
                RequestedNewDeadline = (issue.Deadline ?? DateTime.UtcNow.Date).AddDays(3)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Developer,Admin")]
        public async Task<IActionResult> RequestExtension(RequestExtensionViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            try
            {
                var ok = await _issueService.RequestExtensionAsync(
                    model.IssueId, user!.Id, model.RequestedNewDeadline, model.Reason);
                if (!ok)
                {
                    TempData["Error"] = "Issue not found.";
                    return RedirectToAction(nameof(Index));
                }
                TempData["Success"] = "Extension request submitted — awaiting manager approval.";
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id = model.IssueId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> ReviewExtension(int id, bool approve)
        {
            var user = await _userManager.GetUserAsync(User);
            var ok = await _issueService.ReviewExtensionAsync(id, user!.Id, approve);
            TempData[ok ? "Success" : "Error"] = ok
                ? (approve ? "Extension approved — deadline updated." : "Extension rejected — original deadline stands.")
                : "No pending extension request found for this issue.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Developer,Admin")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var allowed = new[] { IssueStatuses.InProgress, IssueStatuses.CodeReview, IssueStatuses.ReadyForTest };
            status = IssueStatuses.Normalize(status);
            if (!allowed.Contains(status))
            {
                TempData["Error"] = "Invalid status. Developers can set: InProgress, CodeReview, ReadyForTest.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var issue = await _issueService.GetByIdAsync(id);
            if (issue == null) return NotFound();

            var isAssigned = await _issueService.IsAssignedAsync(id, user.Id);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && !isAssigned)
            {
                TempData["Error"] = "You are not assigned to this issue. Ask a Project Manager to assign you, or use Request this work.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                await _issueService.ChangeStatusAsync(id, status, user, isPrivilegedRole: isAdmin || isAssigned);
                TempData["Success"] = $"Status updated to {status}.";
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QATester,Admin,ProjectManager")]
        public async Task<IActionResult> MarkReadyToLaunch(int id)
        {
            var issue = await _issueService.GetByIdAsync(id);
            if (issue == null) return NotFound();

            if (issue.Status is not (IssueStatuses.ReadyForTest or IssueStatuses.Testing or IssueStatuses.CodeReview))
            {
                TempData["Error"] = "Only features in CodeReview / ReadyForTest / Testing can be marked Done.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var user = await _userManager.GetUserAsync(User);
            await _issueService.ChangeStatusAsync(id, IssueStatuses.Done, user!, isPrivilegedRole: true);
            TempData["Success"] = "Feature marked Done.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<SelectList> BuildProjectSelectListAsync(int? selectedId)
        {
            var projects = await _projectService.GetActiveProjectsAsync();
            return new SelectList(projects.OrderBy(p => p.Name), "Id", "Name", selectedId);
        }
    }
}
