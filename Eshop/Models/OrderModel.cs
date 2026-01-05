namespace Eshop.Models
{
    public class OrderModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedTime { get; set; }
        public int Status { get; set; }
    }
}
