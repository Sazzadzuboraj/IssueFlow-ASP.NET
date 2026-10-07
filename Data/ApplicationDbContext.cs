using IssueFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IssueFlow.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Project> Projects { get; set; }
        public DbSet<Issue> Issues { get; set; }
        public DbSet<IssueAssignment> IssueAssignments { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Bug> Bugs { get; set; }
        public DbSet<BugAttachment> BugAttachments { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<AssignmentRequest> AssignmentRequests { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<IssueAttachment> IssueAttachments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ---------- Existing relationships ----------
            builder.Entity<Project>()
                .HasOne(p => p.CreatedBy)
                .WithMany(u => u.CreatedProjects)
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Issue>()
                .HasOne(i => i.CreatedBy)
                .WithMany(u => u.CreatedIssues)
                .HasForeignKey(i => i.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Issue>()
                .HasOne(i => i.Project)
                .WithMany(p => p.Issues)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<IssueAssignment>()
                .HasOne(a => a.Issue)
                .WithMany(i => i.Assignments)
                .HasForeignKey(a => a.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<IssueAssignment>()
                .HasOne(a => a.User)
                .WithMany(u => u.Assignments)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Comment>()
                .HasOne(c => c.Issue)
                .WithMany(i => i.Comments)
                .HasForeignKey(c => c.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Subscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- Bug ----------
            builder.Entity<Bug>()
                .HasOne(b => b.Project)
                .WithMany()
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Bug>()
                .HasOne(b => b.ReportedBy)
                .WithMany(u => u.ReportedBugs)
                .HasForeignKey(b => b.ReportedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Bug>()
                .HasOne(b => b.AssignedDeveloper)
                .WithMany(u => u.AssignedBugs)
                .HasForeignKey(b => b.AssignedDeveloperId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- BugAttachment ----------
            builder.Entity<BugAttachment>()
                .HasOne(a => a.Bug)
                .WithMany(b => b.Attachments)
                .HasForeignKey(a => a.BugId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BugAttachment>()
                .HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Rating ----------
            builder.Entity<Rating>()
                .HasOne(r => r.User)
                .WithMany(u => u.Ratings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Rating>()
                .HasOne(r => r.Issue)
                .WithMany()
                .HasForeignKey(r => r.IssueId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Rating>()
                .HasOne(r => r.Bug)
                .WithMany()
                .HasForeignKey(r => r.BugId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- ActivityLog ----------
            builder.Entity<ActivityLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ActivityLog>()
                .HasOne(a => a.Issue)
                .WithMany()
                .HasForeignKey(a => a.IssueId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ActivityLog>()
                .HasOne(a => a.Bug)
                .WithMany()
                .HasForeignKey(a => a.BugId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Notification ----------
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PaymentTransaction>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PaymentTransaction>()
                .HasIndex(p => p.TranId)
                .IsUnique();

            builder.Entity<PaymentTransaction>()
                .HasOne(p => p.Subscription)
                .WithMany()
                .HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AssignmentRequest>()
                .HasOne(a => a.Requester)
                .WithMany()
                .HasForeignKey(a => a.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AssignmentRequest>()
                .HasOne(a => a.ReviewedBy)
                .WithMany()
                .HasForeignKey(a => a.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AssignmentRequest>()
                .HasOne(a => a.OfferedToUser)
                .WithMany()
                .HasForeignKey(a => a.OfferedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProjectMember>()
                .HasOne(m => m.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProjectMember>()
                .HasIndex(m => new { m.ProjectId, m.UserId })
                .IsUnique();

            // ---------- IssueAttachment (project source file system) ----------
            builder.Entity<IssueAttachment>()
                .HasOne(a => a.Issue)
                .WithMany(i => i.Attachments)
                .HasForeignKey(a => a.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<IssueAttachment>()
                .HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<IssueAttachment>()
                .HasIndex(a => a.IssueId);
        }
    }
}
