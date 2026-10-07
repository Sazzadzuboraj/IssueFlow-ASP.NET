using IssueFlow.Models;

namespace IssueFlow.Services.Interfaces
{
    public interface INotificationService
    {
        Task NotifyAsync(string userId, string title, string? message, string type, string? linkUrl = null, bool sendEmail = true);
        Task<List<Notification>> GetUnreadAsync(string userId, int take = 15);
        Task<List<Notification>> GetRecentAsync(string userId, int take = 30);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(int notificationId, string userId);
        Task MarkAllAsReadAsync(string userId);
    }
}
