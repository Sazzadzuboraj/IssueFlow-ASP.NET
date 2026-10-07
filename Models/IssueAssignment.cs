using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class IssueAssignment
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int IssueId { get; set; }

        [ForeignKey(nameof(IssueId))]
        public Issue? Issue { get; set; }

        [Required(ErrorMessage = "User is required.")]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Display(Name = "Assigned")]
        [DataType(DataType.DateTime)]
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    }
}
