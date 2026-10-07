using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class Rating
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        public int? IssueId { get; set; }

        [ForeignKey(nameof(IssueId))]
        public Issue? Issue { get; set; }

        public int? BugId { get; set; }

        [ForeignKey(nameof(BugId))]
        public Bug? Bug { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Score must be between 0 and 100.")]
        [Display(Name = "Score")]
        public double Score { get; set; }

        [StringLength(300)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        [Display(Name = "Rated at")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
