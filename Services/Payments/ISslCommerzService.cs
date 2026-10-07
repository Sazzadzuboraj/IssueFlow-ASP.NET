using IssueFlow.Models;

namespace IssueFlow.Services.Payments
{
    public class SslSessionResult
    {
        public bool Success { get; set; }
        public string? GatewayPageUrl { get; set; }
        public string? SessionKey { get; set; }
        public string? Error { get; set; }
        public string? RawJson { get; set; }
    }

    public class SslValidationResult
    {
        public bool IsValid { get; set; }
        public string Status { get; set; } = string.Empty; // VALID, VALIDATED, INVALID, etc.
        public string? TranId { get; set; }
        public string? ValId { get; set; }
        public string? BankTranId { get; set; }
        public string? CardType { get; set; }
        public string? CardBrand { get; set; }
        public decimal? Amount { get; set; }
        public decimal? StoreAmount { get; set; }
        public string? Currency { get; set; }
        public int RiskLevel { get; set; }
        public string? RawJson { get; set; }
    }

    public interface ISslCommerzService
    {
        Task<SslSessionResult> InitiatePaymentAsync(PaymentTransaction payment, ApplicationUser user, CancellationToken ct = default);
        Task<SslValidationResult> ValidatePaymentAsync(string valId, CancellationToken ct = default);
    }
}
