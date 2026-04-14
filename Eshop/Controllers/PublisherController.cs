using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Controllers
{
    public class PublisherController : Controller
    {
        private readonly DataContext _dataContext;

        public PublisherController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index(
            string slug = "",
            string? sort_by = null,
            [FromQuery(Name = "price")] List<string>? price = null,
            [FromQuery(Name = "publisher")] List<string>? publisherFilter = null,
            [FromQuery(Name = "cpu")] List<string>? cpu = null,
            [FromQuery(Name = "ram")] List<string>? ram = null,
            [FromQuery(Name = "gpu")] List<string>? gpu = null)
        {
            var publisherEntity = await _dataContext.Publishers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == slug && p.status == 1);

            if (publisherEntity == null)
            {
                return NotFound();
            }

            var allProducts = await _dataContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .Include(p => p.Specifications)
                    .ThenInclude(s => s.SpecificationDefinition)
                .Where(p => p.PublisherId == publisherEntity.Id)
                .WhereVisibleOnStorefront(_dataContext)
                .ToListAsync();

            var selectedPriceRanges = ProductCatalogFilterHelper.NormalizePriceSelections(price);
            var selectedPublishers = ProductCatalogFilterHelper.NormalizeSelections(publisherFilter != null && publisherFilter.Any()
                ? publisherFilter
                : new List<string> { publisherEntity.Name });
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
                Title = publisherEntity.Name,
                BreadcrumbLabel = "Thương hiệu",
                SummaryText = $"Hiển thị {filteredProducts.Count} sản phẩm từ thương hiệu {publisherEntity.Name}.",
                SortBy = normalizedSort,
                TotalProducts = filteredProducts.Count,
                Products = filteredProducts,
                Sidebar = new ProductCatalogSidebarViewModel
                {
                    SortBy = normalizedSort,
                    FormActionUrl = Url.Action(nameof(Index), "Publisher") ?? string.Empty,
                    ClearFiltersUrl = Url.Action(nameof(Index), "Publisher", new { slug }) ?? string.Empty,
                    PreservedRouteValues = new List<ProductCatalogRouteValueViewModel>
                    {
                        new() { Key = "slug", Value = slug }
                    },
                    HasActiveFilters = selectedPriceRanges.Any()
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

            return View("~/Views/Category/Index.cshtml", viewModel);
        }
    }
}
