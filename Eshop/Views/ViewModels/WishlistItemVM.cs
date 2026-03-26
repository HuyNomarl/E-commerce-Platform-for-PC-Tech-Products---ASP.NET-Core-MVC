using Eshop.Models;

namespace Eshop.Views.ViewModels
{
    public class WishlistItemVM
    {
        public WishlistModel Wishlist { get; set; } = null!;
        public ProductModel Product { get; set; } = null!;
        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public int AvailableQuantity { get; set; }
        public List<string> SummarySpecs { get; set; } = new();
    }

}
