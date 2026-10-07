using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Transaction ID is required.")]
        [StringLength(50)]
        [Display(Name = "Transaction ID")]
        public string TranId { get; set; } = string.Empty;

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Plan is required.")]
        [StringLength(20)]
        [Display(Name = "Plan")]
        public string Plan { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "Amount must be greater than zero.")]
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [StringLength(3)]
        [Display(Name = "Currency")]
        public string Currency { get; set; } = "BDT";

        [Required]
        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [StringLength(100)]
        public string? ValId { get; set; }

        [StringLength(100)]
        public string? BankTranId { get; set; }

        [StringLength(50)]
        public string? CardType { get; set; }

        [StringLength(30)]
        public string? CardBrand { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? StoreAmount { get; set; }

        public int? SubscriptionId { get; set; }

        [ForeignKey(nameof(SubscriptionId))]
        public Subscription? Subscription { get; set; }

        [Display(Name = "Created")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Completed")]
        [DataType(DataType.DateTime)]
        public DateTime? CompletedDate { get; set; }

        [StringLength(8000)]
        public string? GatewayResponse { get; set; }
    }
}
