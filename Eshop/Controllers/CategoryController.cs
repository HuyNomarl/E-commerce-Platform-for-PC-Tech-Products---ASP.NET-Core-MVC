using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Controllers
{
    public class CategoryController : Controller
    {
        private readonly DataContext _dataContext;

        public CategoryController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index(
            string slug,
            string? sort_by,
            [FromQuery(Name = "price")] List<string>? price,
            [FromQuery(Name = "publisher")] List<string>? publisher,
            [FromQuery(Name = "cpu")] List<string>? cpu,
            [FromQuery(Name = "ram")] List<string>? ram,
            [FromQuery(Name = "gpu")] List<string>? gpu)
        {
            var category = await _dataContext.Categories
                .Include(x => x.Children)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == 1);

            if (category == null)
            {
                return NotFound();
            }

            var categoryIds = new List<int> { category.Id };

            if (category.Children != null && category.Children.Any())
            {
                categoryIds.AddRange(category.Children
                    .Where(x => x.Status == 1)
                    .Select(x => x.Id));
            }

            var allProducts = await _dataContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .Include(p => p.Specifications)
                    .ThenInclude(s => s.SpecificationDefinition)
                .Where(p => categoryIds.Contains(p.CategoryId))
                .WhereVisibleOnStorefront(_dataContext)
                .ToListAsync();

            var selectedPriceRanges = ProductCatalogFilterHelper.NormalizePriceSelections(price);
            var selectedPublishers = ProductCatalogFilterHelper.NormalizeSelections(publisher);
            var selectedCpus = ProductCatalogFilterHelper.NormalizeSelections(cpu);
            var selectedRams = ProductCatalogFilterHelper.NormalizeSelections(ram);
            var selectedGpus = ProductCatalogFilterHelper.NormalizeSelections(gpu);
            var normalizedSort = ProductCatalogFilterHelper.NormalizeSort(sort_by);

            var filteredProducts = allProducts
                .Where(product => ProductCatalogFilterHelper.MatchesPriceRanges(product.Price, selectedPriceRanges))
                .Where(product => ProductCatalogFilterHelper.MatchesSelection(product.Publisher?.Name, selectedPublishers))
                .Where(product => ProductCatalogFilterHelper.MatchesSelection(ProductCatalogFilterHelper.ExtractCpuLabel(product), selectedCpus))
                .Where(product => ProductCatalogFilterHelper.MatchesSelection(ProductCatalogFilterHelper.ExtractRamLabel(product), selectedRams))
                .Where(product => ProductCatalogFilterHelper.MatchesSelection(ProductCatalogFilterHelper.ExtractGpuLabel(product), selectedGpus))
                .ToList();

            filteredProducts = ProductCatalogFilterHelper.ApplySorting(filteredProducts, normalizedSort);

            var viewModel = new ProductCatalogPageViewModel
            {
                Title = category.Name,
                BreadcrumbLabel = "Danh mục",
                SummaryText = $"Hiển thị {filteredProducts.Count} sản phẩm phù hợp trong danh mục này.",
                CurrentSlug = category.Slug,
                SortBy = normalizedSort,
                TotalProducts = filteredProducts.Count,
                Products = filteredProducts,
                Sidebar = new ProductCatalogSidebarViewModel
                {
                    CurrentSlug = category.Slug,
                    SortBy = normalizedSort,
                    FormActionUrl = Url.Action("Index", "Category") ?? string.Empty,
                    ClearFiltersUrl = Url.Action("Index", "Category", new { slug = category.Slug }) ?? string.Empty,
                    PreservedRouteValues = new List<ProductCatalogRouteValueViewModel>
                    {
                        new() { Key = "slug", Value = category.Slug }
                    },
                    HasActiveFilters = selectedPriceRanges.Any()
                        || selectedPublishers.Any()
                        || selectedCpus.Any()
                        || selectedRams.Any()
                        || selectedGpus.Any(),
                    PriceRanges = ProductCatalogFilterHelper.BuildPriceRanges(allProducts, selectedPriceRanges),
                    Publishers = ProductCatalogFilterHelper.BuildFilterOptions(allProducts, x => x.Publisher?.Name, selectedPublishers),
                    CpuOptions = ProductCatalogFilterHelper.BuildFilterOptions(allProducts, ProductCatalogFilterHelper.ExtractCpuLabel, selectedCpus),
                    RamOptions = ProductCatalogFilterHelper.BuildFilterOptions(allProducts, ProductCatalogFilterHelper.ExtractRamLabel, selectedRams),
                    GpuOptions = ProductCatalogFilterHelper.BuildFilterOptions(allProducts, ProductCatalogFilterHelper.ExtractGpuLabel, selectedGpus)
                }
            };

            return View(viewModel);
        }
    }
}
