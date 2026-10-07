using IssueFlow.Data;
using IssueFlow.Models;
using IssueFlow.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Controllers
{
    /// <summary>
    /// SSLCommerz callbacks + payment initiation for subscription plans.
    /// Success/Fail/Cancel/IPN must allow anonymous POST from SSLCommerz servers.
    /// </summary>
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISslCommerzService _ssl;
        private readonly ILogger<PaymentController> _logger;

        private static readonly Dictionary<string, (decimal Amount, int Days)> Plans = new()
        {
            { "Weekly", (99m, 7) },
            { "Monthly", (299m, 30) },
            { "Yearly", (2499m, 365) }
        };

        public PaymentController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ISslCommerzService ssl,
            ILogger<PaymentController> logger)
        {
            _db = db;
            _userManager = userManager;
            _ssl = ssl;
            _logger = logger;
        }

        /// <summary>Start SSLCommerz checkout for a plan (logged-in user).</summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Initiate(string plan)
        {
            if (string.IsNullOrWhiteSpace(plan) || !Plans.ContainsKey(plan))
            {
                TempData["Error"] = "Invalid plan selected.";
                return RedirectToAction("Subscribe", "Subscription");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var (amount, _) = Plans[plan];
            var tranId = $"IF{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

            var payment = new PaymentTransaction
            {
                TranId = tranId,
                UserId = user.Id,
                Plan = plan,
                Amount = amount,
                Currency = "BDT",
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _db.PaymentTransactions.Add(payment);
            await _db.SaveChangesAsync();

            var session = await _ssl.InitiatePaymentAsync(payment, user);

            // SSL JSON can be large — never fail the redirect because of logging fields
            if (!string.IsNullOrEmpty(session.RawJson))
            {
                payment.GatewayResponse = session.RawJson.Length > 7900
                    ? session.RawJson.Substring(0, 7900)
                    : session.RawJson;
            }

            try
            {
                if (!session.Success || string.IsNullOrWhiteSpace(session.GatewayPageUrl))
                {
                    payment.Status = "Failed";
                    await _db.SaveChangesAsync();
                    TempData["Error"] = session.Error ?? "Could not start payment gateway.";
                    return RedirectToAction("Subscribe", "Subscription");
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Session already created at SSLCommerz — still send user to pay
                var logger = HttpContext.RequestServices.GetService<Microsoft.Extensions.Logging.ILogger<PaymentController>>();
                logger?.LogWarning(ex, "Could not save gateway response for {TranId}", payment.TranId);
                if (session.Success && !string.IsNullOrWhiteSpace(session.GatewayPageUrl))
                    return Redirect(session.GatewayPageUrl);
                TempData["Error"] = session.Error ?? ex.Message;
                return RedirectToAction("Subscribe", "Subscription");
            }

            return Redirect(session.GatewayPageUrl!);
        }

        /// <summary>Browser redirect after successful payment (SSLCommerz POST).</summary>
        [AllowAnonymous]
        [HttpGet, HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Success()
        {
            var form = Request.HasFormContentType
                ? Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString())
                : new Dictionary<string, string>();

            // Also accept query string fallbacks
            foreach (var q in Request.Query)
                form.TryAdd(q.Key, q.Value.ToString());

            var tranId = form.GetValueOrDefault("tran_id");
            var valId = form.GetValueOrDefault("val_id");
            var status = form.GetValueOrDefault("status");

            _logger.LogInformation("Payment Success callback tran_id={TranId} status={Status}", tranId, status);

            var ok = await FinalizeIfValidAsync(tranId, valId, form);
            if (ok)
            {
                TempData["Success"] = "Payment successful! Your subscription is now active.";
            }
            else
            {
                TempData["Error"] = "Payment received but validation failed. If money was deducted, contact support with your transaction id: " + tranId;
            }

            return RedirectToAction("Index", "Subscription");
        }

        [AllowAnonymous]
        [HttpGet, HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Fail()
        {
            var tranId = Request.Form["tran_id"].ToString();
            if (string.IsNullOrEmpty(tranId))
                tranId = Request.Query["tran_id"].ToString();

            await MarkStatusAsync(tranId, "Failed");
            TempData["Error"] = "Payment failed. Please try again.";
            return RedirectToAction("Subscribe", "Subscription");
        }

        [AllowAnonymous]
        [HttpGet, HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Cancel()
        {
            var tranId = Request.Form["tran_id"].ToString();
            if (string.IsNullOrEmpty(tranId))
                tranId = Request.Query["tran_id"].ToString();

            await MarkStatusAsync(tranId, "Cancelled");
            TempData["Error"] = "Payment was cancelled.";
            return RedirectToAction("Subscribe", "Subscription");
        }

        /// <summary>
        /// Server-to-server IPN — primary source of truth.
        /// Configure this URL in SSLCommerz panel: https://yourdomain.com/Payment/Ipn
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Ipn()
        {
            var form = Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString());
            var tranId = form.GetValueOrDefault("tran_id");
            var valId = form.GetValueOrDefault("val_id");
            var status = form.GetValueOrDefault("status");

            _logger.LogInformation("IPN received tran_id={TranId} status={Status}", tranId, status);

            if (string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "VALIDATED", StringComparison.OrdinalIgnoreCase))
            {
                await FinalizeIfValidAsync(tranId, valId, form);
            }
            else
            {
                await MarkStatusAsync(tranId, status ?? "Failed");
            }

            return Ok("IPN received");
        }

        private async Task<bool> FinalizeIfValidAsync(string? tranId, string? valId, Dictionary<string, string> form)
        {
            if (string.IsNullOrWhiteSpace(tranId))
                return false;

            var payment = await _db.PaymentTransactions
                .FirstOrDefaultAsync(p => p.TranId == tranId);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found for tran_id {TranId}", tranId);
                return false;
            }

            // Idempotent: already activated
            if (payment.Status is "Success" or "Validated" && payment.SubscriptionId != null)
                return true;

            // Prefer server-side validation when val_id present
            SslValidationResult? validation = null;
            if (!string.IsNullOrWhiteSpace(valId))
            {
                validation = await _ssl.ValidatePaymentAsync(valId);
                if (!validation.IsValid)
                {
                    payment.Status = "Failed";
                    payment.GatewayResponse = validation.RawJson;
                    payment.CompletedDate = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    return false;
                }

                // Amount must match
                if (validation.Amount.HasValue && Math.Abs(validation.Amount.Value - payment.Amount) > 0.01m)
                {
                    _logger.LogWarning("Amount mismatch for {TranId}: expected {Expected}, got {Got}",
                        tranId, payment.Amount, validation.Amount);
                    payment.Status = "Failed";
                    payment.GatewayResponse = validation.RawJson;
                    await _db.SaveChangesAsync();
                    return false;
                }
            }
            else
            {
                // No val_id — only accept if form status is VALID (still weaker; IPN should carry val_id)
                var formStatus = form.GetValueOrDefault("status");
                if (!string.Equals(formStatus, "VALID", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(formStatus, "VALIDATED", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            payment.ValId = validation?.ValId ?? valId;
            payment.BankTranId = validation?.BankTranId ?? form.GetValueOrDefault("bank_tran_id");
            payment.CardType = validation?.CardType ?? form.GetValueOrDefault("card_type");
            payment.CardBrand = validation?.CardBrand;
            payment.StoreAmount = validation?.StoreAmount;
            payment.Status = "Validated";
            payment.CompletedDate = DateTime.UtcNow;
            payment.GatewayResponse = validation?.RawJson ?? payment.GatewayResponse;

            if (!Plans.TryGetValue(payment.Plan, out var planInfo))
            {
                await _db.SaveChangesAsync();
                return false;
            }

            // Deactivate previous active subscriptions for this user
            var oldSubs = await _db.Subscriptions
                .Where(s => s.UserId == payment.UserId && s.IsActive)
                .ToListAsync();
            foreach (var old in oldSubs)
                old.IsActive = false;

            var subscription = new Subscription
            {
                UserId = payment.UserId,
                Plan = payment.Plan,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(planInfo.Days),
                Amount = payment.Amount,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            _db.Subscriptions.Add(subscription);
            await _db.SaveChangesAsync();

            payment.SubscriptionId = subscription.Id;
            payment.Status = "Success";
            await _db.SaveChangesAsync();

            _logger.LogInformation("Subscription activated for user {UserId} plan {Plan} via {TranId}",
                payment.UserId, payment.Plan, payment.TranId);

            return true;
        }

        private async Task MarkStatusAsync(string? tranId, string status)
        {
            if (string.IsNullOrWhiteSpace(tranId)) return;
            var payment = await _db.PaymentTransactions.FirstOrDefaultAsync(p => p.TranId == tranId);
            if (payment == null) return;
            if (payment.Status is "Success" or "Validated") return;
            payment.Status = status;
            payment.CompletedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
