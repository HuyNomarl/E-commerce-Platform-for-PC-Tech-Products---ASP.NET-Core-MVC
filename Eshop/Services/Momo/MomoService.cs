using Eshop.Models;
using Eshop.Models.Momo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System.Security.Cryptography;
using System.Text;

namespace Eshop.Services.Momo
{
    public class MomoService : IMomoService
    {
        private readonly IOptions<MomoOptionModel> _options;

        public MomoService(IOptions<MomoOptionModel> options)
        {
            _options = options;
        }

        public async Task<MomoCreatePaymentResponseModel> CreatePaymentAsync(OrderInfoModel model)
        {
            model.OrderId = string.IsNullOrWhiteSpace(model.OrderId)
                ? DateTime.UtcNow.Ticks.ToString()
                : model.OrderId.Trim();
            model.OrderInfomation = "Khách hàng: " + (model.FullName ?? "") + ". Nội dung: " + (model.OrderInfomation ?? "");

            if (_options.Value.UseMock)
            {
                return new MomoCreatePaymentResponseModel
                {
                    OrderId = model.OrderId,
                    RequestId = model.OrderId,
                    ResultCode = 0,
                    Message = "Mock Momo thành công",
                    PayUrl = $"/Payment/PaymentCallback?orderId={model.OrderId}&amount={model.Amount}&orderInfo={Uri.EscapeDataString(model.OrderInfomation)}&resultCode=0&message={Uri.EscapeDataString("Success")}"
                };
            }

            var extraData = "";

            var rawData =
                $"accessKey={_options.Value.AccessKey}" +
                $"&amount={model.Amount}" +
                $"&extraData={extraData}" +
                $"&ipnUrl={_options.Value.NotifyUrl}" +
                $"&orderId={model.OrderId}" +
                $"&orderInfo={model.OrderInfomation}" +
                $"&partnerCode={_options.Value.PartnerCode}" +
                $"&redirectUrl={_options.Value.ReturnUrl}" +
                $"&requestId={model.OrderId}" +
                $"&requestType={_options.Value.RequestType}";

            var signature = ComputeHmacSha256(rawData, _options.Value.SecretKey);

            var requestData = new
            {
                partnerCode = _options.Value.PartnerCode,
                requestId = model.OrderId,
                amount = model.Amount.ToString(),
                orderId = model.OrderId,
                orderInfo = model.OrderInfomation,
                redirectUrl = _options.Value.ReturnUrl,
                ipnUrl = _options.Value.NotifyUrl,
                lang = "vi",
                requestType = _options.Value.RequestType,
                autoCapture = true,
                extraData = extraData,
                signature = signature
            };

            var client = new RestClient(_options.Value.MomoApiUrl);
            var request = new RestRequest("", RestSharp.Method.Post);
            request.AddJsonBody(requestData);

            var response = await client.ExecuteAsync(request);

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                return new MomoCreatePaymentResponseModel
                {
                    ResultCode = -1,
                    Message = "MoMo không trả về dữ liệu"
                };
            }

            var result = JsonConvert.DeserializeObject<MomoCreatePaymentResponseModel>(response.Content);

            return result ?? new MomoCreatePaymentResponseModel
            {
                ResultCode = -1,
                Message = "Không đọc được phản hồi từ MoMo"
            };
        }

        public MomoExecuteResponseModel PaymentExecuteAsync(IQueryCollection collection)
        {
            return new MomoExecuteResponseModel
            {
                Amount = collection["amount"].ToString(),
                OrderId = collection["orderId"].ToString(),
                OrderInfo = collection["orderInfo"].ToString(),
                ResultCode = collection["resultCode"].ToString(),
                Message = collection["message"].ToString()
            };
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(messageBytes);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}
