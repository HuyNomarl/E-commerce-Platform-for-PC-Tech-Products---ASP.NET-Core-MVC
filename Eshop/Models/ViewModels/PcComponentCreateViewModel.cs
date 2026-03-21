using Eshop.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class ProductSpecificationInputViewModel
    {
        public int SpecificationDefinitionId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public SpecificationDataType DataType { get; set; }
        public string? Unit { get; set; }

        public string? ValueText { get; set; }
        public decimal? ValueNumber { get; set; }
        public bool? ValueBool { get; set; }
        public string? ValueJson { get; set; }
    }

    //public class PcComponentCreateViewModel
    //{
    //    public ProductModel Product { get; set; } = new ProductModel();

    //    public List<ProductSpecificationInputViewModel> Specifications { get; set; } = new();

    //    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
    //    public IEnumerable<SelectListItem> Publishers { get; set; } = Enumerable.Empty<SelectListItem>();
    //}
}