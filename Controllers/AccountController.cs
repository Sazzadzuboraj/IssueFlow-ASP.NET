using IssueFlow.Models;
using IssueFlow.Models.ViewModels;
using IssueFlow.Services.Interfaces;
// subscription
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IssueFlow.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserProfileService _userProfileService;
        private readonly ISubscriptionService _subscriptionService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IUserProfileService userProfileService,
            ISubscriptionService subscriptionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _userProfileService = userProfileService;
            _subscriptionService = subscriptionService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return RedirectToLocal(returnUrl);
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber,
                    Position = model.Position,
                    Address = model.Address,
                    EmployeeCode = model.EmployeeCode,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var photoUrl = await _userProfileService.SavePhotoAsync(user.Id, model.Photo);
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        user.PhotoUrl = photoUrl;
                        await _userManager.UpdateAsync(user);
                    }

                    // Default role for new users = Developer
                    await _userManager.AddToRoleAsync(user, "Developer");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var model = new EditProfileViewModel
            {
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                EmployeeCode = user.EmployeeCode,
                Position = user.Position,
                Address = user.Address,
                CurrentPhotoUrl = user.PhotoUrl
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MyProfile(EditProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                model.Email = user.Email;
                model.CurrentPhotoUrl = user.PhotoUrl;
                return View(model);
            }

            user.FullName = model.FullName;

            var photoUrl = await _userProfileService.SavePhotoAsync(user.Id, model.Photo);
            await _userProfileService.UpdateProfileAsync(
                user, model.Position, model.Address, model.EmployeeCode, model.PhoneNumber, photoUrl);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(MyProfile));
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
