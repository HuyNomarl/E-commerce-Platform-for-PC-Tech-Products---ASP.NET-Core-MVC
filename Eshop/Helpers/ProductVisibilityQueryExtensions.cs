using Eshop.Models;
using Eshop.Repository;

namespace Eshop.Helpers
{
    public static class ProductVisibilityQueryExtensions
    {
        public static IQueryable<ProductModel> WhereVisibleOnStorefront(
            this IQueryable<ProductModel> query,
            DataContext context)
        {
            return query.Where(product =>
                product.Status == 1 &&
                product.Category.Status == 1 &&
                (
                    context.InventoryStocks.Any(stock =>
                        stock.ProductId == product.Id &&
                        stock.Warehouse.IsActive &&
                        (stock.OnHandQuantity > 0 || stock.ReservedQuantity > 0)) ||
                    !context.InventoryStocks.Any(stock =>
                        stock.ProductId == product.Id &&
                        (stock.OnHandQuantity > 0 || stock.ReservedQuantity > 0))
                ));
        }
    }
}
