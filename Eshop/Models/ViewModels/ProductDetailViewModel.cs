using Eshop.Models;
using Eshop.Models.ViewModel;

namespace Eshop.Models.ViewModels
{
    public class ProductDetailViewModel
    {
        public ProductModel? ProductDetail { get; set; }

        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Comment { get; set; }

        public int Quantity { get; set; } = 1;
        public int ProductId { get; set; }

        public List<SelectedOptionViewModel> SelectedOptions { get; set; } = new();
    }
}