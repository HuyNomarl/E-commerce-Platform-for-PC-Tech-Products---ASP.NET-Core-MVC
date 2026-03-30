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

        public VnPayService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            var timeZoneId = _configuration["TimeZoneId"];
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            var txnRef = string.IsNullOrWhiteSpace(model.TxnRef)
                ? $"DH{DateTime.Now:yyyyMMddHHmmss}"
                : model.TxnRef.Trim();
            var pay = new VnPayLibrary();
            var urlCallBack = _configuration["PaymentCallBack:ReturnUrl"];

            var orderDescription = string.IsNullOrWhiteSpace(model.OrderDescription)
                ? "Thanh toan don hang"
                : NormalizeVnPayOrderInfo(model.OrderDescription);

            var orderType = string.IsNullOrWhiteSpace(model.OrderType)
                ? "other"
                : model.OrderType;

            pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
            pay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
            pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"]);
            pay.AddRequestData("vnp_Amount", ((long)model.Amount * 100).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_ExpireDate", timeNow.AddMinutes(15).ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);
            pay.AddRequestData("vnp_OrderInfo", orderDescription);
            pay.AddRequestData("vnp_OrderType", orderType);
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", txnRef);

            var paymentUrl = pay.CreateRequestUrl(
                _configuration["Vnpay:BaseUrl"],
                _configuration["Vnpay:HashSecret"]
            );

            Console.WriteLine("===== VNPAY URL =====");
            Console.WriteLine(paymentUrl);
            Console.WriteLine("=====================");

            return paymentUrl;
        }

        public PaymentResponseModel PaymentExecute(IQueryCollection collections)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(collections, _configuration["Vnpay:HashSecret"]);
            return response;
        }
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();

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

            // chỉ giữ chữ, số, khoảng trắng, dấu chấm, gạch ngang
            noSign = Regex.Replace(noSign, @"[^a-zA-Z0-9\.\-\s]", " ");

            // gom khoảng trắng
            noSign = Regex.Replace(noSign, @"\s+", " ").Trim();

            if (noSign.Length > 255)
                noSign = noSign.Substring(0, 255);

            return noSign;
        }
    }
}
