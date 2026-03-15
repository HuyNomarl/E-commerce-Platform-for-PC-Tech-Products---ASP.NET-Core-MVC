namespace Eshop.Models
{
    public class CartItemOptionModel
    {
        public int OptionGroupId { get; set; }
        public int OptionValueId { get; set; }
        public string GroupName { get; set; }
        public string ValueName { get; set; }
        public decimal AdditionalPrice { get; set; }
    }
}