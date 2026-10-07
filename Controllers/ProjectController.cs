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
    /// HTTP only — all project data access goes through IProjectService.
    /// </summary>
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectService _projectService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IProjectPermissionService _perm;

        public ProjectController(
            UserManager<ApplicationUser> userManager,
            IProjectService projectService,
            ISubscriptionService subscriptionService,
            IProjectPermissionService perm)
        {
            _userManager = userManager;
            _projectService = projectService;
            _subscriptionService = subscriptionService;
            _perm = perm;
        }

        public async Task<IActionResult> Index(string? search)
        {
            ViewBag.Search = search;
            var projects = await _projectService.GetActiveProjectsAsync(search);
            return View(projects);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var project = await _projectService.GetDetailsAsync(id.Value);
            if (project == null) return NotFound();

            ViewBag.CanManage = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            return View(project);
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Create([Bind("Name,Description")] Project project)
        {
            ModelState.Remove(nameof(Project.CreatedById));
            ModelState.Remove(nameof(Project.CreatedBy));
            ModelState.Remove(nameof(Project.CreatedDate));
            ModelState.Remove(nameof(Project.IsActive));
            ModelState.Remove(nameof(Project.Issues));
            ModelState.Remove(nameof(Project.Members));

            if (!ModelState.IsValid)
                return View(project);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!User.IsInRole("Admin"))
            {
                var (allowed, message) = await _subscriptionService.CanCreateProjectAsync(user.Id);
                if (!allowed)
                {
                    TempData["Error"] = message;
                    return RedirectToAction(nameof(Index));
                }
            }

            var created = await _projectService.CreateAsync(project.Name, project.Description, user.Id);
            TempData["Success"] = "Project created. You are the Project Leader. Open Board for the Kanban view, or Team to add members.";
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var project = await _projectService.GetByIdAsync(id.Value);
            if (project == null) return NotFound();
            return View(project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Project project)
        {
            if (id != project.Id) return NotFound();

            ModelState.Remove(nameof(Project.CreatedById));
            ModelState.Remove(nameof(Project.CreatedBy));
            ModelState.Remove(nameof(Project.CreatedDate));
            ModelState.Remove(nameof(Project.IsActive));
            ModelState.Remove(nameof(Project.Issues));
            ModelState.Remove(nameof(Project.Members));

            if (!ModelState.IsValid)
                return View(project);

            var ok = await _projectService.UpdateAsync(id, project.Name, project.Description);
            if (!ok) return NotFound();

            TempData["Success"] = "Project updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var project = await _projectService.GetDetailsAsync(id.Value);
            if (project == null) return NotFound();
            return View(project);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _projectService.SoftDeleteAsync(id);
            TempData["Success"] = "Project deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Members(int id)
        {
            var project = await _projectService.GetWithMembersAsync(id);
            if (project == null) return NotFound();

            var allUsers = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(
                allUsers.Select(u => new { u.Id, Name = (u.FullName ?? u.Email) + " (" + u.Email + ")" }),
                "Id", "Name");
            ViewBag.Roles = new SelectList(new[]
            {
                new { Value = "ProjectManager", Text = "Project Leader" },
                new { Value = "Developer", Text = "Developer" },
                new { Value = "QATester", Text = "QA Tester" },
                new { Value = "Designer", Text = "Designer" },
                new { Value = "Reporter", Text = "Reporter" }
            }, "Value", "Text");
            return View(project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> AddMember(int projectId, string userId, string role)
        {
            var (ok, error) = await _projectService.AddMemberAsync(projectId, userId, role);
            TempData[ok ? "Success" : "Error"] = ok ? "Member added." : error;
            return RedirectToAction(nameof(Members), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> RemoveMember(int id, int projectId)
        {
            await _projectService.RemoveMemberAsync(id);
            TempData["Success"] = "Member removed.";
            return RedirectToAction(nameof(Members), new { id = projectId });
        }
    }
}
