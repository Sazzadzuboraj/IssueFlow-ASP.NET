using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    /// <summary>
    /// Work request (Developer) or Review request (QA) for an Issue/Bug.
    /// </summary>
    public class AssignmentRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Target type is required.")]
        [StringLength(10)]
        [Display(Name = "Target type")]
        public string TargetType { get; set; } = "Issue";

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid target.")]
        [Display(Name = "Target")]
        public int TargetId { get; set; }

        [Required(ErrorMessage = "Request kind is required.")]
        [StringLength(20)]
        [Display(Name = "Request kind")]
        public string RequestKind { get; set; } = "Work";

        [Required]
        [StringLength(450)]
        public string RequesterId { get; set; } = string.Empty;

        [ForeignKey(nameof(RequesterId))]
        public ApplicationUser? Requester { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters.")]
        [Display(Name = "Message")]
        [DataType(DataType.MultilineText)]
        public string? RequestMessage { get; set; }

        [StringLength(500, ErrorMessage = "Response cannot exceed 500 characters.")]
        [Display(Name = "Manager response")]
        [DataType(DataType.MultilineText)]
        public string? ManagerResponse { get; set; }

        [StringLength(10)]
        public string? AlternativeTargetType { get; set; }

        [Range(1, int.MaxValue)]
        public int? AlternativeTargetId { get; set; }

        [StringLength(450)]
        public string? OfferedToUserId { get; set; }

        [ForeignKey(nameof(OfferedToUserId))]
        public ApplicationUser? OfferedToUser { get; set; }

        [StringLength(450)]
        public string? ReviewedById { get; set; }

        [ForeignKey(nameof(ReviewedById))]
        public ApplicationUser? ReviewedBy { get; set; }

        [Display(Name = "Created")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Reviewed")]
        [DataType(DataType.DateTime)]
        public DateTime? ReviewedDate { get; set; }
    }
}
