using Eshop.Libraries;
using Eshop.Models.VNPay;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Eshop.Services.VNPay
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VnPayService> _logger;

        public VnPayService(IConfiguration configuration, ILogger<VnPayService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            var timeZoneId = GetRequiredSetting("TimeZoneId");
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            var callbackUrl = ResolveVnPayReturnUrl();
            var baseUrl = GetRequiredSetting("Vnpay:BaseUrl");
            var hashSecret = GetRequiredSetting("Vnpay:HashSecret");
            var tmnCode = GetRequiredSetting("Vnpay:TmnCode");

            var txnRef = string.IsNullOrWhiteSpace(model.TxnRef)
                ? $"DH{timeNow:yyyyMMddHHmmss}"
                : model.TxnRef.Trim();

            var orderDescription = string.IsNullOrWhiteSpace(model.OrderDescription)
                ? "Thanh toan don hang"
                : NormalizeVnPayOrderInfo(model.OrderDescription);

            var orderType = string.IsNullOrWhiteSpace(model.OrderType)
                ? "other"
                : model.OrderType;

            var pay = new VnPayLibrary();

            pay.AddRequestData("vnp_Version", GetRequiredSetting("Vnpay:Version"));
            pay.AddRequestData("vnp_Command", GetRequiredSetting("Vnpay:Command"));
            pay.AddRequestData("vnp_TmnCode", tmnCode);
            pay.AddRequestData("vnp_Amount", ((long)model.Amount * 100).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_ExpireDate", timeNow.AddMinutes(15).ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", GetRequiredSetting("Vnpay:CurrCode"));
            pay.AddRequestData("vnp_IpAddr", "127.0.0.1");
            pay.AddRequestData("vnp_Locale", GetRequiredSetting("Vnpay:Locale"));
            pay.AddRequestData("vnp_OrderInfo", orderDescription);
            pay.AddRequestData("vnp_OrderType", orderType);
            pay.AddRequestData("vnp_ReturnUrl", callbackUrl);
            pay.AddRequestData("vnp_TxnRef", txnRef);

            var requestData = pay.BuildRequestDataString();
            var paymentUrl = pay.CreateRequestUrl(baseUrl, hashSecret);

            _logger.LogInformation(
                "VNPay request debug. TxnRef={TxnRef}, TmnCode={TmnCode}, Amount={Amount}, CallbackUrl={CallbackUrl}, BaseUrl={BaseUrl}, HashSecretHint={HashSecretHint}, RequestData={RequestData}, PaymentUrl={PaymentUrl}",
                txnRef,
                tmnCode,
                model.Amount,
                callbackUrl,
                baseUrl,
                MaskSecret(hashSecret),
                requestData,
                paymentUrl);

            return paymentUrl;
        }

        public PaymentResponseModel PaymentExecute(IQueryCollection collections)
        {
            var pay = new VnPayLibrary();
            var hashSecret = GetRequiredSetting("Vnpay:HashSecret");

            foreach (var (key, value) in collections)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_", StringComparison.Ordinal))
                {
                    pay.AddResponseData(key, value.ToString());
                }
            }

            var providedHash = collections.FirstOrDefault(x => x.Key == "vnp_SecureHash").Value.ToString();
            var responseData = pay.BuildResponseValidationDataString();
            var expectedHash = pay.ComputeResponseHash(hashSecret);
            var response = pay.GetFullResponseData(collections, hashSecret);

            _logger.LogInformation(
                "VNPay callback debug. OrderId={OrderId}, ResponseCode={ResponseCode}, TransactionStatus={TransactionStatus}, Amount={Amount}, SignatureValid={SignatureValid}, HashSecretHint={HashSecretHint}, ResponseData={ResponseData}, ProvidedHash={ProvidedHash}, ExpectedHash={ExpectedHash}, RawQuery={RawQuery}",
                response.OrderId,
                response.VnPayResponseCode,
                response.TransactionStatus,
                response.Amount,
                response.SignatureValid,
                MaskSecret(hashSecret),
                responseData,
                providedHash,
                expectedHash,
                collections.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal));

            return response;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string NormalizeVnPayOrderInfo(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Thanh toan don hang";

            var noSign = RemoveDiacritics(input);
            noSign = Regex.Replace(noSign, @"[^a-zA-Z0-9\.\-\s]", " ");
            noSign = Regex.Replace(noSign, @"\s+", " ").Trim();

            if (noSign.Length > 255)
                noSign = noSign.Substring(0, 255);

            return noSign;
        }

        private string GetRequiredSetting(string key)
        {
            var value = _configuration[key]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            throw new InvalidOperationException($"Thiếu cấu hình VNPay bắt buộc: {key}");
        }

        private string ResolveVnPayReturnUrl()
        {
            return "https://warriorlike-herbert-galvanoplastically.ngrok-free.dev/Payment/PaymentCallbackVnpay";
        }

        private static string MaskSecret(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return string.Empty;
            }

            if (secret.Length <= 8)
            {
                return new string('*', secret.Length);
            }

            return $"{secret[..4]}...{secret[^4..]}";
        }
    }
}