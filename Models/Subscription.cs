using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class Subscription
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Plan is required.")]
        [StringLength(20)]
        [Display(Name = "Plan")]
        public string Plan { get; set; } = "Monthly";

        [Display(Name = "Start date")]
        [DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "End date")]
        [DataType(DataType.DateTime)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999.99, ErrorMessage = "Amount must be between 0 and 999999.99.")]
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "Created")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
