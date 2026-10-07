using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using IssueFlow.Models;
using Microsoft.Extensions.Options;

namespace IssueFlow.Services.Payments
{
    /// <summary>
    /// SSLCommerz v4 Session + Order Validation API client.
    /// Docs: https://developer.sslcommerz.com/
    /// </summary>
    public class SslCommerzService : ISslCommerzService
    {
        private readonly HttpClient _http;
        private readonly SslCommerzOptions _opt;
        private readonly ILogger<SslCommerzService> _logger;

        public SslCommerzService(HttpClient http, IOptions<SslCommerzOptions> opt, ILogger<SslCommerzService> logger)
        {
            _http = http;
            _opt = opt.Value;
            _logger = logger;
        }

        public async Task<SslSessionResult> InitiatePaymentAsync(PaymentTransaction payment, ApplicationUser user, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opt.StoreId) || string.IsNullOrWhiteSpace(_opt.StorePassword))
            {
                return new SslSessionResult
                {
                    Success = false,
                    Error = "SSLCommerz is not configured. Set StoreId and StorePassword in appsettings (SslCommerz section)."
                };
            }

            var baseUrl = (_opt.BaseUrl ?? "").TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return new SslSessionResult
                {
                    Success = false,
                    Error = "SslCommerz:BaseUrl is required (public URL for success/fail/cancel/ipn callbacks)."
                };
            }

            var amount = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture);
            var form = new Dictionary<string, string>
            {
                ["store_id"] = _opt.StoreId,
                ["store_passwd"] = _opt.StorePassword,
                ["total_amount"] = amount,
                ["currency"] = payment.Currency,
                ["tran_id"] = payment.TranId,
                ["success_url"] = $"{baseUrl}/Payment/Success",
                ["fail_url"] = $"{baseUrl}/Payment/Fail",
                ["cancel_url"] = $"{baseUrl}/Payment/Cancel",
                ["ipn_url"] = $"{baseUrl}/Payment/Ipn",
                ["cus_name"] = user.FullName ?? user.Email ?? "Customer",
                ["cus_email"] = user.Email ?? "customer@example.com",
                ["cus_add1"] = user.Address ?? "Dhaka",
                ["cus_city"] = "Dhaka",
                ["cus_country"] = "Bangladesh",
                ["cus_phone"] = user.PhoneNumber ?? "01700000000",
                ["shipping_method"] = "NO",
                ["num_of_item"] = "1",
                ["product_name"] = $"IssueFlow {payment.Plan} Subscription",
                ["product_category"] = "Subscription",
                ["product_profile"] = "non-physical-goods",
                ["value_a"] = payment.UserId,
                ["value_b"] = payment.Plan,
                ["value_c"] = payment.Id.ToString()
            };

            try
            {
                using var content = new FormUrlEncodedContent(form);
                var response = await _http.PostAsync(_opt.SessionApiUrl, content, ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("SSLCommerz session response for {TranId}: {Json}", payment.TranId, json);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

                if (string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    var gatewayUrl = root.TryGetProperty("GatewayPageURL", out var g) ? g.GetString() : null;
                    var sessionKey = root.TryGetProperty("sessionkey", out var sk) ? sk.GetString() : null;
                    if (string.IsNullOrWhiteSpace(gatewayUrl))
                    {
                        return new SslSessionResult { Success = false, Error = "GatewayPageURL missing in SSLCommerz response.", RawJson = json };
                    }

                    return new SslSessionResult
                    {
                        Success = true,
                        GatewayPageUrl = gatewayUrl,
                        SessionKey = sessionKey,
                        RawJson = json
                    };
                }

                var failedReason = root.TryGetProperty("failedreason", out var fr) ? fr.GetString() : status ?? "Unknown error";
                return new SslSessionResult { Success = false, Error = failedReason, RawJson = json };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSLCommerz initiate failed for {TranId}", payment.TranId);
                return new SslSessionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SslValidationResult> ValidatePaymentAsync(string valId, CancellationToken ct = default)
        {
            var result = new SslValidationResult { IsValid = false };
            if (string.IsNullOrWhiteSpace(valId))
            {
                result.Status = "INVALID";
                return result;
            }

            var url = $"{_opt.ValidationApiUrl}?val_id={Uri.EscapeDataString(valId)}" +
                      $"&store_id={Uri.EscapeDataString(_opt.StoreId)}" +
                      $"&store_passwd={Uri.EscapeDataString(_opt.StorePassword)}" +
                      "&format=json";

            try
            {
                var json = await _http.GetStringAsync(url, ct);
                result.RawJson = json;
                _logger.LogInformation("SSLCommerz validation response for {ValId}: {Json}", valId, json);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                result.Status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                result.TranId = root.TryGetProperty("tran_id", out var ti) ? ti.GetString() : null;
                result.ValId = root.TryGetProperty("val_id", out var vi) ? vi.GetString() : valId;
                result.BankTranId = root.TryGetProperty("bank_tran_id", out var bt) ? bt.GetString() : null;
                result.CardType = root.TryGetProperty("card_type", out var ct2) ? ct2.GetString() : null;
                result.CardBrand = root.TryGetProperty("card_brand", out var cb) ? cb.GetString() : null;
                result.Currency = root.TryGetProperty("currency", out var cur) ? cur.GetString() : null;

                if (root.TryGetProperty("amount", out var amt) &&
                    decimal.TryParse(amt.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                    result.Amount = amount;

                if (root.TryGetProperty("store_amount", out var sa) &&
                    decimal.TryParse(sa.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var storeAmt))
                    result.StoreAmount = storeAmt;

                if (root.TryGetProperty("risk_level", out var rl) &&
                    int.TryParse(rl.GetString(), out var risk))
                    result.RiskLevel = risk;

                // VALID or VALIDATED means payment is good
                result.IsValid = result.Status.Equals("VALID", StringComparison.OrdinalIgnoreCase)
                              || result.Status.Equals("VALIDATED", StringComparison.OrdinalIgnoreCase);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSLCommerz validation failed for {ValId}", valId);
                result.Status = "ERROR";
                result.RawJson = ex.Message;
                return result;
            }
        }
    }
}
