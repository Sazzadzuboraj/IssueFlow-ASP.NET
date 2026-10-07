using IssueFlow.Constants;
using IssueFlow.DTOs;
using IssueFlow.Models;
using IssueFlow.Models.ViewModels;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IssueFlow.Data;

namespace IssueFlow.Controllers
{
    [Authorize]
    public class BugController : Controller
    {
        private readonly IBugService _bugService;
        private readonly IActivityLogService _activityLog;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptionService;

        public BugController(
            IBugService bugService,
            IActivityLogService activityLog,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ISubscriptionService subscriptionService)
        {
            _bugService = bugService;
            _activityLog = activityLog;
            _userManager = userManager;
            _context = context;
            _subscriptionService = subscriptionService;
        }

        // GET: Bug
        public async Task<IActionResult> Index(string search, string status, string priority, int? projectId)
        {
            var user = await _userManager.GetUserAsync(User);
            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");

            var bugs = await _bugService.GetBugsAsync(search, status, priority, user!, isPrivileged);
            if (projectId.HasValue && projectId.Value > 0)
                bugs = bugs.Where(b => b.ProjectId == projectId.Value).ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.ProjectId = projectId;
            ViewBag.StatusList = new SelectList(BugStatuses.All, status);
            ViewBag.PriorityList = new SelectList(Priorities.All, priority);
            ViewBag.ProjectList = new SelectList(
                await _context.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name", projectId);

            return View(bugs);
        }

        // GET: Bug/SearchResults — AJAX partial
        [HttpGet]
        public async Task<IActionResult> SearchResults(string search, string status, string priority, int? projectId)
        {
            var user = await _userManager.GetUserAsync(User);
            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            var bugs = await _bugService.GetBugsAsync(search, status, priority, user!, isPrivileged);
            if (projectId.HasValue && projectId.Value > 0)
                bugs = bugs.Where(b => b.ProjectId == projectId.Value).ToList();
            return PartialView("_BugRows", bugs);
        }

        // GET: Bug/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bug = await _bugService.GetBugDetailsAsync(id.Value);
            if (bug == null) return NotFound();

            ViewBag.ActivityLog = await _activityLog.GetForBugAsync(id.Value);
            return View(bug);
        }

        // GET: Bug/Report — only Testers report defects found during review
        [Authorize(Roles = "QATester")]
        public async Task<IActionResult> Report()
        {
            var model = new ReportBugViewModel
            {
                Projects = await _context.Projects
                    .Where(p => p.IsActive)
                    .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                    .ToListAsync()
            };
            return View(model);
        }

        // POST: Bug/Report
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "QATester")]
        public async Task<IActionResult> Report(ReportBugViewModel model, List<IFormFile>? attachments)
        {
            if (!ModelState.IsValid)
            {
                model.Projects = await _context.Projects
                    .Where(p => p.IsActive)
                    .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                    .ToListAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (!User.IsInRole("Admin"))
            {
                var (allowed, message) = await _subscriptionService.CanReportBugAsync(user!.Id);
                if (!allowed)
                {
                    TempData["Error"] = message;
                    return RedirectToAction(nameof(Index));
                }
            }

            var dto = new CreateBugDto
            {
                Title = model.Title,
                Description = model.Description,
                Priority = model.Priority,
                ProjectId = model.ProjectId
            };

            var bug = await _bugService.ReportBugAsync(dto, user!.Id, attachments);
            TempData["Success"] = "Bug reported successfully!";
            return RedirectToAction(nameof(Details), new { id = bug.Id });
        }

        // GET: Bug/Assign/5
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Assign(int? id)
        {
            if (id == null) return NotFound();
            var bug = await _bugService.GetBugDetailsAsync(id.Value);
            if (bug == null) return NotFound();

            var developers = await _userManager.GetUsersInRoleAsync("Developer");
            var model = new AssignBugViewModel
            {
                BugId = bug.Id,
                BugTitle = bug.Title,
                Developers = developers.Select(d => new SelectListItem
                {
                    Value = d.Id,
                    Text = d.FullName ?? d.Email
                }).ToList()
            };
            return View(model);
        }

        // POST: Bug/Assign
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Assign(AssignBugViewModel model)
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

            // Assignee must have an active subscription
            if (!await _subscriptionService.HasActiveSubscriptionAsync(model.DeveloperId))
            {
                TempData["Error"] = "Cannot assign: this developer does not have an active subscription plan.";
                return RedirectToAction(nameof(Assign), new { id = model.BugId });
            }

            var dto = new AssignBugDto { BugId = model.BugId, DeveloperId = model.DeveloperId };
            var ok = await _bugService.AssignBugAsync(dto);
            if (!ok)
            {
                TempData["Error"] = "Bug not found.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Developer assigned to bug.";
            return RedirectToAction(nameof(Details), new { id = model.BugId });
        }

        // POST: Bug/SubmitFix/5  (Developer marks fix ready for review)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Developer,Admin")]
        public async Task<IActionResult> SubmitFix(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            try
            {
                var ok = await _bugService.SubmitFixAsync(id, user!.Id);
                if (!ok)
                {
                    TempData["Error"] = "Bug not found.";
                    return RedirectToAction(nameof(Index));
                }
                TempData["Success"] = "Fix submitted — awaiting tester review.";
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Bug/Review/5
        [Authorize(Roles = "Admin,ProjectManager,QATester")]
        public async Task<IActionResult> Review(int? id)
        {
            if (id == null) return NotFound();
            var bug = await _bugService.GetBugDetailsAsync(id.Value);
            if (bug == null) return NotFound();
            if (bug.Status != "ReadyForReview")
            {
                TempData["Error"] = "This bug is not awaiting review.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(new ReviewFixViewModel
            {
                BugId = bug.Id,
                BugTitle = bug.Title,
                CurrentStatus = bug.Status
            });
        }

        // POST: Bug/Review
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager,QATester")]
        public async Task<IActionResult> Review(ReviewFixViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            var ok = await _bugService.ReviewFixAsync(model.BugId, user!.Id, model.Approve, model.ReviewNotes);
            if (!ok)
            {
                TempData["Error"] = "Bug not found.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = model.Approve
                ? "Bug verified and closed."
                : "Bug reopened — developer will be notified.";
            return RedirectToAction(nameof(Details), new { id = model.BugId });
        }
    }
}
