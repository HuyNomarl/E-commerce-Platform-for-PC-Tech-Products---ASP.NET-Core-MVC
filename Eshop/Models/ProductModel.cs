using Eshop.Models.Enums;
using Eshop.Repository.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductModel
    {
        [Key]
        public int Id { get; set; }

        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập tên sản phẩm")]
        public string Name { get; set; }

        public string Slug { get; set; }

        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập mô tả sản phẩm")]
        public string Description { get; set; }

        public int Quantity { get; set; }
        public int Sold { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập giá sản phẩm!")]
        [Range(0.01, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [ForeignKey("PublisherId")]
        [Required, Range(1, int.MaxValue, ErrorMessage = "Yêu cầu chọn thương hiệu!")]
        public int PublisherId { get; set; }

        [ForeignKey("CategoryId")]
        [Required, Range(1, int.MaxValue, ErrorMessage = "Yêu cầu chọn danh mục!")]
        public int CategoryId { get; set; }

        public bool IsPcBuild { get; set; } = false;

        public ProductType ProductType { get; set; } = ProductType.Normal;
        public PcComponentType? ComponentType { get; set; }

        public CategoryModel Category { get; set; }
        public PublisherModel Publisher { get; set; }

        public ICollection<RatingModel> RatingModel { get; set; } = new List<RatingModel>();

        public string? Image { get; set; }
        public string? ImagePublicId { get; set; }

        [NotMapped]
        [FileExtension]
        public IFormFile? ImageUpload { get; set; }
        public ICollection<ProductImageModel> ProductImages { get; set; } = new List<ProductImageModel>();

        public ProductTechnicalAssetModel? TechnicalAsset { get; set; }

        // PC dựng sẵn
        public ICollection<PrebuiltPcComponentModel> PrebuiltComponents { get; set; } = new List<PrebuiltPcComponentModel>();

        // Option nâng cấp cho PC dựng sẵn / sản phẩm cố định
        public ICollection<ProductOptionGroupModel> OptionGroups { get; set; } = new List<ProductOptionGroupModel>();

        public ICollection<ProductQuantityModel> ProductQuantities { get; set; } = new List<ProductQuantityModel>();

        // Spec kỹ thuật cho builder
        public ICollection<ProductSpecificationModel> Specifications { get; set; } = new List<ProductSpecificationModel>();
        public ICollection<OrderDetails> OrderDetails { get; set; } = new List<OrderDetails>();
    }
}