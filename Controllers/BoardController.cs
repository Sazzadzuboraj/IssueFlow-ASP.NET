using IssueFlow.Constants;
using IssueFlow.Data;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IssueFlow.Models;

namespace IssueFlow.Controllers
{
    [Authorize]
    public class BoardController : Controller
    {
        public static readonly string[] Columns = IssueStatuses.All;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IProjectPermissionService _perm;

        public BoardController(ApplicationDbContext db, UserManager<ApplicationUser> users, IProjectPermissionService perm)
        {
            _db = db;
            _users = users;
            _perm = perm;
        }

        /// <summary>Jira-style Kanban board for a project.</summary>
        [HttpGet]
        public async Task<IActionResult> Project(int id)
        {
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            // Global PM/Admin can view; members can view
            var canView = User.IsInRole("Admin") || User.IsInRole("ProjectManager")
                          || await _perm.IsMemberAsync(id, user.Id);
            if (!canView)
            {
                TempData["Error"] = "You are not a member of this project.";
                return RedirectToAction("Index", "Project");
            }

            var issues = await _db.Issues
                .Include(i => i.Assignments!).ThenInclude(a => a.User)
                .Where(i => i.ProjectId == id)
                .OrderByDescending(i => i.Priority == "Critical")
                .ThenByDescending(i => i.Priority == "High")
                .ThenBy(i => i.CreatedDate)
                .ToListAsync();

            // Map legacy statuses into columns
            string Map(string s) => IssueStatuses.Normalize(s);

            var board = Columns.ToDictionary(c => c, c => new List<Issue>());
            foreach (var issue in issues)
            {
                var col = Map(issue.Status);
                if (!board.ContainsKey(col)) board[col] = new List<Issue>();
                board[col].Add(issue);
            }

            ViewBag.Project = project;
            ViewBag.Columns = Columns;
            ViewBag.ProjectRole = await _perm.GetProjectRoleAsync(id, user.Id);
            return View(board);
        }
    }
}
