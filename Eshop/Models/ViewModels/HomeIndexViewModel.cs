using Eshop.Models;

namespace Eshop.Models.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<ProductModel> Products { get; set; } = new();
        public List<ProductModel> RecommendedProducts { get; set; } = new();
        public List<SliderModel> Sliders { get; set; } = new();
    }
}
