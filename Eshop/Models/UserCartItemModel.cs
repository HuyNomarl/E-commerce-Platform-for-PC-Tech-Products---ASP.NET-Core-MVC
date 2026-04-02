namespace Eshop.Models
{
    public class UserCartItemModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUserModel? User { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal BasePrice { get; set; }
        public decimal OptionPrice { get; set; }
        public string? Image { get; set; }
        public string LineKey { get; set; } = string.Empty;
        public string? BuildGroupKey { get; set; }
        public int? PcBuildId { get; set; }
        public string? BuildName { get; set; }
        public bool IsPcBuildItem { get; set; }
        public string? ComponentType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<UserCartItemOptionModel> SelectedOptions { get; set; } = new();
    }
}
