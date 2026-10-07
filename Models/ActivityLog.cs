using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        public int? IssueId { get; set; }

        [ForeignKey(nameof(IssueId))]
        public Issue? Issue { get; set; }

        public int? BugId { get; set; }

        [ForeignKey(nameof(BugId))]
        public Bug? Bug { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Action is required.")]
        [StringLength(60)]
        [Display(Name = "Action")]
        public string Action { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Details")]
        public string? Details { get; set; }

        [Display(Name = "When")]
        [DataType(DataType.DateTime)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
