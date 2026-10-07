using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(120, MinimumLength = 1)]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Message")]
        public string? Message { get; set; }

        [Required]
        [StringLength(60)]
        [Display(Name = "Type")]
        public string Type { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Link")]
        public string? LinkUrl { get; set; }

        [Display(Name = "Read")]
        public bool IsRead { get; set; } = false;

        [Display(Name = "Created")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
