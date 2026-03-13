namespace Eshop.Models.Momo
{
    public class MomoCreatePaymentResponseModel
    {
        public string? PayUrl { get; set; }
        public string? Deeplink { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? OrderId { get; set; }
        public string? RequestId { get; set; }
        public string? Message { get; set; }
        public int ResultCode { get; set; }
    }
}