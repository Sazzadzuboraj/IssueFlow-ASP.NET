using IssueFlow.Constants;
using IssueFlow.Data;
using IssueFlow.Models;
using IssueFlow.Models.ViewModels;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRatingService _ratingService;
        private readonly IActivityLogService _activityLog;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IRatingService ratingService,
            IActivityLogService activityLog)
        {
            _context = context;
            _userManager = userManager;
            _ratingService = ratingService;
            _activityLog = activityLog;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (User.IsInRole("Admin") || User.IsInRole("ProjectManager"))
                return await ManagerDashboard(user);

            if (User.IsInRole("Developer"))
                return await DeveloperDashboard(user);

            if (User.IsInRole("QATester"))
                return await TesterDashboard(user);

            return await ManagerDashboard(user);
        }

        private async Task<IActionResult> ManagerDashboard(ApplicationUser user)
        {
            var issues = _context.Issues.AsQueryable();
            var bugs = _context.Bugs.AsQueryable();
            var projects = _context.Projects.Where(p => p.IsActive);

            var model = new ManagerDashboardViewModel
            {
                Stats = new DashboardStats
                {
                    TotalProjects = await projects.CountAsync(),
                    TotalIssues = await issues.CountAsync(),
                    // Canonical statuses
                    OpenIssues = await issues.CountAsync(i =>
                        i.Status == IssueStatuses.Backlog || i.Status == IssueStatuses.ToDo || i.Status == IssueStatuses.Reopened),
                    InProgressIssues = await issues.CountAsync(i =>
                        i.Status == IssueStatuses.InProgress || i.Status == IssueStatuses.CodeReview),
                    ResolvedIssues = await issues.CountAsync(i =>
                        i.Status == IssueStatuses.ReadyForTest || i.Status == IssueStatuses.Testing),
                    ClosedIssues = await issues.CountAsync(i => i.Status == IssueStatuses.Done),
                    TotalBugs = await bugs.CountAsync(),
                    OpenBugs = await bugs.CountAsync(b =>
                        b.Status == BugStatuses.Reported || b.Status == BugStatuses.Assigned || b.Status == BugStatuses.Reopened),
                    ReadyForReviewBugs = await bugs.CountAsync(b => b.Status == BugStatuses.ReadyForReview),
                    ReadyForReviewIssues = await issues.CountAsync(i =>
                        i.Status == IssueStatuses.ReadyForTest || i.Status == IssueStatuses.CodeReview || i.Status == IssueStatuses.Testing),
                    VerifiedBugs = await bugs.CountAsync(b => b.Status == BugStatuses.Verified)
                },
                PendingExtensions = await _context.Issues
                    .Include(i => i.Project)
                    .Include(i => i.Assignments!).ThenInclude(a => a.User)
                    .Where(i => i.ExtensionStatus == "Pending")
                    .OrderBy(i => i.Deadline)
                    .Take(10)
                    .ToListAsync(),
                UnassignedBugs = await bugs
                    .Include(b => b.Project)
                    .Include(b => b.ReportedBy)
                    .Where(b => b.Status == BugStatuses.Reported && b.AssignedDeveloperId == null)
                    .OrderByDescending(b => b.CreatedDate)
                    .Take(8)
                    .ToListAsync(),
                ReadyForReviewBugs = await bugs
                    .Include(b => b.Project)
                    .Include(b => b.AssignedDeveloper)
                    .Where(b => b.Status == BugStatuses.ReadyForReview)
                    .OrderByDescending(b => b.UpdatedDate)
                    .Take(8)
                    .ToListAsync(),
                RecentIssues = await issues
                    .Include(i => i.Project)
                    .OrderByDescending(i => i.CreatedDate)
                    .Take(8)
                    .ToListAsync(),
                RecentActivity = await _activityLog.GetRecentAsync(15),
                IssuesByStatus = await issues
                    .GroupBy(i => i.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count),
                BugsByStatus = await bugs
                    .GroupBy(b => b.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count),
                IssuesByPriority = await issues
                    .GroupBy(i => i.Priority)
                    .Select(g => new { Priority = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Priority, x => x.Count)
            };

            var devs = await _userManager.GetUsersInRoleAsync("Developer");
            var testers = await _userManager.GetUsersInRoleAsync("QATester");
            var team = devs.Concat(testers).DistinctBy(u => u.Id).ToList();

            foreach (var member in team)
            {
                var avg = await _ratingService.GetAverageScoreAsync(member.Id);
                var completed = await _context.Ratings.CountAsync(r => r.UserId == member.Id);
                var openAssign = await _context.IssueAssignments
                    .Include(a => a.Issue)
                    .CountAsync(a => a.UserId == member.Id &&
                                     a.Issue != null && a.Issue.Status != IssueStatuses.Done);

                var roles = await _userManager.GetRolesAsync(member);
                model.TeamPerformance.Add(new TeamMemberPerformance
                {
                    UserId = member.Id,
                    FullName = member.FullName ?? member.Email ?? "Unknown",
                    Role = roles.FirstOrDefault() ?? "—",
                    AverageScore = avg,
                    CompletedCount = completed,
                    OpenAssignments = openAssign
                });
            }

            model.TeamPerformance = model.TeamPerformance
                .OrderByDescending(t => t.AverageScore ?? 0)
                .ToList();

            return View("ManagerDashboard", model);
        }

        private async Task<IActionResult> DeveloperDashboard(ApplicationUser user)
        {
            var myIssues = await _context.Issues
                .Include(i => i.Project)
                .Include(i => i.Assignments!)
                .Where(i => i.Assignments!.Any(a => a.UserId == user.Id))
                .OrderBy(i => i.Priority == "Critical" ? 0 : i.Priority == "High" ? 1 : i.Priority == "Medium" ? 2 : 3)
                .ThenBy(i => i.Deadline)
                .ToListAsync();

            var myBugs = await _context.Bugs
                .Include(b => b.Project)
                .Where(b => b.AssignedDeveloperId == user.Id)
                .OrderByDescending(b => b.UpdatedDate ?? b.CreatedDate)
                .ToListAsync();

            var model = new DeveloperDashboardViewModel
            {
                Stats = new DashboardStats
                {
                    TotalIssues = myIssues.Count,
                    OpenIssues = myIssues.Count(i =>
                        i.Status is IssueStatuses.Backlog or IssueStatuses.ToDo or IssueStatuses.Reopened),
                    InProgressIssues = myIssues.Count(i =>
                        i.Status is IssueStatuses.InProgress or IssueStatuses.CodeReview),
                    ResolvedIssues = myIssues.Count(i =>
                        i.Status is IssueStatuses.ReadyForTest or IssueStatuses.Testing),
                    ClosedIssues = myIssues.Count(i => i.Status == IssueStatuses.Done),
                    TotalBugs = myBugs.Count,
                    OpenBugs = myBugs.Count(b => b.Status is BugStatuses.Assigned or BugStatuses.Reopened or BugStatuses.Fixed),
                    ReadyForReviewBugs = myBugs.Count(b => b.Status == BugStatuses.ReadyForReview),
                    VerifiedBugs = myBugs.Count(b => b.Status == BugStatuses.Verified)
                },
                MyIssues = myIssues.Where(i => i.Status != IssueStatuses.Done).Take(15).ToList(),
                MyBugs = myBugs.Where(b => b.Status != BugStatuses.Verified).Take(15).ToList(),
                PendingExtensionRequests = myIssues
                    .Where(i => i.ExtensionStatus == "Pending")
                    .ToList(),
                MyAverageScore = await _ratingService.GetAverageScoreAsync(user.Id),
                RecentRatings = await _ratingService.GetRatingsForUserAsync(user.Id),
                RecentActivity = (await _activityLog.GetRecentAsync(30))
                    .Where(a => a.UserId == user.Id)
                    .Take(10)
                    .ToList()
            };

            return View("DeveloperDashboard", model);
        }

        private async Task<IActionResult> TesterDashboard(ApplicationUser user)
        {
            var myBugs = await _context.Bugs
                .Include(b => b.Project)
                .Include(b => b.AssignedDeveloper)
                .Where(b => b.ReportedById == user.Id)
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();

            var awaitingReview = await _context.Bugs
                .Include(b => b.Project)
                .Include(b => b.AssignedDeveloper)
                .Where(b => b.Status == BugStatuses.ReadyForReview &&
                            (b.ReportedById == user.Id || User.IsInRole("Admin") || User.IsInRole("ProjectManager")))
                .OrderByDescending(b => b.UpdatedDate)
                .ToListAsync();

            var model = new TesterDashboardViewModel
            {
                Stats = new DashboardStats
                {
                    TotalBugs = myBugs.Count,
                    OpenBugs = myBugs.Count(b => b.Status is BugStatuses.Reported or BugStatuses.Assigned or BugStatuses.Reopened or BugStatuses.Fixed),
                    ReadyForReviewBugs = awaitingReview.Count,
                    VerifiedBugs = myBugs.Count(b => b.Status == BugStatuses.Verified)
                },
                MyReportedBugs = myBugs.Take(15).ToList(),
                AwaitingMyReview = awaitingReview.Take(15).ToList(),
                MyAverageScore = await _ratingService.GetAverageScoreAsync(user.Id),
                RecentRatings = await _ratingService.GetRatingsForUserAsync(user.Id),
                RecentActivity = (await _activityLog.GetRecentAsync(30))
                    .Where(a => a.UserId == user.Id)
                    .Take(10)
                    .ToList()
            };

            return View("TesterDashboard", model);
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }
    }
}
