using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IssueFlow.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(
            INotificationService notifications,
            UserManager<ApplicationUser> userManager)
        {
            _notifications = notifications;
            _userManager = userManager;
        }

        // GET: Notification — full list
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var list = await _notifications.GetRecentAsync(user!.Id, 50);
            return View(list);
        }

        // GET: Notification/UnreadCount — used by layout AJAX (optional)
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            var count = await _notifications.GetUnreadCountAsync(user!.Id);
            return Json(new { count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            await _notifications.MarkAsReadAsync(id, user!.Id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User);
            await _notifications.MarkAllAsReadAsync(user!.Id);
            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }
    }
}
