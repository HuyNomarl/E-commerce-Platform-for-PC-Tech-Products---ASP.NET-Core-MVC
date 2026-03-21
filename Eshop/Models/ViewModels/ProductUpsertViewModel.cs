using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class ProductUpsertViewModel
    {
        public ProductModel Product { get; set; } = new();

        [Display(Name = "Ảnh sản phẩm")]
        public List<IFormFile> ImageUploads { get; set; } = new();

        [Display(Name = "File/ảnh thông số kỹ thuật")]
        public IFormFile? TechnicalFileUpload { get; set; }
        public string? WidgetImagesJson { get; set; }
        // Ảnh đại diện
        public string? PrimaryImageSource { get; set; }   // "new", "existing", "legacy"
        public int? PrimaryNewImageIndex { get; set; }
        public long? PrimaryExistingImageId { get; set; }

        // Ảnh cũ cần xóa khi Edit
        public List<long> DeletedImageIds { get; set; } = new();

        // Nếu project cũ còn dùng Product.Image đơn lẻ
        public bool DeleteLegacyImage { get; set; }
    }

    public class WidgetUploadedImageInputViewModel
    {
        public string PublicId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ResourceType { get; set; } = "image";
        public string? OriginalFileName { get; set; }
    }
}