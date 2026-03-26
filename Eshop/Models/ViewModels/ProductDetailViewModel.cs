using Eshop.Models;
using Eshop.Models.ViewModel;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class ProductDetailViewModel
    {
        public ProductModel? ProductDetail { get; set; }
        public int Quantity { get; set; } = 1;
        public int ProductId { get; set; }
        public List<SelectedOptionViewModel> SelectedOptions { get; set; } = new();
        public ProductInventorySummaryViewModel? InventorySummary { get; set; }
        public List<ProductGalleryItemViewModel> GalleryItems { get; set; } = new();
        public List<ProductSpecificationDisplayViewModel> Specifications { get; set; } = new();
        public List<ProductModel> RelatedProducts { get; set; } = new();
        public List<RatingModel> Reviews { get; set; } = new();
        public ProductReviewSummaryViewModel ReviewSummary { get; set; } = new();
        public ProductReviewFormViewModel ReviewForm { get; set; } = new();
        public bool IsAuthenticated { get; set; }
        public bool HasPurchasedProduct { get; set; }
        public bool HasExistingReview { get; set; }
        public string? ReviewPermissionMessage { get; set; }
        public string? CurrentUserDisplayName { get; set; }
        public string? CurrentUserEmail { get; set; }
        public string? TechnicalDocumentUrl { get; set; }
        public string? TechnicalDocumentName { get; set; }
        public bool CanSubmitReview => IsAuthenticated && HasPurchasedProduct;
    }

    public class ProductGalleryItemViewModel
    {
        public string Url { get; set; } = string.Empty;
        public string AltText { get; set; } = string.Empty;
        public bool IsMain { get; set; }
    }

    public class ProductSpecificationDisplayViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ProductReviewSummaryViewModel
    {
        public decimal AverageStars { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> CountByStars { get; set; } = new();
    }

    public class ProductReviewFormViewModel
    {
        [Required]
        public int ProductId { get; set; }

        [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao.")]
        public int Stars { get; set; } = 5;

        [StringLength(1000, ErrorMessage = "Nội dung đánh giá tối đa 1000 ký tự.")]
        public string? Comment { get; set; }

        public List<IFormFile> MediaFiles { get; set; } = new();
    }
}
