namespace Eshop.Models
{
    public class UserCartItemOptionModel
    {
        public int Id { get; set; }
        public int UserCartItemId { get; set; }
        public UserCartItemModel? UserCartItem { get; set; }
        public int OptionGroupId { get; set; }
        public int OptionValueId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public decimal AdditionalPrice { get; set; }
    }
}
