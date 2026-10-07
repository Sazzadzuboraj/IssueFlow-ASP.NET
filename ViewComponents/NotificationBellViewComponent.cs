using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IssueFlow.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly INotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBellViewComponent(
            INotificationService notifications,
            UserManager<ApplicationUser> userManager)
        {
            _notifications = notifications;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (UserClaimsPrincipal?.Identity?.IsAuthenticated != true)
                return View(new NotificationBellModel());

            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (user == null)
                return View(new NotificationBellModel());

            var unread = await _notifications.GetUnreadAsync(user.Id, 8);
            var count = await _notifications.GetUnreadCountAsync(user.Id);

            return View(new NotificationBellModel
            {
                UnreadCount = count,
                Items = unread
            });
        }
    }

    public class NotificationBellModel
    {
        public int UnreadCount { get; set; }
        public List<Notification> Items { get; set; } = new();
    }
}
