namespace Eshop.Models.Momo
{
    public class MomoExecuteResponseModel
    {
        public string? FullName { get; set; }
        public string? OrderId { get; set; }
        public string? Amount { get; set; }
        public string? OrderInfo { get; set; }
        public string? ResultCode { get; set; }
        public string? Message { get; set; }

        public bool Success => string.IsNullOrWhiteSpace(ResultCode) || ResultCode == "0";
    }
}
