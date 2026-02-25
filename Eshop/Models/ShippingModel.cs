namespace Eshop.Models
{
    public class ShippingModel
    {
        public int Id { get; set; }

        // CODE (khóa ổn định sau sáp nhập)
        public string CityCode { get; set; } = "";
        public string DistrictCode { get; set; } = "";
        public string WardCode { get; set; } = "";

        // NAME (hiển thị)
        public string City { get; set; } = "";
        public string District { get; set; } = "";
        public string Ward { get; set; } = "";

        public decimal ShippingCost { get; set; }
    }
}