using Eshop.Models;
using Eshop.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eshop.Areas.Admin.Models.ViewModels
{
    public class EditableProductImageViewModel
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public bool MarkedForDelete { get; set; } = false;

        public IFormFile? ReplacementFile { get; set; }
    }

    public class PcComponentCreateViewModel
    {
        public ProductModel Product { get; set; } = new();

        public List<SelectListItem> Publishers { get; set; } = new();
        public List<SelectListItem> Categories { get; set; } = new();


        public List<ProductSpecificationInputViewModel> Specifications { get; set; } = new();

        // Ảnh mới thêm
        public List<IFormFile> ImageUploads { get; set; } = new();
        public string? WidgetImagesJson { get; set; }

        public string? PrimaryImageSource { get; set; } = "new";
        public int? PrimaryExistingImageId { get; set; }
        public int? PrimaryNewImageIndex { get; set; }

        public bool DeleteLegacyImage { get; set; }
        public List<int> DeletedImageIds { get; set; } = new();

        // Ảnh cũ khi edit
        public List<EditableProductImageViewModel> ExistingImages { get; set; } = new();
    }
}
