using System.Threading.RateLimiting;
using IssueFlow.Data;
using IssueFlow.Middleware;
using IssueFlow.Filters;
using IssueFlow.Models;
using IssueFlow.Repositories;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services;
using IssueFlow.Services.Interfaces;
using IssueFlow.Services.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging (Serilog: console + rolling file) ----------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/issueflow-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();
builder.Host.UseSerilog();

// ---------- Database ----------
// NOTE: for real deployments, move the connection string out of appsettings.json
// into User Secrets (dev) or environment variables / Azure Key Vault (prod):
//   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Identity ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;

    // Lockout after repeated failed logins — brute-force mitigation.
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// ---------- Clean architecture: Repository + Unit of Work + Services ----------
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<IBugService, BugService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IProjectPermissionService, ProjectPermissionService>();
builder.Services.AddScoped<IProjectFileService, ProjectFileService>();
builder.Services.AddScoped<IProjectService, ProjectService>();

// ---------- SSLCommerz payment gateway ----------
builder.Services.Configure<SslCommerzOptions>(builder.Configuration.GetSection(SslCommerzOptions.SectionName));
builder.Services.AddHttpClient<ISslCommerzService, SslCommerzService>();

// ---------- Rate limiting (built into ASP.NET Core) ----------
// Protects login/register and the API from brute-force / abuse.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---------- Swagger / OpenAPI for the JSON API ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IssueFlow API",
        Version = "v1",
        Description = "REST API for the IssueFlow issue-tracking system."
    });
});

var app = builder.Build();

// ---------- Seed roles & admin ----------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// ---------- Middleware pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IssueFlow API v1"));
}

app.UseHttpsRedirection();

// Basic security headers (defense in depth against clickjacking/MIME sniffing/XSS).
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapControllers(); // enables [ApiController] attribute-routed endpoints

app.Run();
