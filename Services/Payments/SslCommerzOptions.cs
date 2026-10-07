namespace IssueFlow.Services.Payments
{
    public class SslCommerzOptions
    {
        public const string SectionName = "SslCommerz";

        /// <summary>Store ID from SSLCommerz merchant panel (sandbox or live).</summary>
        public string StoreId { get; set; } = string.Empty;

        /// <summary>Store password (API password).</summary>
        public string StorePassword { get; set; } = string.Empty;

        /// <summary>true = sandbox.sslcommerz.com, false = securepay.sslcommerz.com</summary>
        public bool IsSandbox { get; set; } = true;

        /// <summary>
        /// Public base URL of this app for callbacks, e.g. https://yourdomain.com
        /// For local testing use ngrok: https://xxxx.ngrok-free.app
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        public string SessionApiUrl => IsSandbox
            ? "https://sandbox.sslcommerz.com/gwprocess/v4/api.php"
            : "https://securepay.sslcommerz.com/gwprocess/v4/api.php";

        public string ValidationApiUrl => IsSandbox
            ? "https://sandbox.sslcommerz.com/validator/api/validationserverAPI.php"
            : "https://securepay.sslcommerz.com/validator/api/validationserverAPI.php";
    }
}
