using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssueFlow.Constants;

namespace IssueFlow.Models
{
    public class Issue
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Issue type is required.")]
        [StringLength(20)]
        [Display(Name = "Type")]
        public string Type { get; set; } = IssueTypes.Task;

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; } = IssueStatuses.Backlog;

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = Priorities.Medium;

        [Required(ErrorMessage = "Project is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid project.")]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public Project? Project { get; set; }

        [Required]
        [StringLength(450)]
        public string CreatedById { get; set; } = string.Empty;

        [ForeignKey(nameof(CreatedById))]
        public ApplicationUser? CreatedBy { get; set; }

        [Display(Name = "Created")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Deadline")]
        [DataType(DataType.Date)]
        public DateTime? Deadline { get; set; }

        public bool ExtensionRequested { get; set; } = false;

        [Display(Name = "Requested deadline")]
        [DataType(DataType.Date)]
        public DateTime? RequestedNewDeadline { get; set; }

        [StringLength(500, ErrorMessage = "Extension reason cannot exceed 500 characters.")]
        [Display(Name = "Extension reason")]
        public string? ExtensionReason { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Extension status")]
        public string ExtensionStatus { get; set; } = "None";

        public ICollection<IssueAssignment>? Assignments { get; set; }
        public ICollection<Comment>? Comments { get; set; }
        public ICollection<IssueAttachment>? Attachments { get; set; }
    }
}
