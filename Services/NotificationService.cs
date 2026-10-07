using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _email;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IUnitOfWork unitOfWork,
            IEmailService email,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationService> logger)
        {
            _unitOfWork = unitOfWork;
            _email = email;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task NotifyAsync(
            string userId,
            string title,
            string? message,
            string type,
            string? linkUrl = null,
            bool sendEmail = true)
        {
            var n = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.Notifications.AddAsync(n);
            await _unitOfWork.SaveChangesAsync();

            if (sendEmail)
            {
                try
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user?.Email != null)
                    {
                        var body = $@"
                            <h3>{System.Net.WebUtility.HtmlEncode(title)}</h3>
                            <p>{System.Net.WebUtility.HtmlEncode(message ?? "")}</p>
                            {(string.IsNullOrEmpty(linkUrl) ? "" : $"<p><a href=\"{linkUrl}\">Open in IssueFlow</a></p>")}
                            <hr/><small>IssueFlow notification</small>";
                        await _email.SendAsync(user.Email, $"[IssueFlow] {title}", body);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Email notification failed for user {UserId}", userId);
                }
            }
        }

        public async Task<List<Notification>> GetUnreadAsync(string userId, int take = 15)
        {
            return await _unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetRecentAsync(string userId, int take = 30)
        {
            return await _unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _unitOfWork.Notifications.Query()
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId, string userId)
        {
            var n = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
            if (n == null) return;
            n.IsRead = true;
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var list = await _unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            foreach (var n in list) n.IsRead = true;
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
