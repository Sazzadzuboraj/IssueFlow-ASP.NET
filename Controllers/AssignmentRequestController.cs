using IssueFlow.Constants;
using IssueFlow.Data;
using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Controllers
{
    /// <summary>
    /// Developer: request work / accept-reject offers.
    /// QA: request review of completed issues.
    /// PM/Admin: approve, reject, offer alternative, or directly offer work/review.
    /// </summary>
    [Authorize]
    public class AssignmentRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISubscriptionService _subscriptionService;
        private readonly INotificationService? _notifications;

        public AssignmentRequestController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ISubscriptionService subscriptionService,
            IServiceProvider sp)
        {
            _db = db;
            _userManager = userManager;
            _subscriptionService = subscriptionService;
            _notifications = sp.GetService<INotificationService>();
        }

        // ========== Browse open work (Developer) / completed for review (QA) ==========

        [Authorize(Roles = "Developer,QATester,Admin")]
        public async Task<IActionResult> Available(string? kind, string? search, string? priority)
        {
            kind = string.IsNullOrWhiteSpace(kind)
                ? (User.IsInRole("QATester") && !User.IsInRole("Developer") ? "Review" : "Work")
                : kind;

            ViewBag.Kind = kind;
            ViewBag.Search = search;
            ViewBag.Priority = priority;

            if (kind == "Review")
            {
                // Completed features ready for QA
                var issues = _db.Issues.Include(i => i.Project)
                    .Where(i => i.Status == IssueStatuses.ReadyForTest || i.Status == IssueStatuses.CodeReview || i.Status == IssueStatuses.Testing);
                if (!string.IsNullOrWhiteSpace(search))
                    issues = issues.Where(i => i.Title.Contains(search) || (i.Description != null && i.Description.Contains(search)));
                if (!string.IsNullOrWhiteSpace(priority))
                    issues = issues.Where(i => i.Priority == priority);
                return View("AvailableReview", await issues.OrderByDescending(i => i.CreatedDate).Take(100).ToListAsync());
            }

            // Work: open issues + unassigned/reported bugs
            var openIssues = _db.Issues.Include(i => i.Project)
                .Where(i => i.Status == IssueStatuses.Backlog || i.Status == IssueStatuses.ToDo || i.Status == IssueStatuses.Reopened || i.Status == IssueStatuses.InProgress);
            if (!string.IsNullOrWhiteSpace(search))
                openIssues = openIssues.Where(i => i.Title.Contains(search) || (i.Description != null && i.Description.Contains(search)));
            if (!string.IsNullOrWhiteSpace(priority))
                openIssues = openIssues.Where(i => i.Priority == priority);

            ViewBag.Bugs = await _db.Bugs
                .Where(b => b.Status == BugStatuses.Reported || b.Status == BugStatuses.Reopened)
                .OrderByDescending(b => b.CreatedDate).Take(50).ToListAsync();

            return View("AvailableWork", await openIssues
                .OrderBy(i => i.Priority == "Critical" ? 0 : i.Priority == "High" ? 1 : 2)
                .ThenByDescending(i => i.CreatedDate).Take(100).ToListAsync());
        }

        // ========== Developer / QA: create request ==========

        [Authorize(Roles = "Developer,QATester")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestWork(string targetType, int targetId, string? message, string requestKind = "Work")
        {
            if (targetType is not ("Issue" or "Bug"))
            {
                TempData["Error"] = "Invalid target.";
                return RedirectToAction(nameof(Available));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!await _subscriptionService.HasActiveSubscriptionAsync(user.Id))
            {
                TempData["Error"] = "Active subscription required to request work or review.";
                return RedirectToAction("Subscribe", "Subscription");
            }

            // Role rules
            if (requestKind == "Review" && !User.IsInRole("QATester") && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "Only QA Testers can request review.";
                return RedirectToAction(nameof(Available), new { kind = "Review" });
            }
            if (requestKind == "Work" && !User.IsInRole("Developer") && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "Only Developers can request development work.";
                return RedirectToAction(nameof(Available), new { kind = "Work" });
            }

            var exists = await _db.AssignmentRequests.AnyAsync(r =>
                r.RequesterId == user.Id && r.TargetType == targetType && r.TargetId == targetId
                && (r.Status == "Pending" || r.Status == "Offered"));
            if (exists)
            {
                TempData["Error"] = "You already have a pending request/offer for this item.";
                return RedirectToAction(nameof(MyRequests));
            }

            _db.AssignmentRequests.Add(new AssignmentRequest
            {
                TargetType = targetType,
                TargetId = targetId,
                RequestKind = requestKind,
                RequesterId = user.Id,
                RequestMessage = message,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            await NotifyManagersAsync(
                $"{user.FullName} requested {requestKind.ToLower()} on {targetType} #{targetId}",
                "/AssignmentRequest/Pending");

            TempData["Success"] = "Request sent to Project Manager.";
            return RedirectToAction(nameof(MyRequests));
        }

        [Authorize(Roles = "Developer,QATester")]
        public async Task<IActionResult> MyRequests()
        {
            var user = await _userManager.GetUserAsync(User);
            var list = await _db.AssignmentRequests
                .Where(r => r.RequesterId == user!.Id || r.OfferedToUserId == user.Id)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return View(list);
        }

        // ========== PM: pending queue ==========

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Pending()
        {
            var list = await _db.AssignmentRequests
                .Include(r => r.Requester)
                .Include(r => r.OfferedToUser)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();
            return View(list);
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var req = await _db.AssignmentRequests.Include(r => r.Requester)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (req == null || req.Status != "Pending") return NotFound();

            if (!await _subscriptionService.HasActiveSubscriptionAsync(req.RequesterId))
            {
                TempData["Error"] = "Cannot approve: user has no active subscription.";
                return RedirectToAction(nameof(Pending));
            }

            await AssignUserAsync(req.TargetType, req.TargetId, req.RequesterId, req.RequestKind);

            var reviewer = await _userManager.GetUserAsync(User);
            req.Status = "Approved";
            req.ReviewedById = reviewer!.Id;
            req.ReviewedDate = DateTime.UtcNow;
            req.ManagerResponse = "Approved — you are assigned.";
            await _db.SaveChangesAsync();

            if (_notifications != null)
                await _notifications.NotifyAsync(req.RequesterId, "Request approved",
                    $"Your {req.RequestKind} request for {req.TargetType} #{req.TargetId} was approved.",
                    "AssignmentRequest",
                    req.TargetType == "Issue" ? $"/Issue/Details/{req.TargetId}" : $"/Bug/Details/{req.TargetId}");

            TempData["Success"] = "Approved and assigned.";
            return RedirectToAction(nameof(Pending));
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? response)
        {
            var req = await _db.AssignmentRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (req == null || req.Status != "Pending") return NotFound();
            var reviewer = await _userManager.GetUserAsync(User);
            req.Status = "Rejected";
            req.ReviewedById = reviewer!.Id;
            req.ReviewedDate = DateTime.UtcNow;
            req.ManagerResponse = string.IsNullOrWhiteSpace(response) ? "Rejected." : response;
            await _db.SaveChangesAsync();

            if (_notifications != null)
                await _notifications.NotifyAsync(req.RequesterId, "Request rejected",
                    $"Your request for {req.TargetType} #{req.TargetId} was rejected.",
                    "AssignmentRequest", "/AssignmentRequest/MyRequests");

            TempData["Success"] = "Request rejected.";
            return RedirectToAction(nameof(Pending));
        }

        /// <summary>PM offers a different task instead of requested one.</summary>
        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OfferAlternative(int id, string alternativeType, int alternativeId, string? response)
        {
            var req = await _db.AssignmentRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (req == null || req.Status != "Pending") return NotFound();
            if (alternativeType is not ("Issue" or "Bug"))
            {
                TempData["Error"] = "Invalid alternative.";
                return RedirectToAction(nameof(Pending));
            }

            var reviewer = await _userManager.GetUserAsync(User);
            req.Status = "Offered";
            req.ReviewedById = reviewer!.Id;
            req.ReviewedDate = DateTime.UtcNow;
            req.AlternativeTargetType = alternativeType;
            req.AlternativeTargetId = alternativeId;
            req.OfferedToUserId = req.RequesterId;
            req.ManagerResponse = response ?? $"Offered {alternativeType} #{alternativeId} instead.";
            await _db.SaveChangesAsync();

            if (_notifications != null)
                await _notifications.NotifyAsync(req.RequesterId, "Alternative task offered",
                    req.ManagerResponse, "AssignmentRequest", "/AssignmentRequest/MyRequests");

            TempData["Success"] = "Alternative offered.";
            return RedirectToAction(nameof(Pending));
        }

        /// <summary>PM directly offers Issue/Bug work or review to a user (no prior request).</summary>
        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpGet]
        public async Task<IActionResult> DirectOffer(string? targetType, int? targetId, string? kind)
        {
            ViewBag.TargetType = targetType ?? "Issue";
            ViewBag.TargetId = targetId;
            ViewBag.Kind = kind ?? "Work";

            var devs = await _userManager.GetUsersInRoleAsync("Developer");
            var qas = await _userManager.GetUsersInRoleAsync("QATester");
            ViewBag.Developers = new SelectList(devs.Select(d => new { d.Id, Name = d.FullName ?? d.Email }), "Id", "Name");
            ViewBag.Testers = new SelectList(qas.Select(d => new { d.Id, Name = d.FullName ?? d.Email }), "Id", "Name");
            return View();
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DirectOffer(string targetType, int targetId, string requestKind, string userId, string? message)
        {
            if (targetType is not ("Issue" or "Bug") || string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Invalid offer.";
                return RedirectToAction(nameof(DirectOffer));
            }

            if (!await _subscriptionService.HasActiveSubscriptionAsync(userId))
            {
                TempData["Error"] = "Cannot offer: user has no active subscription.";
                return RedirectToAction(nameof(DirectOffer), new { targetType, targetId, kind = requestKind });
            }

            var pm = await _userManager.GetUserAsync(User);
            var req = new AssignmentRequest
            {
                TargetType = targetType,
                TargetId = targetId,
                RequestKind = requestKind,
                RequesterId = userId, // treated as the offered person for history
                OfferedToUserId = userId,
                Status = "Offered",
                RequestMessage = message,
                ManagerResponse = message ?? $"Direct offer for {requestKind}",
                ReviewedById = pm!.Id,
                ReviewedDate = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow
            };
            _db.AssignmentRequests.Add(req);
            await _db.SaveChangesAsync();

            if (_notifications != null)
                await _notifications.NotifyAsync(userId, "Work offer from Manager",
                    $"You were offered {requestKind} on {targetType} #{targetId}. Accept or reject in My requests.",
                    "AssignmentRequest", "/AssignmentRequest/MyRequests");

            TempData["Success"] = "Offer sent to user.";
            return RedirectToAction(nameof(Pending));
        }

        /// <summary>Developer/QA accepts an offer (alternative or direct).</summary>
        [Authorize(Roles = "Developer,QATester")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOffer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (!await _subscriptionService.HasActiveSubscriptionAsync(user.Id))
            {
                TempData["Error"] = "Subscription required.";
                return RedirectToAction("Subscribe", "Subscription");
            }

            var req = await _db.AssignmentRequests.FirstOrDefaultAsync(r =>
                r.Id == id && r.Status == "Offered" &&
                (r.OfferedToUserId == user.Id || r.RequesterId == user.Id));
            if (req == null) return NotFound();

            var type = req.AlternativeTargetType ?? req.TargetType;
            var tid = req.AlternativeTargetId ?? req.TargetId;

            await AssignUserAsync(type, tid, user.Id, req.RequestKind);

            req.Status = "Approved";
            req.TargetType = type;
            req.TargetId = tid;
            req.ManagerResponse = (req.ManagerResponse ?? "") + " (Accepted)";
            req.ReviewedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Offer accepted — you are assigned.";
            return RedirectToAction(nameof(MyRequests));
        }

        [Authorize(Roles = "Developer,QATester")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectOffer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var req = await _db.AssignmentRequests.FirstOrDefaultAsync(r =>
                r.Id == id && r.Status == "Offered" &&
                (r.OfferedToUserId == user!.Id || r.RequesterId == user.Id));
            if (req == null) return NotFound();

            req.Status = "OfferRejected";
            req.ManagerResponse = (req.ManagerResponse ?? "") + " (Rejected by user)";
            req.ReviewedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Offer declined.";
            return RedirectToAction(nameof(MyRequests));
        }

        // ========== helpers ==========

        private async Task AssignUserAsync(string targetType, int targetId, string userId, string kind)
        {
            if (targetType == "Issue")
            {
                if (kind == "Work")
                {
                    if (!await _db.IssueAssignments.AnyAsync(a => a.IssueId == targetId && a.UserId == userId))
                    {
                        _db.IssueAssignments.Add(new IssueAssignment
                        {
                            IssueId = targetId,
                            UserId = userId,
                            AssignedDate = DateTime.UtcNow
                        });
                    }
                    var issue = await _db.Issues.FindAsync(targetId);
                    if (issue != null && (issue.Status == IssueStatuses.Backlog || issue.Status == IssueStatuses.ToDo || issue.Status == IssueStatuses.Reopened))
                        issue.Status = IssueStatuses.InProgress;
                }
                else if (kind == "Review")
                {
                    // Mark that QA is reviewing — status stays Resolved until they set ReadyToLaunch
                    var issue = await _db.Issues.FindAsync(targetId);
                    // optional: could track QA assignment in a field; for now just status note via activity not required
                }
            }
            else if (targetType == "Bug")
            {
                var bug = await _db.Bugs.FindAsync(targetId);
                if (bug != null)
                {
                    bug.AssignedDeveloperId = userId;
                    if (bug.Status is BugStatuses.Reported or BugStatuses.Reopened)
                        bug.Status = BugStatuses.Assigned;
                }
            }
        }

        private async Task NotifyManagersAsync(string message, string link)
        {
            if (_notifications == null) return;
            var managers = await _userManager.GetUsersInRoleAsync("ProjectManager");
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var m in managers.Concat(admins).DistinctBy(u => u.Id))
                await _notifications.NotifyAsync(m.Id, "Assignment request", message, "AssignmentRequest", link);
        }
    }
}
