using System.ComponentModel.DataAnnotations;
using IssueFlow.Constants;

namespace IssueFlow.DTOs
{
    public class BugDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string ReportedById { get; set; } = string.Empty;
        public string? ReportedByName { get; set; }
        public string? AssignedDeveloperId { get; set; }
        public string? AssignedDeveloperName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class CreateBugDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = Priorities.Medium;

        [Required(ErrorMessage = "Project is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid project.")]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }
    }

    public class AssignBugDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid bug.")]
        public int BugId { get; set; }

        [Required(ErrorMessage = "Please select a developer.")]
        [StringLength(450)]
        [Display(Name = "Developer")]
        public string DeveloperId { get; set; } = string.Empty;
    }

    public class ReviewBugDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int BugId { get; set; }

        [Required]
        [Display(Name = "Approve")]
        public bool Approve { get; set; }

        [StringLength(1000, ErrorMessage = "Review notes cannot exceed 1000 characters.")]
        [Display(Name = "Review notes")]
        public string? ReviewNotes { get; set; }
    }
}
