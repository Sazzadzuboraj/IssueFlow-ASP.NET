using IssueFlow.Constants;
using IssueFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            string[] roles = { "Admin", "ProjectManager", "Developer", "QATester" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            async Task<ApplicationUser> EnsureUserAsync(string email, string fullName, string password, string role)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FullName = fullName,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    };
                    var result = await userManager.CreateAsync(user, password);
                    if (result.Succeeded)
                        await userManager.AddToRoleAsync(user, role);
                }
                return (await userManager.FindByEmailAsync(email))!;
            }

            var admin = await EnsureUserAsync("admin@issueflow.com", "System Admin", "Admin@123", "Admin");
            var pm = await EnsureUserAsync("pm@issueflow.com", "Project Manager", "Pm@12345", "ProjectManager");
            var dev = await EnsureUserAsync("dev@issueflow.com", "John Developer", "Dev@12345", "Developer");
            var qa = await EnsureUserAsync("qa@issueflow.com", "Sarah QA", "Qa@12345", "QATester");

            // Active Yearly subscription for demo users (unblocks assign/request; Free also works now)
            async Task EnsureSubAsync(string userId)
            {
                var has = await context.Subscriptions.AnyAsync(s =>
                    s.UserId == userId && s.IsActive && s.EndDate >= DateTime.UtcNow);
                if (!has)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        UserId = userId,
                        Plan = "Yearly",
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddYears(1),
                        IsActive = true,
                        Amount = 0,
                        CreatedDate = DateTime.UtcNow
                    });
                }
            }

            await EnsureSubAsync(admin.Id);
            await EnsureSubAsync(pm.Id);
            await EnsureSubAsync(dev.Id);
            await EnsureSubAsync(qa.Id);
            await context.SaveChangesAsync();

            // Sample project + members + issues so modules are not empty
            if (!await context.Projects.AnyAsync())
            {
                var project = new Project
                {
                    Name = "IssueFlow Demo",
                    Description = "Sample project for demos and testing workflows.",
                    CreatedById = pm.Id,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };
                context.Projects.Add(project);
                await context.SaveChangesAsync();

                context.ProjectMembers.AddRange(
                    new ProjectMember { ProjectId = project.Id, UserId = pm.Id, Role = "ProjectManager", JoinedDate = DateTime.UtcNow },
                    new ProjectMember { ProjectId = project.Id, UserId = dev.Id, Role = "Developer", JoinedDate = DateTime.UtcNow },
                    new ProjectMember { ProjectId = project.Id, UserId = qa.Id, Role = "QATester", JoinedDate = DateTime.UtcNow }
                );

                var issue1 = new Issue
                {
                    Title = "Implement login page",
                    Description = "Build the login UI and wire Identity.",
                    Type = IssueTypes.Task,
                    Status = IssueStatuses.InProgress,
                    Priority = Priorities.High,
                    ProjectId = project.Id,
                    CreatedById = pm.Id,
                    CreatedDate = DateTime.UtcNow,
                    Deadline = DateTime.UtcNow.Date.AddDays(7),
                    ExtensionStatus = "None"
                };
                var issue2 = new Issue
                {
                    Title = "Kanban board polish",
                    Description = "Improve board columns and card layout.",
                    Type = IssueTypes.Story,
                    Status = IssueStatuses.Backlog,
                    Priority = Priorities.Medium,
                    ProjectId = project.Id,
                    CreatedById = pm.Id,
                    CreatedDate = DateTime.UtcNow,
                    ExtensionStatus = "None"
                };
                context.Issues.AddRange(issue1, issue2);
                await context.SaveChangesAsync();

                context.IssueAssignments.Add(new IssueAssignment
                {
                    IssueId = issue1.Id,
                    UserId = dev.Id,
                    AssignedDate = DateTime.UtcNow
                });

                context.Bugs.Add(new Bug
                {
                    Title = "Login button not responding on mobile",
                    Description = "Tap does nothing on small screens.",
                    Status = BugStatuses.Reported,
                    Priority = Priorities.High,
                    ProjectId = project.Id,
                    ReportedById = qa.Id,
                    CreatedDate = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
