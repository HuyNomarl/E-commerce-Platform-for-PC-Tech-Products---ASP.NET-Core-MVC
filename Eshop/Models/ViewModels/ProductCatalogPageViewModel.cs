using Eshop.Models;

namespace Eshop.Models.ViewModels
{
    public class ProductCatalogPageViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string BreadcrumbLabel { get; set; } = "Danh mục";
        public string SummaryText { get; set; } = string.Empty;
        public string? CurrentSlug { get; set; }
        public string? SortBy { get; set; }
        public int TotalProducts { get; set; }
        public List<ProductModel> Products { get; set; } = new();
        public ProductCatalogSidebarViewModel Sidebar { get; set; } = new();
    }

    public class ProductCatalogSidebarViewModel
    {
        public string? CurrentSlug { get; set; }
        public string? SortBy { get; set; }
        public string FormActionUrl { get; set; } = string.Empty;
        public string ClearFiltersUrl { get; set; } = string.Empty;
        public bool HasActiveFilters { get; set; }
        public List<ProductCatalogRouteValueViewModel> PreservedRouteValues { get; set; } = new();
        public List<ProductCatalogFilterOptionViewModel> PriceRanges { get; set; } = new();
        public List<ProductCatalogFilterOptionViewModel> Publishers { get; set; } = new();
        public List<ProductCatalogFilterOptionViewModel> CpuOptions { get; set; } = new();
        public List<ProductCatalogFilterOptionViewModel> RamOptions { get; set; } = new();
        public List<ProductCatalogFilterOptionViewModel> GpuOptions { get; set; } = new();
    }

    public class ProductCatalogFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool Selected { get; set; }
    }

    public class ProductCatalogRouteValueViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
