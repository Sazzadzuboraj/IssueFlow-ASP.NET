using System.Net;
using System.Net.Mail;
using IssueFlow.Services.Interfaces;

namespace IssueFlow.Services
{
    /// <summary>
    /// SMTP email sender. Configure under "Email" in appsettings.json.
    /// If Host is empty, emails are skipped and only logged (safe for local dev).
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _config["Email:SmtpHost"];
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogInformation(
                    "Email skipped (no SMTP configured). To={To} Subject={Subject}",
                    toEmail, subject);
                return false;
            }

            try
            {
                var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
                var from = _config["Email:From"] ?? "noreply@issueflow.local";
                var user = _config["Email:Username"];
                var pass = _config["Email:Password"];
                var enableSsl = !string.Equals(_config["Email:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                if (!string.IsNullOrWhiteSpace(user))
                    client.Credentials = new NetworkCredential(user, pass);

                using var msg = new MailMessage(from, toEmail, subject, htmlBody)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(msg);
                _logger.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}: {Subject}", toEmail, subject);
                return false;
            }
        }
    }
}
