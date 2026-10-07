using IssueFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: User
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.IsActive,
                    user.CreatedDate,
                    Roles = string.Join(", ", roles)
                });
            }

            ViewBag.Users = userList;
            return View(users);
        }

        // GET: User/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            ViewBag.UserRoles = userRoles;
            ViewBag.AllRoles = allRoles;
            return View(user);
        }

        // POST: User/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string fullName, bool isActive, string selectedRole,
            string? position, string? address, string? employeeCode, string? phoneNumber)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = fullName;
            user.IsActive = isActive;
            user.Position = position;
            user.Address = address;
            user.EmployeeCode = employeeCode;
            user.PhoneNumber = phoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                // Update role
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

                if (!string.IsNullOrEmpty(selectedRole))
                {
                    await _userManager.AddToRoleAsync(user, selectedRole);
                }

                TempData["Success"] = "User updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            ViewBag.UserRoles = await _userManager.GetRolesAsync(user);
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return View(user);
        }

        // POST: Assign Role
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["Success"] = $"Role '{role}' assigned to {user.FullName}";
            return RedirectToAction(nameof(Index));
        }
    }
}
