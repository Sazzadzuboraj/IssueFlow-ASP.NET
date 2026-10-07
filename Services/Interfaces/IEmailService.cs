namespace IssueFlow.Services.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email if SMTP is configured in appsettings.
        /// When SMTP is missing, logs the message and returns false (no throw).
        /// </summary>
        Task<bool> SendAsync(string toEmail, string subject, string htmlBody);
    }
}
