using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IssueFlow.Filters
{
    /// <summary>
    /// Optional attribute: require the current user to have an active subscription.
    /// Viewing projects/issues/bugs is allowed without this.
    /// Use on request/assign actions if you prefer attribute-based gates.
    /// Global filter is NOT registered — assignment gates live in controllers.
    /// </summary>
    public class RequireActiveSubscriptionAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            if (user.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var subService = context.HttpContext.RequestServices.GetRequiredService<ISubscriptionService>();
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = await userManager.GetUserAsync(user);
            if (appUser == null || !await subService.HasActiveSubscriptionAsync(appUser.Id))
            {
                context.Result = new RedirectToActionResult("Subscribe", "Subscription", null);
                return;
            }

            await next();
        }
    }
}
