using IssueFlow.Data;
using IssueFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SubscriptionController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private static readonly Dictionary<string, (decimal Amount, int Days)> Plans = new()
        {
            { "Weekly", (99m, 7) },
            { "Monthly", (299m, 30) },
            { "Yearly", (2499m, 365) }
        };

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var subscriptions = await _context.Subscriptions
                .Where(s => s.UserId == user!.Id)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            var active = subscriptions.FirstOrDefault(s => s.IsActive && s.EndDate >= DateTime.UtcNow);
            ViewBag.ActiveSubscription = active;
            ViewBag.Plans = Plans;
            ViewBag.RecentPayments = await _context.PaymentTransactions
                .Where(p => p.UserId == user!.Id)
                .OrderByDescending(p => p.CreatedDate)
                .Take(10)
                .ToListAsync();

            return View(subscriptions);
        }

        public IActionResult Subscribe()
        {
            ViewBag.Plans = Plans;
            return View();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> All()
        {
            var all = await _context.Subscriptions
                .Include(s => s.User)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
            return View(all);
        }
    }
}
