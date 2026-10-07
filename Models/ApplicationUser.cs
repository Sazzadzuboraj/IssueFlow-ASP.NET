using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace IssueFlow.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        [Display(Name = "Full name")]
        public string? FullName { get; set; }

        [Display(Name = "Joined")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [StringLength(50)]
        [Display(Name = "Employee ID")]
        public string? EmployeeCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Position")]
        public string? Position { get; set; }

        [StringLength(300)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [StringLength(500)]
        [Display(Name = "Photo")]
        public string? PhotoUrl { get; set; }

        public ICollection<Project>? CreatedProjects { get; set; }
        public ICollection<Issue>? CreatedIssues { get; set; }
        public ICollection<IssueAssignment>? Assignments { get; set; }
        public ICollection<Comment>? Comments { get; set; }
        public ICollection<Subscription>? Subscriptions { get; set; }
        public ICollection<Bug>? ReportedBugs { get; set; }
        public ICollection<Bug>? AssignedBugs { get; set; }
        public ICollection<Rating>? Ratings { get; set; }
    }
}
