using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssueFlow.Constants;

namespace IssueFlow.Models
{
    /// <summary>
    /// Defect found by a Tester. Lifecycle: Reported → Assigned → Fixed → ReadyForReview → Verified / Reopened.
    /// </summary>
    public class Bug
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

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; } = BugStatuses.Reported;

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
        public string ReportedById { get; set; } = string.Empty;

        [ForeignKey(nameof(ReportedById))]
        public ApplicationUser? ReportedBy { get; set; }

        [StringLength(450)]
        public string? AssignedDeveloperId { get; set; }

        [ForeignKey(nameof(AssignedDeveloperId))]
        public ApplicationUser? AssignedDeveloper { get; set; }

        [Display(Name = "Reported")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Verified")]
        [DataType(DataType.DateTime)]
        public DateTime? VerifiedDate { get; set; }

        [StringLength(1000, ErrorMessage = "Review notes cannot exceed 1000 characters.")]
        [Display(Name = "Review notes")]
        [DataType(DataType.MultilineText)]
        public string? ReviewNotes { get; set; }

        public ICollection<BugAttachment>? Attachments { get; set; }
    }
}
