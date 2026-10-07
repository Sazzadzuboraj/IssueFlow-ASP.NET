using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int IssueId { get; set; }

        [ForeignKey(nameof(IssueId))]
        public Issue? Issue { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Comment cannot be empty.")]
        [StringLength(1000, MinimumLength = 1, ErrorMessage = "Comment must be between 1 and 1000 characters.")]
        [Display(Name = "Comment")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Posted")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
